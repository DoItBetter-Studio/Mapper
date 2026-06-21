using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Damascus.Mapper.Theme;
using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;
using System;
using System.Globalization;
using System.Linq;

namespace Damascus.Mapper.Controls;

public partial class AreaControl : Control
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
	public const int HEADER = 30;

	private static FormattedText GenerationFailed = new FormattedText("!", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 16, Brushes.White);
	private static FormattedText GenerationError = new FormattedText("X", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 16, Brushes.White);
	private FormattedText AreaSize = new FormattedText($"Area: 0×0", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 16, Brushes.White);

	public AreaControl() { HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left; VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top; }

	public void SetArea(AreaDocument area)
	{
		Area = area;

		Width = area.Width * CELL + 4;
		Height = area.Height * CELL + HEADER + 4;

		InvalidateVisual();
	}

	public override void Render(DrawingContext context)
	{
		base.Render(context);

		context.DrawText(AreaSize, new Point(5, 5));

		if (Area == null) return;

		for (int y = 0; y < Area.Height; y++)
		{
			for (int x = 0; x < Area.Width; x++)
			{
				var rect = new Rect(x * CELL, y * CELL + HEADER, CELL, CELL);

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

				if (State != null && x == State.ActiveMapX && y == State.ActiveMapY)
					context.DrawRectangle(null, new Pen(Brushes.DodgerBlue, 3), rect);
			}
		}
	}

	protected override void OnPointerPressed(PointerPressedEventArgs e)
	{
		base.OnPointerPressed(e);

		if (Area == null) return;

		var pos = e.GetPosition(this);

		if (pos.Y < HEADER) return;

		int x = (int)(pos.X / CELL);
		int y = (int)((pos.Y - HEADER) / CELL);

		if (x < 0 || y < 0 || x >= Area.Width || y >= Area.Height) return;

		var map = Area.Maps[x, y];

		if (map == null) return;

		if (State == null) return;

		State.ActiveMapX = x;
		State.ActiveMapY = y;

		MapSelected?.Invoke(map);

		InvalidateVisual();
	}

	private void Area_Changed()
	{
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(Area_Changed);
			return;
		}

		if (Area == null)
			return;

		Width = Area.Width * CELL + 4;
		Height = Area.Height * CELL + HEADER + 4;

		AreaSize = new FormattedText($"Area: {Area.Width}×{Area.Height}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 9, Brushes.White);

		InvalidateVisual();
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

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
	{
		if (Area != null)
			Area.Changed -= Area_Changed;

		base.OnDetachedFromVisualTree(e);
	}
}