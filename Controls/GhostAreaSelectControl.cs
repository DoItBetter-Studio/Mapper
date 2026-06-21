using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace Glyphborn.Mapper.Controls
{
	public class GhostAreaSelectControl : Control
	{
		private AreaDocument? _area;
		private HashSet<(int x, int y)> _selectedCells = new();

		private const int CELL = 64;
		private const int HEADER = 30;

		public void SetArea(AreaDocument area)
		{
			_area = area;
			_selectedCells.Clear();
			Size = new Size(area.Width * CELL, area.Height * CELL + HEADER);
			Invalidate();
		}

		public List<(int x, int y, MapDocument map)> GetSelectedCells()
		{
			var result = new List<(int, int, MapDocument)>();
			foreach (var (x, y) in _selectedCells)
			{
				var map = _area?.GetMap(x, y);
				if (map != null)
					result.Add((x, y, map));
			}
			return result;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			if (_area == null)
				return;

			var g = e.Graphics;
			g.Clear(BackColor);

			// Draw debug border
			g.DrawString($"Area: {_area.Width}×{_area.Height}", Font, Brushes.White, 5, 5);

			for (int y = 0; y < _area.Height; y++)
				for (int x = 0; x < _area.Width; x++)
				{
					var rect = new Rectangle(
						x * CELL,
						y * CELL + HEADER,
						CELL,
						CELL
					);

					var map = _area.Maps[x, y];

					if (map != null)
					{
						try
						{
							if (map.MiniPreview == null || map.MiniPreviewDirty)
							{
								map.MiniPreview = BuildMiniPreview(map, _area);
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
					if (_selectedCells.Contains((x, y)))
					{
						using var pen = new Pen(Color.DodgerBlue, 3);
						g.DrawRectangle(pen, rect);
					}
				}
		}

		public static Bitmap BuildMiniPreview(MapDocument map, AreaDocument area)
		{
			const int TILE_PIXELS = 2;
			const int SIZE = MapDocument.WIDTH * TILE_PIXELS;

			var bmp = new Bitmap(SIZE, SIZE, PixelFormat.Format32bppArgb);

			using var graphics = Graphics.FromImage(bmp);
			graphics.Clear(Color.Black);

			for (int ty = 0; ty < MapDocument.HEIGHT; ty++)
			{
				for (int tx = 0; tx < MapDocument.WIDTH; tx++)
				{
					Texture? targetTex = null;

					// Look from the topmost layer downwards to find the first visible surface
					for (int l = MapDocument.LAYERS - 1; l >= 0; l--)
					{
						var t = map.Tiles[l][ty][tx];

						// Guard against unassigned tilesets/tile entries out of range
						if (t.Tileset >= area.Tilesets.Count)
							continue;

						var ts = area.Tilesets[t.Tileset];
						if (t.TileId >= ts.Tiles.Count)
							continue;

						var def = ts.Tiles[t.TileId];

						// Skip air/empty slots entirely
						if (def.TileType == TileType.None)
							continue;

						// Safe extraction from the multi-primitive mesh layout
						var firstPrimitive = def.GetPrimitives().FirstOrDefault();
						if (firstPrimitive?.Texture == null)
							continue; // It's a non-visual logic/trigger tile; keep looking down layers

						// We found our highest visual tile!
						targetTex = firstPrimitive.Texture;
						break;
					}

					// If no visual tile exists in this column, leave it as the background color (Black)
					if (targetTex == null)
						continue;

					var src = targetTex.Pixels;

					// Sample center pixel (fast & stable preview rendering)
					int sx = targetTex.Width / 2;
					int sy = targetTex.Height / 2;

					uint pixelColor = src[sy * targetTex.Width + sx];

					byte a = (byte)(pixelColor >> 24);
					byte r = (byte)(pixelColor >> 16);
					byte g = (byte)(pixelColor >> 8);
					byte b = (byte)(pixelColor);

					Color c = Color.FromArgb(a, r, g, b);

					using var brush = new SolidBrush(c);
					graphics.FillRectangle(brush, tx * TILE_PIXELS, ty * TILE_PIXELS, TILE_PIXELS, TILE_PIXELS);
				}
			}

			return bmp;
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);

			if (_area == null)
				return;

			if (e.Y < HEADER)
				return;

			int x = e.X / CELL;
			int y = (e.Y - HEADER) / CELL;

			if (x < 0 || y < 0 || x >= _area.Width || y >= _area.Height)
				return;

			var map = _area.Maps[x, y];
			if (map == null)
				return;

			// Toggle selection
			var cell = (x, y);
			if (_selectedCells.Contains(cell))
				_selectedCells.Remove(cell);
			else
				_selectedCells.Add(cell);

			Invalidate();
		}
	}
}
