using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Damascus.Mapper.Theme;
using Glyphborn.Mapper.Tiles;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Damascus.Mapper.Controls
{
	sealed class RoomControl : Control
	{
		private List<RoomDefinition> _rooms = new();
		private int _selectedIndex = -1;

		private const int ROW_HEIGHT = 36;
		private const int SWATCH_SIZE = 18;
		private const int SWATCH_MARGIN = 8;

		public event Action<RoomDefinition?>? RoomSelected;

		private static readonly IBrush ActiveRoom = new SolidColorBrush(Color.FromRgb(50, 100, 200));
		private static readonly IBrush OddRooms = new SolidColorBrush(Color.FromRgb(35, 35, 35));
		private static readonly IBrush EvenRooms = new SolidColorBrush(Color.FromRgb(28, 28, 28));

		public RoomDefinition? SelectedRoom =>
			_selectedIndex >= 0 && _selectedIndex < _rooms.Count
				? _rooms[_selectedIndex]
				: null;

		public RoomControl() { }

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
			InvalidateVisual();
		}

		public void SelectById(uint id)
		{
			for (int i = 0; i < _rooms.Count; i++)
			{
				if (_rooms[i].Id == id)
				{
					_selectedIndex = i;
					InvalidateVisual();
					RoomSelected?.Invoke(_rooms[i]);
					return;
				}
			}
		}

		private void UpdateHeight()
		{
			Height = Math.Max(ROW_HEIGHT, _rooms.Count * ROW_HEIGHT);
		}

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);

			var point = e.GetCurrentPoint(this);

			int index = (int)point.Position.Y / ROW_HEIGHT;
			if (index < 0 || index >= _rooms.Count)
				return;

			_selectedIndex = index;
			InvalidateVisual();
			RoomSelected?.Invoke(_rooms[index]);
		}

		public override void Render(DrawingContext context)
		{
			base.Render(context);

			for (int i = 0; i < _rooms.Count; i++)
			{
				DrawRow(context, i);
			}
		}

		private void DrawRow(DrawingContext context, int i)
		{
			var room = _rooms[i];
			var rowRect = new Rect(0, i * ROW_HEIGHT, Bounds.Width, ROW_HEIGHT);

			IBrush bgColor = i == _selectedIndex
				? ActiveRoom
				: i % 2 == 0
					? EvenRooms
					: OddRooms;

			context.FillRectangle(bgColor, rowRect);

			var swatchRect = new Rect(
				SWATCH_MARGIN,
				i * ROW_HEIGHT + (ROW_HEIGHT - SWATCH_SIZE) / 2,
				SWATCH_SIZE,
				SWATCH_SIZE);

			context.FillRectangle(new SolidColorBrush(room.Color), swatchRect);

			context.DrawRectangle(new Pen(Brushes.Black, 1), swatchRect);

			int textX = (int)swatchRect.Right + SWATCH_MARGIN;
			int textY = i * ROW_HEIGHT + (ROW_HEIGHT / 2) - 7;

			var nameLabel = new FormattedText(room.Name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 12, MapperTheme.TextPrimary);
			context.DrawText(nameLabel, new Point(textX, textY));

			var idLabel = new FormattedText($"#{room.Id}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 12, MapperTheme.TextPrimary);

			int idX = (int)Bounds.Width - (int)idLabel.Width - SWATCH_MARGIN;
			int idY = i * ROW_HEIGHT + (ROW_HEIGHT / 2) - 7;

			context.DrawText(idLabel, new Point(idX, idY));

			context.DrawLine(new Pen(Brushes.White, 1), new Point(0, rowRect.Bottom - 1), new Point(Bounds.Width, rowRect.Bottom - 1));
		}
	}
}
