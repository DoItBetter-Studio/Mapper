using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Damascus.Mapper.Controls
{
	sealed class GhostAreaSelectControl : Control
	{
		private AreaDocument? _area;
		private HashSet<(int x, int y)> _selectedCells = new();

		private const int CELL = 64;
		private const int HEADER = 30;

		private static FormattedText GenerationFailed = new FormattedText("!", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 16, Brushes.White);
		private static FormattedText GenerationError = new FormattedText("X", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 16, Brushes.White);
		private FormattedText AreaSize = new FormattedText($"Area: 0×0", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 16, Brushes.White);

		public void SetArea(AreaDocument area)
		{
			_area = area;
			_selectedCells.Clear();
			Width = area.Width * CELL;
			Height = area.Height * CELL + HEADER;

			HorizontalAlignment = HorizontalAlignment.Left;
			VerticalAlignment = VerticalAlignment.Top;

			InvalidateVisual();
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

		public override void Render(DrawingContext context)
		{
			base.Render(context);

			if (_area == null)
				return;

			context.DrawText(AreaSize, new Point(5, 5));

			for (int y = 0; y < _area.Height; y++)
			{
				for (int x = 0; x < _area.Width; x++)
				{
					var rect = new Rect(x * CELL, y * CELL + HEADER, CELL, CELL);

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
								context.DrawImage(
									map.MiniPreview,
									rect);
							}
							else
							{
								context.FillRectangle(Brushes.DarkRed, rect);
								context.DrawText(GenerationFailed, new Point(rect.X + 20, rect.Y + 20));
							}
						}
						catch
						{
							context.FillRectangle(Brushes.DarkRed, rect);
							context.DrawText(GenerationError, new Point(rect.X + 20, rect.Y + 20));
						}
					}
					else
					{
						continue;
					}

					context.DrawRectangle(null, new Pen(Brushes.DimGray), rect);

					if (_selectedCells.Contains((x,y)))
						context.DrawRectangle(null, new Pen(Brushes.DodgerBlue, 3), rect);
				}
			}
		}

		public static WriteableBitmap BuildMiniPreview(MapDocument map, AreaDocument area)
		{
			const int TILE_PIXELS = 2;
			const int SIZE = MapDocument.WIDTH * TILE_PIXELS;

			var bmp = new WriteableBitmap(
				new PixelSize(SIZE, SIZE),
				new Vector(96, 96),
				Avalonia.Platform.PixelFormat.Bgra8888,
				Avalonia.Platform.AlphaFormat.Premul);

			using var fb = bmp.Lock();
			unsafe
			{
				var ptr = (uint*)fb.Address;

				// Fill black
				for (int i = 0; i < SIZE * SIZE; i++)
					ptr[i] = 0xFF000000;

				for (int ty = 0; ty < MapDocument.HEIGHT; ty++)
				{
					for (int tx = 0; tx < MapDocument.WIDTH; tx++)
					{
						Texture? targetTex = null;

						for (int l = MapDocument.LAYERS - 1; l >= 0; l--)
						{
							var t = map.Tiles[l][ty][tx];
							if (t.Tileset >= area.Tilesets.Count) continue;
							var ts = area.Tilesets[t.Tileset];
							if (t.TileId >= ts.Tiles.Count) continue;
							var def = ts.Tiles[t.TileId];
							if (def.TileType == TileType.None) continue;
							var firstPrimitive = def.GetPrimitives().FirstOrDefault();
							if (firstPrimitive?.Texture == null) continue;
							targetTex = firstPrimitive.Texture;
							break;
						}

						if (targetTex == null) continue;

						int sx = targetTex.Width / 2;
						int sy = targetTex.Height / 2;
						uint raw = targetTex.Pixels[sy * targetTex.Width + sx];

						byte a = (byte)(raw >> 24);
						byte r = (byte)(raw >> 16);
						byte g = (byte)(raw >> 8);
						byte b = (byte)(raw);

						// Premultiply for Avalonia's Bgra8888 Premul format
						float af = a / 255f;
						byte pr = (byte)(r * af);
						byte pg = (byte)(g * af);
						byte pb = (byte)(b * af);

						uint pixel =
							  pb
							| ((uint)pg << 8)
							| ((uint)pr << 16)
							| ((uint)a << 24);

						for (int py = 0; py < TILE_PIXELS; py++)
							for (int px = 0; px < TILE_PIXELS; px++)
								ptr[(ty * TILE_PIXELS + py) * SIZE + (tx * TILE_PIXELS + px)] = pixel;
					}
				}
			}

			return bmp;
		}

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);

			if (_area == null)
				return;

			var pos = e.GetPosition(this);

			if (pos.Y < HEADER) return;

			int x = (int)(pos.X / CELL);
			int y = (int)((pos.Y - HEADER) / CELL);

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

			InvalidateVisual();
		}
	}
}
