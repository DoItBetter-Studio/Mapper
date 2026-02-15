using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using System.Windows.Forms;

using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;
using System.ComponentModel;

namespace Glyphborn.Mapper.Controls
{
	public class Viewport3D : UserControl
	{
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public AreaDocument? Area { get; set; }

		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Vector3 LightDirection { get; set; } = Vector3.Normalize(new Vector3(0.5f, -1.0f, 0.3f));

		private float _yaw = -0.8f;
		private float _pitch = 0.6f;
		private float _distance = 20.0f;

		private float[] _depthBuffer;

		private Point _lastMouse;
		private bool _panning;
		private Vector3 _target = Vector3.Zero;

		private Matrix4x4 _viewMatrix;
		private Matrix4x4 _projectionMatrix;
		private Bitmap _backbuffer;
		private Timer _timer;

		private SolidBrush _brush;

		public Viewport3D()
		{
			DoubleBuffered = true;
			BackColor = Color.Black;

			_brush = new SolidBrush(Color.FromArgb(100, 150, 200));
			_backbuffer = new Bitmap(Width, Height);
			_depthBuffer = new float[Width * Height];

			_timer = new Timer();
			_timer.Interval = 32;
			_timer.Tick += (_, __) => Invalidate();
			_timer.Start();
		}

		private void UpdateCursor(MouseButtons buttons)
		{
			if (_panning)
				Cursor = Cursors.Hand;
			else if ((buttons & MouseButtons.Left) != 0)
				Cursor = Cursors.SizeAll;
			else
				Cursor = Cursors.Default;
		}

		#region Overrides
		protected override void OnMouseDown(MouseEventArgs e)
		{
			_lastMouse = e.Location;
			Capture = true;

			if (e.Button == MouseButtons.Middle)
				_panning = true;

			UpdateCursor(e.Button);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			_panning = false;
			Capture = false;
			Cursor = Cursors.Default;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			if (!Capture)
				return;

			var dx = e.X - _lastMouse.X;
			var dy = e.Y - _lastMouse.Y;
			_lastMouse = e.Location;

			UpdateCursor(e.Button);

			if (_panning)
			{
				// Pan speed scales with distance
				float panSpeed = _distance * 0.002f;

				// Camera basis vectors (yaw only — keeps grid flat)
				Vector3 right = new(
					MathF.Cos(_yaw + MathF.PI * 0.5f),
					0.0f,
					MathF.Sin(_yaw + MathF.PI * 0.5f)
				);

				Vector3 forward = new(
					MathF.Cos(_yaw),
					0.0f,
					MathF.Sin(_yaw)
				);

				Vector3 pan =
					(-right * dx + forward * dy) * panSpeed;

				_target += pan;
				return;
			}

			if (e.Button == MouseButtons.Left)
			{
				_yaw += dx * 0.01f;
				_pitch += dy * 0.01f;
				_pitch = Math.Clamp(_pitch, -1.5f, 1.5f);
			}
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			Cursor = Cursors.Default;
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			Cursor = Cursors.Default;
		}

		protected override void OnMouseWheel(MouseEventArgs e)
		{
			_distance *= e.Delta > 0 ? 0.9f : 1.1f;
			_distance = Math.Clamp(_distance, 4.0f, 100.0f);
			Invalidate();
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);

			_backbuffer?.Dispose();
			if (Width > 0 && Height > 0)
			{
				_backbuffer = new Bitmap(Width, Height);
				_depthBuffer = new float[Width * Height];
			}
		}

		protected override void OnVisibleChanged(EventArgs e)
		{
			base.OnVisibleChanged(e);
			_timer.Enabled = Visible;
		}
		#endregion

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			var g = e.Graphics;
			g.Clear(Color.Black);

			if (Area == null)
				return;

			var eye = new Vector3(
				_target.X + MathF.Cos(_yaw) * MathF.Cos(_pitch) * _distance,
				_target.Y + MathF.Sin(_pitch) * _distance,
				_target.Z + MathF.Sin(_yaw) * MathF.Cos(_pitch) * _distance);

			_viewMatrix = Matrix4x4.CreateLookAt(
				eye,
				_target,
				Vector3.UnitY
			);

