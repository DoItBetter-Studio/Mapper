using Glyphborn.Mapper.Tiles;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Glyphborn.Mapper.Controls
{
	public sealed class RoomControl : Control
	{
		private List<RoomDefinition> _rooms = new();
		private int _selectedIndex = -1;

		private const int ROW_HEIGHT = 36;
		private const int SWATCH_SIZE = 18;
		private const int SWATCH_MARGIN = 8;

		public event Action<RoomDefinition?>? RoomSelected;

		public RoomDefinition? SelectedRoom =>
			_selectedIndex >= 0 && _selectedIndex < _rooms.Count
				? _rooms[_selectedIndex]
				: null;

		public RoomControl()
		{
			DoubleBuffered = true;
			BackColor = Color.FromArgb(30, 30, 30);
			ForeColor = Color.White;
		}

		public void Refresh(List<RoomDefinition> rooms)
		{
			// Preserve selection across refreshes if the room still exists
			uint? selectedId = SelectedRoom?.Id;
			_rooms = rooms;
			_selectedIndex = -1;

			if (selectedId.HasValue)
			{
				for (int i = 0; i < _rooms.Count; i++)
				{
					if (_rooms[i].Id == selectedId.Value)
					{
						_selectedIndex = i;
						break;
					}
				}
			}

			UpdateHeight();
			Invalidate();
		}

		public void SelectById(uint id)
		{
			for (int i = 0; i < _rooms.Count; i++)
			{
				if (_rooms[i].Id == id)
				{
					_selectedIndex = i;
					Invalidate();
					RoomSelected?.Invoke(_rooms[i]);
					return;
				}
			}
		}

		private void UpdateHeight()
		{
			Height = Math.Max(ROW_HEIGHT, _rooms.Count * ROW_HEIGHT);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);

			int index = e.Y / ROW_HEIGHT;
			if (index < 0 || index >= _rooms.Count)
				return;

			_selectedIndex = index;
			Invalidate();
			RoomSelected?.Invoke(_rooms[index]);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			var g = e.Graphics;
			g.Clear(BackColor);

			for (int i = 0; i < _rooms.Count; i++)
			{
				DrawRow(g, i);
			}
		}

		private void DrawRow(Graphics g, int i)
		{
			var room = _rooms[i];
			var rowRect = new Rectangle(0, i * ROW_HEIGHT, Width, ROW_HEIGHT);

			// Row background
			Color bgColor = i == _selectedIndex
				? Color.FromArgb(50, 100, 200)
				: i % 2 == 0
					? Color.FromArgb(35, 35, 35)
					: Color.FromArgb(28, 28, 28);

			using (var bg = new SolidBrush(bgColor))
				g.FillRectangle(bg, rowRect);

			// Color swatch
			var swatchRect = new Rectangle(
				SWATCH_MARGIN,
				i * ROW_HEIGHT + (ROW_HEIGHT - SWATCH_SIZE) / 2,
				SWATCH_SIZE,
				SWATCH_SIZE);

			using (var swatchBrush = new SolidBrush(room.Color))
				g.FillRectangle(swatchBrush, swatchRect);

			g.DrawRectangle(Pens.DimGray, swatchRect);

			// Room name
			int textX = swatchRect.Right + SWATCH_MARGIN;
			int textY = i * ROW_HEIGHT + (ROW_HEIGHT / 2) - 7;

			using var nameFont = new Font("Segoe UI", 9f, FontStyle.Regular);
			g.DrawString(room.Name, nameFont, Brushes.White, textX, textY);

			// Room ID — right-aligned, muted
			using var idFont = new Font("Segoe UI", 8f, FontStyle.Regular);
			using var idBrush = new SolidBrush(Color.FromArgb(140, 140, 140));

			string idStr = $"#{room.Id}";
			var idSize = g.MeasureString(idStr, idFont);

			g.DrawString(
				idStr,
				idFont,
				idBrush,
				Width - idSize.Width - SWATCH_MARGIN,
				i * ROW_HEIGHT + (ROW_HEIGHT - (int)idSize.Height) / 2);

			// Row separator
			using var sep = new Pen(Color.FromArgb(45, 45, 45));
			g.DrawLine(sep, 0, rowRect.Bottom - 1, Width, rowRect.Bottom - 1);
		}
	}
}