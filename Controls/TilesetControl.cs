using Glyphborn.Mapper.Tiles;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

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
		private int _selectedSlot = -1;
		private int _hoverSlot = -1;


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

			using var indexBrush = new SolidBrush(Color.FromArgb(120, 120, 135));
			using var nameBrush = new SolidBrush(Color.LightGray);
			using var indexFont = new Font("Segoe UI", 6.5f);

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
					int col = i % Columns;
					int row = i / Columns;

					// Slot rect: aligned to raw grid boundaries (keeps mouse hit-testing correct)
					var slotRect = new Rectangle(col * TilePreviewSize, row * TilePreviewSize, TilePreviewSize, TilePreviewSize);

					// Draw rect: inset by padding so adjacent borders don't bleed together
					var drawRect = new Rectangle(
						slotRect.X + TilePadding,
						slotRect.Y + TilePadding,
						slotRect.Width - TilePadding * 2,
						slotRect.Height - TilePadding * 2);

					// Direct index — Tiles[i].Id == i is always guaranteed by EnsureSlots
					TileDefinition? tile = i < Tiles.Count ? Tiles[i] : null;

					bool isEmpty = IsEmptySlot(i);
					bool isAir = i == 0;

					if (!isEmpty && tile != null)
					{
						// Hover tint
						if (i == _hoverSlot && i != _selectedSlot)
						{
							using var hoverBrush = new SolidBrush(Color.FromArgb(30, 255, 255, 255));
							g.FillRectangle(hoverBrush, drawRect);
						}

						if (isAir)
						{
							using var hatch = new HatchBrush(HatchStyle.DiagonalCross,
								Color.FromArgb(50, 50, 65), Color.FromArgb(30, 30, 40));
							g.FillRectangle(hatch, drawRect);
						}
						else if (tile.Primitive?.Texture != null)
						{
							try
							{
								var thumb = TextureToBitmap(tile.Primitive.Texture);
								g.DrawImage(thumb, drawRect);
							}
							catch { g.FillRectangle(Brushes.DimGray, drawRect); }
						}
						else
						{
							// Named tile but no texture yet — red X so it's clearly incomplete
							g.FillRectangle(Brushes.Black, drawRect);
							using var xPen = new Pen(Color.FromArgb(180, 50, 50), 1.5f);
							g.DrawLine(xPen, drawRect.Left + 3, drawRect.Top + 3,
											 drawRect.Right - 3, drawRect.Bottom - 3);
							g.DrawLine(xPen, drawRect.Right - 3, drawRect.Top + 3,
											 drawRect.Left + 3, drawRect.Bottom - 3);
						}

						g.DrawRectangle(borderPen, drawRect);

						// Tile name along the bottom strip
						if (!isAir && !string.IsNullOrEmpty(tile.Name))
						{
							var nameRect = new RectangleF(drawRect.X + 2, drawRect.Bottom - 13, drawRect.Width - 4, 12);
							var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
							g.DrawString(tile.Name, indexFont, nameBrush, nameRect, fmt);
						}
					}
					else
					{
						// Named tile but no texture yet — red X so it's clearly incomplete
						g.FillRectangle(Brushes.Black, drawRect);
						using var xPen = new Pen(Color.FromArgb(180, 50, 50), 1.5f);
						g.DrawLine(xPen, drawRect.Left + 3, drawRect.Top + 3,
										 drawRect.Right - 3, drawRect.Bottom - 3);
						g.DrawLine(xPen, drawRect.Right - 3, drawRect.Top + 3,
										 drawRect.Left + 3, drawRect.Bottom - 3);
					}

					// Slot index — always visible in the top-left corner
					g.DrawString(i.ToString(), indexFont, indexBrush, drawRect.X + 2, drawRect.Y + 2);

					// Selection outline — drawn slightly outside drawRect for visual pop
					if (i == _selectedSlot)
					{
						g.DrawRectangle(selectedPen,
							drawRect.X - 1, drawRect.Y - 1,
							drawRect.Width + 2, drawRect.Height + 2);
					}
				}
			}
		}

		private bool IsEmptySlot(int slot)
		{
			if (slot == 0) return false;
			if (slot >= Tiles.Count) return true;
			var t = Tiles[slot];
			return string.IsNullOrEmpty(t.Name) && t.Primitive == null && t.MeshSourcePath == null;
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

		private static Bitmap TextureToBitmap(Texture tex)
		{
			var bmp = new Bitmap(tex.Width, tex.Height, PixelFormat.Format32bppArgb);
			for (int py = 0; py < tex.Height; py++)
				for (int px = 0; px < tex.Width; px++)
				{
					uint p = tex.Pixels[py * tex.Width + px];
					bmp.SetPixel(px, py, Color.FromArgb(
						(int)(p >> 24 & 0xFF),
						(int)(p >> 16 & 0xFF),
						(int)(p >> 8 & 0xFF),
						(int)(p & 0xFF)));
				}
			return bmp;
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