using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Damascus.Mapper.Theme;
using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Damascus.Mapper.Controls
{
	public sealed class TilesetControl : Control
	{
		public IReadOnlyList<TileDefinition> Tiles = Array.Empty<TileDefinition>();
		public byte TilesetIndex;

		public int TilePreviewSize = 32;
		public int TilePadding = 2;
		public int Columns = 8;

		public TileSelection? SelectedTile;

		public event Action<TileSelection>? TileSelected;
		private int _selectedSlot = -1;
		private int _hoverSlot = -1;

		private static readonly IPen SelectedPen = new Pen(Brush.Parse("#0E74CA"), 2);
		private static readonly IPen EmptyPen = new Pen(Brush.Parse("#37373A"), 1, dashStyle: DashStyle.DashDot);

		private static readonly Typeface TileTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);

		private static readonly FormattedText addText = new FormattedText("+", System.Globalization.CultureInfo.CurrentCulture,
								   FlowDirection.LeftToRight, Typeface.Default, 10, Brushes.LightGray);

		private int TotalSlots
		{
			get
			{
				int maxId = 0;
				foreach (var t in Tiles)
					if (t.Id > maxId) maxId = t.Id;

				int total = Math.Max(Tiles.Count, maxId + 32);
				int rows = (int)Math.Ceiling(total / (float)Columns);
				return rows * Columns;
			}
		}

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);

			var point = e.GetCurrentPoint(this);

			int col = (int)point.Position.X / TilePreviewSize;
			int row = (int)point.Position.Y / TilePreviewSize;

			if (col >= Columns || col < 0 || row < 0) return;

			int index = row * Columns + col;

			TileDefinition? clickedTile = null;
			foreach (var t in Tiles)
			{
				if (t.Id == index)
				{
					clickedTile = t;
					break;
				}
			}

			if (clickedTile != null && (clickedTile.GetPrimitives().FirstOrDefault() != null || clickedTile.Id == 0))
			{
				SelectedTile = new TileSelection(TilesetIndex, (ushort)index, clickedTile);
				TileSelected?.Invoke(SelectedTile.Value);
				InvalidateVisual();
			}
		}

		public override void Render(DrawingContext context)
		{
			base.Render(context);

			for (int i = 0; i < TotalSlots; i++)
			{
				int col = i % Columns;
				int row = i / Columns;

				var slotRect = new Rect(col * TilePreviewSize, row * TilePreviewSize, TilePreviewSize, TilePreviewSize);
				var drawRect = slotRect.Deflate(TilePadding);

				TileDefinition? tile = i < Tiles.Count ? Tiles[i] : null;
				bool isEmpty = IsEmptySlot(i);

				if (!isEmpty && tile != null)
				{
					if (i == _hoverSlot && i != _selectedSlot)
						context.FillRectangle(new SolidColorBrush(Avalonia.Media.Colors.White, 0.12), drawRect);

					if (i == 0 || tile.TileType == TileType.None)
					{
						context.FillRectangle(MapperTheme.ContainerBackground, drawRect);
					}
					else
					{
						var primitive = tile.GetPrimitives().FirstOrDefault();
						if (primitive?.Texture != null)
						{
							var thumb = TextureToWriteableBitmap(primitive.Texture);
							context.DrawImage(thumb, drawRect);
						}
					}

					context.DrawRectangle(null, new Pen(Brushes.DimGray), drawRect);

					if (!string.IsNullOrEmpty(tile.Name))
					{
						var text = new FormattedText(
							tile.Name,
							System.Globalization.CultureInfo.CurrentCulture,
							FlowDirection.LeftToRight,
							TileTypeface,
							9.0, // Smaller font size often fits better in 32x32 tiles
							Brushes.LightGray)
						{
							MaxTextWidth = drawRect.Width - 4,
							Trimming = TextTrimming.CharacterEllipsis
						};
						context.DrawText(text, new Point(drawRect.X + 2, drawRect.Bottom - 12));
					}
				}
				else
				{
					context.DrawRectangle(null, EmptyPen, drawRect);

					var centerX = drawRect.X + (drawRect.Width / 2);
					var centerY = drawRect.Y + (drawRect.Height / 2);
					context.DrawText(addText, new Point(centerX + 4, centerY));
				}

				var index = new FormattedText(i.ToString(), System.Globalization.CultureInfo.CurrentCulture,
						   FlowDirection.LeftToRight, Typeface.Default, 10, Brushes.LightGray);
				context.DrawText(index, new Point(drawRect.X + 2, drawRect.Y + 2));

				if (i == _selectedSlot)
				{
					context.DrawRectangle(null, SelectedPen, drawRect.Inflate(1));
				}
			}
		}

		private bool IsEmptySlot(int slot)
		{
			if (slot == 0) return false;
			if (slot >= Tiles.Count) return true;
			var t = Tiles[slot];
			return string.IsNullOrEmpty(t.Name) && t.TileType == TileType.None;
		}

		public int GetRequiredHeight()
		{
			int maxTileId = 0;
			foreach (var t in Tiles)
			{
				if (t.Id > maxTileId) maxTileId = t.Id;
			}

			int total = Math.Max(Tiles.Count, maxTileId + 32);
			int rows = (int)Math.Ceiling(total / (float)Columns);

			return rows * TilePreviewSize;
		}

		private static WriteableBitmap TextureToWriteableBitmap(Texture tex)
		{
			// 1. Create the WriteableBitmap
			var bitmap = new WriteableBitmap(
				new PixelSize(tex.Width, tex.Height),
				new Vector(96, 96), // Standard DPI
				PixelFormat.Bgra8888); // Avalonia's standard format

			// 2. Lock the bitmap to get direct access to the back buffer
			using (var frame = bitmap.Lock())
			{
				unsafe
				{
					// Get a pointer to the start of the memory
					uint* backBuffer = (uint*)frame.Address;
					int stride = frame.RowBytes;

					for (int py = 0; py < tex.Height; py++)
					{
						for (int px = 0; px < tex.Width; px++)
						{
							uint p = tex.Pixels[py * tex.Width + px];

							// Extract components
							byte a = (byte)(p >> 24);
							byte r = (byte)(p >> 16);
							byte g = (byte)(p >> 8);
							byte b = (byte)(p);

							// Reconstruct into BGRA format (B | G << 8 | R << 16 | A << 24)
							// This puts the color data in the order Avalonia expects (Little Endian)
							backBuffer[py * (stride / 4) + px] = (uint)(b | (g << 8) | (r << 16) | (a << 24));
						}
					}
				}
			}

			return bitmap;
		}
	}
}
