using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

using Glyphborn.Mapper.Editor;

namespace Glyphborn.Mapper.Controls
{
	public sealed class AreaControl : UserControl
	{
		private AreaDocument? _area;
		public AreaDocument? Area
		{
			get => _area;
			private set
			{
				if (_area == value) return;
				if (_area != null)
					_area.Changed -= Area_Changed;
				_area = value;
				if (_area != null)
					_area.Changed += Area_Changed;
			}
		}

		public event Action<MapDocument?>? MapSelected;

		public EditorState? State;

		private const int CELL = 64;
		private const int HEADER = 30;

		public AreaControl()
		{
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			if (Area == null)
				return;

			var g = e.Graphics;
			g.Clear(BackColor);

			// Draw debug border
			g.DrawString($"Area: {Area.Width}×{Area.Height}", Font, Brushes.White, 5, 5);

			for (int y = 0; y < Area.Height; y++)
				for (int x = 0; x < Area.Width; x++)
				{
					var rect = new Rectangle(
						x * CELL,
						y * CELL + HEADER,
						CELL,
						CELL
					);

					var map = Area.Maps[x, y];

					if (map != null)
					{
						try
						{
							if (map.MiniPreview == null || map.MiniPreviewDirty)
							{
								map.MiniPreview = BuildMiniPreview(map, Area);
								map.MiniPreviewDirty = false;
							}

							if (map.MiniPreview != null)
							{
								g.DrawImage(map.MiniPreview, rect);
							}
							else
							{
								// Preview generation failed
								g.FillRectangle(Brushes.DarkRed, rect);
								g.DrawString("!", Font, Brushes.White, rect.X + 20, rect.Y + 20);
							}
						}
						catch
						{
							// Draw error indicator
							g.FillRectangle(Brushes.DarkRed, rect);
							g.DrawString("X", Font, Brushes.White, rect.X + 20, rect.Y + 20);
						}
					}
					else
					{
						continue;
					}

					// Grid
					g.DrawRectangle(Pens.DimGray, rect);

					// Active map highlight
					if (x == State!.ActiveMapX && y == State!.ActiveMapY)
					{
						using var pen = new Pen(Color.DodgerBlue, 3);
						g.DrawRectangle(pen, rect);
					}
			}
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (Area == null)
				return;

			if (e.Y < HEADER)
				return;

			int x = e.X / CELL;
			int y = (e.Y - HEADER) / CELL;

			if (x < 0 || y < 0 || x >= Area.Width || y >= Area.Height)
				return;

			var map = Area.Maps[x, y];
			if (map == null)
				return;

			State!.ActiveMapX = x;
			State!.ActiveMapY = y;

			MapSelected?.Invoke(map);
			Invalidate();
		}

		public void SetArea(AreaDocument area)
		{
			Area = area;

			// Resize control to fit the area (including header).
			// Parent layout may override this; keep it but call PerformLayout so container can react.
			Size = new Size((area.Width * CELL) + 4, (area.Height * CELL) + (4 + HEADER));

			Invalidate();
			Parent?.PerformLayout();
		}

		private void Area_Changed()
		{
			// AreaDocument may fire Changed from non-UI thread; marshal to UI thread.
			if (InvokeRequired)
			{
				BeginInvoke((Action) Area_Changed);
				return;
			}

			if (Area == null) return;

			// Adjust size if area dimensions changed
			Size = new Size(Area.Width * CELL, Area.Height * CELL + HEADER);

			Invalidate();
			Parent?.PerformLayout();
		}

		public static Bitmap BuildMiniPreview(MapDocument map, AreaDocument area)
		{
			const int TILE_PIXELS = 2;
			const int SIZE = MapDocument.WIDTH * TILE_PIXELS;

			var bmp = new Bitmap(SIZE, SIZE, PixelFormat.Format32bppArgb);

			using var graphics = Graphics.FromImage(bmp);
			graphics.Clear(Color.Black);

			for (int ty = 0; ty < MapDocument.HEIGHT; ty++)
				for (int tx = 0; tx < MapDocument.WIDTH; tx++)
				{
					TileRef tile = default;

					// Find topmost tile
					for (int l = MapDocument.LAYERS - 1; l >= 0; l--)
					{
						var t = map.Tiles[l][ty][tx];

						if (t.TileId != 0)
						{
							tile = t;
							break;
						}
					}

					if (tile.TileId == 0)
						continue;

					if (tile.Tileset >= area.Tilesets.Count)
						continue;

					var ts = area.Tilesets[tile.Tileset];
					if (tile.TileId >= ts.Tiles.Count)
						continue;

					var def = ts.Tiles[tile.TileId];
					if (def.Primitive == null)
						continue;

					var tex = def.Primitive.Texture;
					var src = tex.Pixels;

					// Sample center pixel (fast & stable)
					int sx = tex.Width / 2;
					int sy = tex.Height / 2;

					uint pixelColor = src[sy * tex.Width + sx];

					byte a = (byte) (pixelColor >> 24);
					byte r = (byte) (pixelColor >> 16);
					byte g = (byte) (pixelColor >> 8);
					byte b = (byte) (pixelColor);

					Color c = Color.FromArgb(a, r, g, b);

					using var brush = new SolidBrush(c);

					graphics.FillRectangle(brush, tx * TILE_PIXELS, ty * TILE_PIXELS, TILE_PIXELS, TILE_PIXELS);
				}

			return bmp;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (Area != null)
					Area.Changed -= Area_Changed;
			}
			base.Dispose(disposing);
		}
	}
}