			float aspect = Width / (float) Height;
			_projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
				MathF.PI / 4f,
				aspect,
				0.1f,
				1000f
			);

			// Lock backbuffer ONCE for the whole frame
			var data = _backbuffer.LockBits(
				new Rectangle(0, 0, Width, Height),
				ImageLockMode.WriteOnly,
				PixelFormat.Format32bppArgb
			);

			unsafe
			{
				byte* ptr = (byte*) data.Scan0;
				int stride = data.Stride;

				// Clear raw pixel buffer
				for (int y = 0; y < Height; y++)
				{
					uint* row = (uint*) (ptr + y * stride);
					for (int x = 0; x < Width; x++)
						row[x] = 0xFF000000; // black
				}

				for (int i = 0; i < _depthBuffer.Length; i++)
					_depthBuffer[i] = 1.0f;

				DrawMap(ptr, stride);
			}

			_backbuffer.UnlockBits(data);

			// Blit final image once
			e.Graphics.DrawImage(_backbuffer, 0, 0);

			//DrawMap(g);

			// Draw debug info
			g.DrawString($"Eye: {eye.X:F1}, {eye.Y:F1}, {eye.Z:F1}", Font, Brushes.White, 10, 10);
			g.DrawString($"Distance: {_distance:F1}", Font, Brushes.White, 10, 25);

			// ---- Target indicator ----
			Vector3 clipTarget = Transform(_target);

			// Only draw if inside clip depth
			if (clipTarget.Z >= 0.0f && clipTarget.Z <= 1.0f)
			{
				PointF screenTarget = Project(clipTarget);

				const float r = 2.0f;
				using var brush = new SolidBrush(Color.Magenta);

				e.Graphics.FillEllipse(
					brush,
					screenTarget.X - r,
					screenTarget.Y - r,
					r * 2,
					r * 2
				);
			}
		}

		private Vector3 Transform(Vector3 worldPos)
		{
			Vector4 pos = new Vector4(worldPos, 1.0f);
			pos = Vector4.Transform(pos, _viewMatrix);
			pos = Vector4.Transform(pos, _projectionMatrix);

			// Perspective divide
			if (MathF.Abs(pos.W) > 0.0001f)
			{
				pos.X /= pos.W;
				pos.Y /= pos.W;
				pos.Z /= pos.W;

				pos.Z = pos.Z * 0.5f + 0.5f;
			}

			return new Vector3(pos.X, pos.Y, pos.Z);
		}

		private PointF Project(Vector3 clipSpacePos)
		{
			// Clip space [-1, 1] -> Screen Space [0, Width], [0, Height]
			float x = (clipSpacePos.X + 1.0f) * 0.5f * Width;
			float y = (1.0f - clipSpacePos.Y) * 0.5f * Height;

			return new PointF(x, y);
		}

		private unsafe void DrawMap(byte* ptr, int stride)
		{
			for (int ay = 0; ay < Area!.Height; ay++)
			{
				for (int ax = 0; ax < Area.Width; ax++)
				{
					var map = Area.GetMap(ax, ay);
					int offsetX = ax * MapDocument.WIDTH;
					int offsetY = ay * MapDocument.HEIGHT;

					if (map == null)
						return;

					// Draw each tile as a wireframe cube
					for (int layer = 0; layer < MapDocument.LAYERS; layer++)
					{
						for (int y = 0; y < MapDocument.HEIGHT; y++)
						{
							for (int x = 0; x < MapDocument.WIDTH; x++)
							{
								var tileRef = map.Tiles[layer][y][x];

								if (tileRef.TileId == 0)
									continue;

								TileDefinition def = Area.Tilesets[tileRef.Tileset].Tiles[tileRef.TileId];

								DrawMesh(def.Primitive!, new Vector3(x + offsetX, layer, y + offsetY), ptr, stride);
							}
						}
					}
				}
			}
		}

		private unsafe void DrawMap(Graphics g)
		{
			for (int ay = 0; ay < Area!.Height; ay++)
			{
				for (int ax = 0; ax < Area.Width; ax++)
				{
					var map = Area.GetMap(ax, ay);
					int offsetX = ax * MapDocument.WIDTH;
					int offsetY = ay * MapDocument.HEIGHT;

					if (map == null)
						return;

					// Draw each tile as a wireframe cube
					for (int layer = 0; layer < MapDocument.LAYERS; layer++)
					{
						for (int y = 0; y < MapDocument.HEIGHT; y++)
						{
							for (int x = 0; x < MapDocument.WIDTH; x++)
							{
								var tileRef = map.Tiles[layer][y][x];

								if (tileRef.TileId == 0)
									continue;

								DrawCube(g, new Vector3(x + offsetX, layer, y + offsetY), 1.0f);
							}
						}
					}
				}
			}
		}

		private void DrawCube(Graphics g, Vector3 center, float size)
		{
			float h = size * 0.5f;

			Vector3[] corners =
			{
				new(-h, -h, -h), new(h, -h, -h),  // Bottom front-left, front-right
				new(h,  h, -h), new(-h,  h, -h),  // Top front-right, front-left
				new(-h, -h,  h), new(h, -h,  h),  // Bottom back-left, back-right
				new(h,  h,  h), new(-h,  h,  h),  // Top back-right, back-left
			};

			// Transform to world position
			for (int i = 0; i < corners.Length; i++)
			{
				corners[i] += center;
			}

			// Transform to NDC (clip space after perspective divide)
			Vector3[] clipSpace = new Vector3[8];
			for (int i = 0; i < 8; i++)
			{
				clipSpace[i] = Transform(corners[i]);
			}

			// Cull if all points are outside the [0,1] depth range
			bool allOutside = true;
			for (int i = 0; i < 8; i++)
			{
				float z = clipSpace[i].Z;
				if (z >= 0.0f && z <= 1.0f)
				{
					allOutside = false;
					break;
				}
			}

			if (allOutside)
				return;

			// Project to screen space
			PointF[] screen = new PointF[8];
			for (int i = 0; i < 8; i++)
			{
				screen[i] = Project(clipSpace[i]);
			}

			// Draw edges
			using (var pen = new Pen(Color.FromArgb(100, 150, 200), 1))
			{
				void DrawEdge(int a, int b)
				{
					float za = clipSpace[a].Z;
					float zb = clipSpace[b].Z;

					// Only draw if both endpoints are within the visible depth range
					if (za >= 0.0f && za <= 1.0f &&
						zb >= 0.0f && zb <= 1.0f)
					{
						g.DrawLine(pen, screen[a], screen[b]);
					}
				}

				// Bottom face
				DrawEdge(0, 1); DrawEdge(1, 2); DrawEdge(2, 3); DrawEdge(3, 0);
				// Top face
				DrawEdge(4, 5); DrawEdge(5, 6); DrawEdge(6, 7); DrawEdge(7, 4);
				// Vertical edges
				DrawEdge(0, 4); DrawEdge(1, 5); DrawEdge(2, 6); DrawEdge(3, 7);
			}
		}

		unsafe void DrawMesh(RenderPrimitive prim, Vector3 worldPos, byte* ptr, int stride)
		{
			var mesh = prim.Mesh;

			// Transform vertices
			Vector3[] world = new Vector3[mesh.Vertices.Length];
			Vector3[] clip = new Vector3[mesh.Vertices.Length];
			PointF[] screen = new PointF[mesh.Vertices.Length];

			for (int i = 0; i < mesh.Vertices.Length; i++)
			{
				world[i] = new Vector3(mesh.Vertices[i].Position.x, mesh.Vertices[i].Position.y, mesh.Vertices[i].Position.z) + worldPos;
				clip[i] = Transform(world[i]);
				screen[i] = Project(clip[i]);
			}

			// Rasterize triangles
			for (int i = 0; i < mesh.Indices.Length; i += 3)
			{
				int a = mesh.Indices[i];
				int b = mesh.Indices[i + 1];
				int c = mesh.Indices[i + 2];

				float za = clip[a].Z;
				float zb = clip[b].Z;
				float zc = clip[c].Z;

				// Require all vertices to be in the visible depth range
				if (za < 0.0f || za > 1.0f ||
					zb < 0.0f || zb > 1.0f ||
					zc < 0.0f || zc > 1.0f)
					continue;

				Vector3 wa = world[a];
				Vector3 wb = world[b];
				Vector3 wc = world[c];

				Vector3 faceNormal = Vector3.Normalize(Vector3.Cross(wb - wa, wc - wa));

				DrawTriangle(
					new Vector3(screen[a].X, screen[a].Y, za),
					new Vector3(screen[b].X, screen[b].Y, zb),
					new Vector3(screen[c].X, screen[c].Y, zc),

					faceNormal,

					new Vector2(mesh.Vertices[a].UV.x, mesh.Vertices[a].UV.y),
					new Vector2(mesh.Vertices[b].UV.x, mesh.Vertices[b].UV.y),
					new Vector2(mesh.Vertices[c].UV.x, mesh.Vertices[c].UV.y),
					prim.Texture,
					ptr, stride
				);
			}
		}

		unsafe void DrawTriangle(
			Vector3 p0, Vector3 p1, Vector3 p2,
			Vector3 faceNormal,
			Vector2 uv0, Vector2 uv1, Vector2 uv2,
			Texture texture,
			byte* ptr, int stride)
		{
			// 2D backface culling
			Vector2 a = new(p1.X - p0.X, p1.Y - p0.Y);
			Vector2 b = new(p2.X - p0.X, p2.Y - p0.Y);

			// TEMP: disable backface culling to debug artifacts
			// float cross = a.X * b.Y - a.Y * b.X;
			// if (cross >= 0)
			//     return;

			// Bounding box
			int minX = (int) MathF.Floor(MathF.Min(p0.X, MathF.Min(p1.X, p2.X)));
			int maxX = (int) MathF.Ceiling(MathF.Max(p0.X, MathF.Max(p1.X, p2.X)));
			int minY = (int) MathF.Floor(MathF.Min(p0.Y, MathF.Min(p1.Y, p2.Y)));
			int maxY = (int) MathF.Ceiling(MathF.Max(p0.Y, MathF.Max(p1.Y, p2.Y)));

			// Clamp to screen
			minX = Math.Clamp(minX, 0, Width - 1);
			maxX = Math.Clamp(maxX, 0, Width - 1);
			minY = Math.Clamp(minY, 0, Height - 1);
			maxY = Math.Clamp(maxY, 0, Height - 1);

			// Edge function denominator (area * 2)
			float denom = Edge(p0, p1, p2);
			if (MathF.Abs(denom) < 1e-6f)
				return;

			// Initialize depth buffer elsewhere each frame:
			// for (int i = 0; i < _depthBuffer.Length; i++) _depthBuffer[i] = 1.0f;

			for (int y = minY; y <= maxY; y++)
			{
				for (int x = minX; x <= maxX; x++)
				{
					Vector3 p = new Vector3(x + 0.5f, y + 0.5f, 0);

					float w0 = Edge(p1, p2, p);
					float w1 = Edge(p2, p0, p);
					float w2 = Edge(p0, p1, p);

					// Accept either all non‑negative or all non‑positive
					bool hasPos = (w0 > 0) || (w1 > 0) || (w2 > 0);
					bool hasNeg = (w0 < 0) || (w1 < 0) || (w2 < 0);
					
					if (hasPos && hasNeg)
						continue; // outside

					// Normalize barycentrics
					w0 /= denom;
					w1 /= denom;
					w2 /= denom;

					// Interpolate NDC depth (0 = near, 1 = far)
					float z = p0.Z * w0 + p1.Z * w1 + p2.Z * w2;

					// Depth test: smaller z is closer
					int idx = y * Width + x;
					if (z >= _depthBuffer[idx])
						continue;

					_depthBuffer[idx] = z;

					Vector3 lightDir = -LightDirection;

					float ndotl = MathF.Max(0.0f, Vector3.Dot(faceNormal, lightDir));

					const float ambient = 0.25f;
					float light = ambient + ndotl * (1.0f - ambient);

					// Simple (non‑perspective‑correct) UV interpolation
					float u = uv0.X * w0 + uv1.X * w1 + uv2.X * w2;
					float v = uv0.Y * w0 + uv1.Y * w1 + uv2.Y * w2;

					uint color = texture.Sample(u, v);

					uint al = (color >> 24) & 0xFF;
					uint r = (color >> 16) & 0xFF;
					uint g = (color >> 8) & 0xFF;
					uint bl = color  & 0xFF;

					r = (uint) Math.Clamp(r * light, 0, 255);
					g = (uint) Math.Clamp(g * light, 0, 255);
					bl = (uint) Math.Clamp(bl * light, 0, 255);

					uint litColor = (al << 24) | (r << 16) | (g << 8) | bl;

					PutPixel(x, y, litColor, ptr, stride);
				}
			}
		}

		float Edge(Vector3 a, Vector3 b, Vector3 c)
		{
			return (c.X - a.X) * (b.Y - a.Y) -
				   (c.Y - a.Y) * (b.X - a.X);
		}

		unsafe void PutPixel(int x, int y, uint color, byte* ptr, int stride)
		{
			if (x < 0 || y < 0 || x >= Width || y >= Height)
				return;

			uint* pixel = (uint*) (ptr + y * stride + x * 4);
			*pixel = color;
		}
	}
}