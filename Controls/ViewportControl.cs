using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;
using System;
using System.Globalization;
using System.Numerics;

namespace Damascus.Mapper.Controls
{
	public class ViewportControl : Control
	{
		public AreaDocument? Area { get; set; }

		public Vector3 LightDirection { get; set; } =
			Vector3.Normalize(new Vector3(0.5f, -1.0f, 0.3f));

		private float _yaw = -0.8f;
		private float _pitch = 0.6f;
		private float _distance = 20.0f;

		private float[] _depthBuffer = Array.Empty<float>();

		private Point _lastMouse;
		private bool _panning;
		private Vector3 _target = Vector3.Zero;

		private Matrix4x4 _viewMatrix;
		private Matrix4x4 _projectionMatrix;

		private WriteableBitmap? _backbuffer;
		private int _renderWidth;
		private int _renderHeight;

		private readonly DispatcherTimer _timer;

		private const int TilePixelSize = 32;
		private readonly long _startTimestamp = Environment.TickCount64;

		public ViewportControl()
		{
			ClipToBounds = true;

			_timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(32) };
			_timer.Tick += (_, _) => InvalidateVisual();
		}

		// ── Lifecycle ─────────────────────────────────────────────────────────

		protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnAttachedToVisualTree(e);
			_timer.Start();
		}

		protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
		{
			base.OnDetachedFromVisualTree(e);
			_timer.Stop();
		}

		// ── Input ─────────────────────────────────────────────────────────────

		private void UpdateCursor(bool leftDown)
		{
			Cursor = _panning
				? new Cursor(StandardCursorType.Hand)
				: leftDown
					? new Cursor(StandardCursorType.SizeAll)
					: Cursor.Default;
		}

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);
			var pt = e.GetCurrentPoint(this);
			_lastMouse = pt.Position;
			e.Pointer.Capture(this);

			if (pt.Properties.IsMiddleButtonPressed)
				_panning = true;

			UpdateCursor(pt.Properties.IsLeftButtonPressed);
		}

		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			base.OnPointerReleased(e);
			_panning = false;
			e.Pointer.Capture(null);
			Cursor = Cursor.Default;
		}

		protected override void OnPointerMoved(PointerEventArgs e)
		{
			base.OnPointerMoved(e);

			if (e.Pointer.Captured != this)
				return;

			var pt = e.GetCurrentPoint(this);
			var pos = pt.Position;

			var dx = (float)(pos.X - _lastMouse.X);
			var dy = (float)(pos.Y - _lastMouse.Y);
			_lastMouse = pos;

			UpdateCursor(pt.Properties.IsLeftButtonPressed);

			if (_panning)
			{
				float panSpeed = _distance * 0.002f;

				Vector3 right = new(
					MathF.Cos(_yaw + MathF.PI * 0.5f),
					0.0f,
					MathF.Sin(_yaw + MathF.PI * 0.5f));

				Vector3 forward = new(
					MathF.Cos(_yaw),
					0.0f,
					MathF.Sin(_yaw));

				_target += (-right * dx + forward * dy) * panSpeed;
				InvalidateVisual();
				return;
			}

			if (pt.Properties.IsLeftButtonPressed)
			{
				_yaw += dx * 0.01f;
				_pitch += dy * 0.01f;
				_pitch = Math.Clamp(_pitch, -1.5f, 1.5f);
				InvalidateVisual();
			}
		}

		protected override void OnPointerEntered(PointerEventArgs e)
		{
			base.OnPointerEntered(e);
			Cursor = Cursor.Default;
		}

		protected override void OnPointerExited(PointerEventArgs e)
		{
			base.OnPointerExited(e);
			Cursor = Cursor.Default;
		}

		protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
		{
			base.OnPointerWheelChanged(e);
			_distance *= e.Delta.Y > 0 ? 0.9f : 1.1f;
			_distance = Math.Clamp(_distance, 4.0f, 100.0f);
			InvalidateVisual();
		}

		// ── Rendering ─────────────────────────────────────────────────────────

		/// <summary>
		/// Lazily allocates (or re-allocates on resize) the backbuffer and depth buffer.
		/// </summary>
		private void EnsureBackbuffer(int width, int height)
		{
			if (_backbuffer != null && _renderWidth == width && _renderHeight == height)
				return;

			_backbuffer?.Dispose();
			_backbuffer = new WriteableBitmap(
				new PixelSize(width, height),
				new Avalonia.Vector(96, 96),
				PixelFormat.Bgra8888,
				AlphaFormat.Unpremul);

			_depthBuffer = new float[width * height];
			_renderWidth = width;
			_renderHeight = height;
		}

		public override void Render(DrawingContext context)
		{
			base.Render(context);

			context.FillRectangle(Brushes.Black, new Rect(Bounds.Size));

			if (Area == null)
				return;

			int width = (int)Bounds.Width;
			int height = (int)Bounds.Height;
			if (width <= 0 || height <= 0)
				return;

			EnsureBackbuffer(width, height);

			// Build camera matrices
			var eye = new Vector3(
				_target.X + MathF.Cos(_yaw) * MathF.Cos(_pitch) * _distance,
				_target.Y + MathF.Sin(_pitch) * _distance,
				_target.Z + MathF.Sin(_yaw) * MathF.Cos(_pitch) * _distance);

			_viewMatrix = Matrix4x4.CreateLookAt(eye, _target, Vector3.UnitY);

			_projectionMatrix = Matrix4x4.CreatePerspectiveFieldOfView(
				MathF.PI / 4f,
				width / (float)height,
				0.1f,
				1000f);

			// Lock backbuffer ONCE for the whole frame
			using (var fb = _backbuffer!.Lock())
			{
				unsafe
				{
					byte* ptr = (byte*)fb.Address;
					int stride = fb.RowBytes;

					// Clear to black
					for (int y = 0; y < height; y++)
					{
						uint* row = (uint*)(ptr + y * stride);
						for (int x = 0; x < width; x++)
							row[x] = 0xFF000000;
					}

					for (int i = 0; i < _depthBuffer.Length; i++)
						_depthBuffer[i] = 1.0f;

					DrawMap(ptr, stride, width, height);
				}
			}

			// Blit
			context.DrawImage(_backbuffer, new Rect(0, 0, width, height), new Rect(0, 0, width, height));

			// Debug overlay
			DrawText(context, $"Eye: {eye.X:F1}, {eye.Y:F1}, {eye.Z:F1}", new Point(10, 10));
			DrawText(context, $"Distance: {_distance:F1}", new Point(10, 25));

			// Target indicator
			Vector3 clipTarget = Transform(_target);
			if (clipTarget.Z is >= 0.0f and <= 1.0f)
			{
				var screenTarget = Project(clipTarget, width, height);
				const float r = 2.0f;
				context.DrawEllipse(
					new SolidColorBrush(Avalonia.Media.Colors.Magenta),
					null,
					screenTarget,
					r, r);
			}
		}

		private void DrawText(DrawingContext context, string text, Point position)
		{
			var ft = new FormattedText(
				text,
				CultureInfo.CurrentCulture,
				FlowDirection.LeftToRight,
				Typeface.Default,
				12.0,
				Brushes.White);

			context.DrawText(ft, position);
		}

		private Vector3 Transform(Vector3 worldPos)
		{
			Vector4 pos = new(worldPos, 1.0f);
			pos = Vector4.Transform(pos, _viewMatrix);
			pos = Vector4.Transform(pos, _projectionMatrix);

			if (MathF.Abs(pos.W) > 0.0001f)
			{
				pos.X /= pos.W;
				pos.Y /= pos.W;
				pos.Z /= pos.W;
				pos.Z = pos.Z * 0.5f + 0.5f;
			}

			return new Vector3(pos.X, pos.Y, pos.Z);
		}

		/// <summary>Clip space [-1,1] → screen pixels.</summary>
		private static Point Project(Vector3 clip, int width, int height)
		{
			float x = (clip.X + 1.0f) * 0.5f * width;
			float y = (1.0f - clip.Y) * 0.5f * height;
			return new Point(x, y);
		}

		// ── Map / mesh rasterizer (pixel-level; unchanged from WinForms) ──────

		private unsafe void DrawMap(byte* ptr, int stride, int width, int height)
		{
			if (Area == null) return;

			for (int ay = 0; ay < Area.Height; ay++)
			{
				for (int ax = 0; ax < Area.Width; ax++)
				{
					var map = Area.GetMap(ax, ay);

					if (map == null || map.IsGhost)
						continue;

					int offsetX = ax * MapDocument.WIDTH;
					int offsetY = ay * MapDocument.HEIGHT;

					for (int layer = 0; layer < MapDocument.LAYERS; layer++)
					{
						for (int y = 0; y < MapDocument.HEIGHT; y++)
						{
							for (int x = 0; x < MapDocument.WIDTH; x++)
							{
								var tileRef = map.Tiles[layer][y][x];

								if (tileRef.Tileset >= Area.Tilesets.Count)
									continue;

								var ts = Area.Tilesets[tileRef.Tileset];
								if (tileRef.TileId >= ts.Tiles.Count)
									continue;

								var def = ts.Tiles[tileRef.TileId];
								if (def == null || def.TileType == TileType.None)
									continue;

								var tileWorldPos = new Vector3(x + offsetX, layer, y + offsetY);

								foreach (var primitive in def.GetPrimitives())
								{
									if (primitive == null)
										continue;

									int frameIndex = 0;
									int frameHeight = primitive.Texture.Height;

									if (def is TileAnimated animated && animated.FrameRate > 0)
									{
										frameHeight = TilePixelSize;
										int frameCount = Math.Max(1, primitive.Texture.Height / frameHeight);
										int ticksPerFrame = Math.Max(1, 60 / animated.FrameRate);
										long elapsedTicks = (Environment.TickCount64 - _startTimestamp) * 60 / 1000;
										frameIndex = (int)(elapsedTicks / ticksPerFrame) % frameCount;
									}

									DrawMesh(primitive, tileWorldPos, ptr, stride, width, height, frameIndex, frameHeight);
								}
							}
						}
					}
				}
			}
		}

		private unsafe void DrawMesh(
			RenderPrimitive prim, Vector3 worldPos,
			byte* ptr, int stride, int width, int height,
			int frameIndex = 0, int frameHeight = 32)
		{
			var mesh = prim.Mesh;

			var world = new Vector3[mesh.Vertices.Length];
			var clip = new Vector3[mesh.Vertices.Length];
			var screen = new Point[mesh.Vertices.Length];

			for (int i = 0; i < mesh.Vertices.Length; i++)
			{
				world[i] = new Vector3(mesh.Vertices[i].Position.x,
										mesh.Vertices[i].Position.y,
										mesh.Vertices[i].Position.z) + worldPos;
				clip[i] = Transform(world[i]);
				screen[i] = Project(clip[i], width, height);
			}

			for (int i = 0; i < mesh.Indices.Length; i += 3)
			{
				int ia = mesh.Indices[i];
				int ib = mesh.Indices[i + 1];
				int ic = mesh.Indices[i + 2];

				float za = clip[ia].Z;
				float zb = clip[ib].Z;
				float zc = clip[ic].Z;

				if (za < 0f || za > 1f ||
					zb < 0f || zb > 1f ||
					zc < 0f || zc > 1f)
					continue;

				Vector3 faceNormal = Vector3.Normalize(
					Vector3.Cross(world[ib] - world[ia], world[ic] - world[ia]));

				DrawTriangle(
					new Vector3((float)screen[ia].X, (float)screen[ia].Y, za),
					new Vector3((float)screen[ib].X, (float)screen[ib].Y, zb),
					new Vector3((float)screen[ic].X, (float)screen[ic].Y, zc),
					faceNormal,
					new Vector2(mesh.Vertices[ia].UV.x, mesh.Vertices[ia].UV.y),
					new Vector2(mesh.Vertices[ib].UV.x, mesh.Vertices[ib].UV.y),
					new Vector2(mesh.Vertices[ic].UV.x, mesh.Vertices[ic].UV.y),
					prim.Texture,
					ptr, stride, width, height,
					frameIndex, frameHeight);
			}
		}

		private unsafe void DrawTriangle(
			Vector3 p0, Vector3 p1, Vector3 p2,
			Vector3 faceNormal,
			Vector2 uv0, Vector2 uv1, Vector2 uv2,
			Texture texture,
			byte* ptr, int stride, int width, int height,
			int frameIndex = 0, int frameHeight = 32)
		{
			// Bounding box
			int minX = (int)MathF.Floor(MathF.Min(p0.X, MathF.Min(p1.X, p2.X)));
			int maxX = (int)MathF.Ceiling(MathF.Max(p0.X, MathF.Max(p1.X, p2.X)));
			int minY = (int)MathF.Floor(MathF.Min(p0.Y, MathF.Min(p1.Y, p2.Y)));
			int maxY = (int)MathF.Ceiling(MathF.Max(p0.Y, MathF.Max(p1.Y, p2.Y)));

			minX = Math.Clamp(minX, 0, width - 1);
			maxX = Math.Clamp(maxX, 0, width - 1);
			minY = Math.Clamp(minY, 0, height - 1);
			maxY = Math.Clamp(maxY, 0, height - 1);

			float denom = Edge(p0, p1, p2);
			if (MathF.Abs(denom) < 1e-6f)
				return;

			Vector3 lightDir = -LightDirection;
			float ndotl = MathF.Max(0.0f, Vector3.Dot(faceNormal, lightDir));
			const float ambient = 0.25f;
			float light = ambient + ndotl * (1.0f - ambient);

			for (int y = minY; y <= maxY; y++)
			{
				for (int x = minX; x <= maxX; x++)
				{
					var p = new Vector3(x + 0.5f, y + 0.5f, 0);

					float w0 = Edge(p1, p2, p);
					float w1 = Edge(p2, p0, p);
					float w2 = Edge(p0, p1, p);

					bool hasPos = (w0 > 0) || (w1 > 0) || (w2 > 0);
					bool hasNeg = (w0 < 0) || (w1 < 0) || (w2 < 0);

					if (hasPos && hasNeg)
						continue;

					w0 /= denom;
					w1 /= denom;
					w2 /= denom;

					float z = p0.Z * w0 + p1.Z * w1 + p2.Z * w2;

					int idx = y * width + x;
					if (z >= _depthBuffer[idx])
						continue;

					_depthBuffer[idx] = z;

					float u = uv0.X * w0 + uv1.X * w1 + uv2.X * w2;
					float v = uv0.Y * w0 + uv1.Y * w1 + uv2.Y * w2;

					uint color = texture.SampleFrame(u, v, frameIndex, frameHeight);

					uint al = (color >> 24) & 0xFF;
					uint r = (color >> 16) & 0xFF;
					uint g = (color >> 8) & 0xFF;
					uint bl = color & 0xFF;

					r = (uint)Math.Clamp(r * light, 0, 255);
					g = (uint)Math.Clamp(g * light, 0, 255);
					bl = (uint)Math.Clamp(bl * light, 0, 255);

					uint litColor = (al << 24) | (r << 16) | (g << 8) | bl;

					PutPixel(x, y, litColor, ptr, stride, width, height);
				}
			}
		}

		private static float Edge(Vector3 a, Vector3 b, Vector3 c) =>
			(c.X - a.X) * (b.Y - a.Y) -
			(c.Y - a.Y) * (b.X - a.X);

		private static unsafe void PutPixel(
			int x, int y, uint color,
			byte* ptr, int stride, int width, int height)
		{
			if ((uint)x >= (uint)width || (uint)y >= (uint)height)
				return;

			*(uint*)(ptr + y * stride + x * 4) = color;
		}
	}
}