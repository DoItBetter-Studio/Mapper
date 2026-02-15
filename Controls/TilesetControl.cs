using System;
using System.Collections.Generic;
using System.Drawing;
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
		public int TilePadding = 4;

		public TileSelection? SelectedTile;

		public event Action<TileSelection>? TileSelected;

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);

			int cols = Math.Max(1, Width / TilePreviewSize);

			int col = e.X / TilePreviewSize;
			int row = e.Y / TilePreviewSize;
			int index = row * cols + col;

			if (index >= 0 && index < Tiles.Count)
			{
				SelectedTile = new TileSelection(
					TilesetIndex,
					(ushort) index,
					Tiles[index]
				);

				TileSelected?.Invoke(SelectedTile.Value);
				Invalidate();
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			var g = e.Graphics;
			g.Clear(BackColor);

			int cols = Math.Max(1, Width / TilePreviewSize);
			int y = TilePadding;
			int x = TilePadding;
			int col = 0;

			for (int i = 0; i < Tiles.Count; i++)
			{
				DrawTilePreview(g, Tiles[i], x, y);

				if (SelectedTile is TileSelection sel &&
					sel.TilesetIndex == TilesetIndex &&
					sel.TileIndex == i)
				{
					DrawSelection(g, x, y);
				}

				x += TilePreviewSize;
				col++;

				if (col >= cols)
				{
					col = 0;
					x = TilePadding;
					y += TilePreviewSize;
				}
			}
		}

		private void DrawTilePreview(Graphics g, TileDefinition tile, int x, int y)
		{
			var rect = new Rectangle(x, y, TilePreviewSize, TilePreviewSize);

			if (tile.Primitive != null)
			{
				var thumb = TilePreviewer.GetThumbnail(tile.Primitive.Texture, TilePreviewSize, TilePreviewSize);
				g.DrawImage(thumb, rect);
			}
			else
			{
				g.FillRectangle(Brushes.Black, rect);
			}

			g.DrawRectangle(Pens.DimGray, rect);
		}

		private void DrawSelection(Graphics g, int x, int y)
		{
			var rect = new Rectangle(x, y, TilePreviewSize, TilePreviewSize);
			g.DrawRectangle(Pens.Green, rect);
		}

		public int GetRequiredHeight()
		{
			if (Tiles.Count == 0)
				return TilePreviewSize + TilePadding * 2;

			int cols = Math.Max(1, Width / TilePreviewSize);
			int rows = (int) Math.Ceiling(Tiles.Count / (float) cols);

			return TilePadding * 2 + rows * TilePreviewSize;
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
