using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;

namespace Glyphborn.Mapper.Controls
{
	public sealed class TilesetControl : Control
	{
		public IReadOnlyList<TileDefinition> Tiles = Array.Empty<TileDefinition>();
		public byte TilesetIndex;

		public int TilePreviewSize = 32;
		public int TilePadding = 2; // Prevents adjacent dashed lines from blending into solid lines
		public int Columns = 8; // Default columns when no tiles are present, matching the Editor Dialog's default layout

		public TileSelection? SelectedTile;

		public event Action<TileSelection>? TileSelected;

		public TilesetControl()
		{
			// Stops the sidebar from flickering when scrolling or selecting tiles
			DoubleBuffered = true;
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);

			int col = e.X / TilePreviewSize;
			int row = e.Y / TilePreviewSize;

			// Guard against clicks on the right edge spilling past the last column
			if (col >= Columns || col < 0 || row < 0) return;

			int index = row * Columns + col;

			TileDefinition? clickedTile = null;
			foreach (var t in Tiles)
			{
				if (t.Id == index) { clickedTile = t; break; }
			}

			if (clickedTile != null)
			{
				SelectedTile = new TileSelection(TilesetIndex, (ushort)index, clickedTile);
				TileSelected?.Invoke(SelectedTile.Value);
				Invalidate();
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			var g = e.Graphics;
			g.Clear(BackColor);

			int maxTileId = 0;
			foreach (var t in Tiles)
			{
				if (t.Id > maxTileId) maxTileId = t.Id;
			}

			// Use 256 to match the baseline established by GetRequiredHeight()
			int totalSlots = Math.Max(256, maxTileId + 1);

			int rows = (int)Math.Ceiling(totalSlots / (float)Columns);
			totalSlots = rows * Columns;

			using (var selectedPen = new Pen(Color.FromArgb(14, 116, 202), 2))
			using (var emptyPen = new Pen(Color.FromArgb(55, 55, 58), 1) { DashStyle = DashStyle.Dash })
			using (var borderPen = new Pen(Color.FromArgb(45, 45, 48), 1))
			using (var font = new Font("Segoe UI", 7f))
			using (var plusBrush = new SolidBrush(Color.FromArgb(110, 110, 115)))
			{
				for (int i = 0; i < totalSlots; i++)
				{
					int row = i / Columns;
					int col = i % Columns;

					// Compute base coordinate slot (keeps mouse input perfectly aligned on 32px boundaries)
					var slotRect = new Rectangle(col * TilePreviewSize, row * TilePreviewSize, TilePreviewSize, TilePreviewSize);

					// Inset the rendering layout rectangle by the padding constraint so borders don't bleed together
					var drawRect = new Rectangle(
						slotRect.X + TilePadding,
						slotRect.Y + TilePadding,
						slotRect.Width - (TilePadding * 2),
						slotRect.Height - (TilePadding * 2)
					);

					// Match tile by its absolute ID slot key, exactly like the Editor Dialog does
					TileDefinition? tile = null;
					foreach (var t in Tiles)
					{
						if (t.Id == i)
						{
							tile = t;
							break;
						}
					}

					if (tile != null)
					{
						// Render tile asset cleanly inside its padded cell frame
						if (tile.Primitive != null)
						{
							try
							{
								var thumb = TilePreviewer.GetThumbnail(tile.Primitive.Texture, drawRect.Width, drawRect.Height);
								g.DrawImage(thumb, drawRect);
							}
							catch
							{
								g.FillRectangle(Brushes.DimGray, drawRect);
							}
						}
						else
						{
							g.FillRectangle(Brushes.Black, drawRect);
						}

						g.DrawRectangle(borderPen, drawRect);
					}
					else
					{
						// Render clean, individual empty cells where the dashes remain visible
						g.DrawRectangle(emptyPen, drawRect);

						g.DrawString("+", font, plusBrush,
							drawRect.X + (drawRect.Width / 2) - 4,
							drawRect.Y + (drawRect.Height / 2) - 6);
					}

					// Draw selection overlay bounding wrapper box slightly outside the draw bounds for visual pop
					if (SelectedTile is TileSelection sel &&
						sel.TilesetIndex == TilesetIndex &&
						sel.TileIndex == i)
					{
						g.DrawRectangle(selectedPen, drawRect.X - 1, drawRect.Y - 1, drawRect.Width + 2, drawRect.Height + 2);
					}
				}
			}
		}

		public int GetRequiredHeight()
		{
			int maxTileId = 0;
			foreach (var t in Tiles)
			{
				if (t.Id > maxTileId) maxTileId = t.Id;
			}

			int totalSlots = Math.Max(256, maxTileId + 1);
			int rows = (int)Math.Ceiling(totalSlots / (float)Columns);

			return rows * TilePreviewSize;
		}
	}

	public readonly struct TileSelection
	{
		public readonly byte TilesetIndex;
		public readonly ushort TileIndex;
		public readonly TileDefinition Tile;

		public TileSelection(byte tilesetIndex, ushort tileIndex, TileDefinition tile)
		{
			TilesetIndex = tilesetIndex;
			TileIndex = tileIndex;
			Tile = tile;
		}
	}
}