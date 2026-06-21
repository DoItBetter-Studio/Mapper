using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Damascus.Mapper.Theme;
using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;
using MsBox.Avalonia;
using System;

namespace Damascus.Mapper.Controls
{
	sealed class RoomPanel : DockPanel
	{
		private readonly EditorState _state;
		private readonly Action _onChanged;

		private RoomControl? _roomControl;
		private TextBox? _nameBox;
		private Border? _colorSwatch;
		private Button? _deleteBtn;

		private bool _suppressNameUpdate;

		public RoomPanel(EditorState state, Action onChanged)
		{
			_state = state;
			_onChanged = onChanged;

			BuildUI();
			RefreshList();
		}

		private void BuildUI()
		{
			var header = new StackPanel { Height = 60, Background = MapperTheme.HeaderBackground };
			SetDock(header, Dock.Top);

			var titlelabel = new TextBlock
			{
				Text = "Rooms",
				Height = 30,
				Padding = new Thickness(6),
				Background = MapperTheme.HeaderBackground,
				Foreground = MapperTheme.TextPrimary,
				IsHitTestVisible = false
			};

			var newBtn = new Button
			{
				Content = "+ New Room",
				Height = 30,
				Background = MapperTheme.ButtonAdd,
				Foreground = MapperTheme.TextPrimary,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				CornerRadius = new CornerRadius(0)
			};
			newBtn.Click += OnNewRoom;

			header.Children.Add(titlelabel);
			header.Children.Add(newBtn);

			_deleteBtn = new Button
			{
				Content = "Delete Room",
				Height = 30,
				Background = MapperTheme.ButtonDelete,
				Foreground = MapperTheme.TextPrimary,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				CornerRadius = new CornerRadius(0)
			};
			_deleteBtn.Click += OnDeleteRoom;

			SetDock(_deleteBtn, Dock.Bottom);

			var colorRow = new Grid
			{
				Height = 34,
				Background = MapperTheme.HeaderBackground,
				ColumnDefinitions = new ColumnDefinitions("50, *"),
				HorizontalAlignment = HorizontalAlignment.Stretch,
			};

			var colorLabel = new TextBlock
			{
				Text = "Color",
				Width = 50,
				Padding = new Thickness(6, 0, 0, 0),
				Foreground = Brushes.White,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
			};

			_colorSwatch = new Border
			{
				Background = Brushes.Transparent,
				BorderBrush = Brushes.DimGray,
				BorderThickness = new Thickness(1),
				CornerRadius = new CornerRadius(2),
				Margin = new Thickness(6, 4),
				Cursor = new Cursor(StandardCursorType.Hand),
				HorizontalAlignment = HorizontalAlignment.Stretch,
				IsEnabled = false
			};

			var flyout = new Flyout();
			var picker = new ColorView
			{
				IsAlphaVisible = false,
				IsColorSpectrumVisible = true,
				IsColorPaletteVisible = true,
				IsColorComponentsVisible = false,
			};
			picker.ColorChanged += OnColorChanged;
			flyout.Content = picker;
			_colorSwatch.ContextFlyout = flyout;
			_colorSwatch.PointerPressed += (s, e) => flyout.ShowAt(_colorSwatch);

			colorRow.Children.Add(colorLabel);
			colorRow.Children.Add(_colorSwatch);

			Grid.SetColumn(colorLabel, 0);
			Grid.SetColumn(_colorSwatch, 1);
			SetDock(colorRow, Dock.Bottom);

			var nameRow = new Grid
			{
				Height = 34,
				Background = MapperTheme.HeaderBackground,
				ColumnDefinitions = new ColumnDefinitions("50, *"),
				HorizontalAlignment = HorizontalAlignment.Stretch,
			};

			var nameLabel = new TextBlock
			{
				Text = "Name",
				Width = 50,
				Padding = new Thickness(6, 0, 0, 0),
				Foreground = Brushes.White,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
			};

			_nameBox = new TextBox
			{
				Background = MapperTheme.HeaderBackground,
				Margin = new Thickness(6, 4),
				IsEnabled = false,
				HorizontalAlignment = HorizontalAlignment.Stretch
			};
			_nameBox.TextChanged += OnNameChanged;

			nameRow.Children.Add(nameLabel);
			nameRow.Children.Add(_nameBox);

			Grid.SetColumn(nameLabel, 0);
			Grid.SetColumn(_nameBox, 1);
			SetDock(nameRow, Dock.Bottom);

			var scroll = new ScrollViewer
			{
				Background = MapperTheme.ContainerBackground,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
				VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
				VerticalAlignment = VerticalAlignment.Top
			};

			_roomControl = new RoomControl();
			_roomControl.RoomSelected += OnRoomSelected;
			scroll.Content = _roomControl;

			Children.Add(header);
			Children.Add(_deleteBtn);
			Children.Add(nameRow);
			Children.Add(colorRow);
			Children.Add(scroll);
		}

		private void RefreshList()
		{
			var rooms = _state.Area?.Rooms;
			if (rooms == null) return;

			_roomControl!.Refresh(rooms);
			_roomControl.Height = Math.Max(36, rooms.Count * 36);

			if (_state.CurrentRoomID != 0)
				_roomControl.SelectById(_state.CurrentRoomID);
		}

		private void OnRoomSelected(RoomDefinition? room)
		{
			bool has = room != null;

			_deleteBtn!.IsEnabled = has;
			_nameBox!.IsEnabled = has;
			_colorSwatch!.IsEnabled = has;

			if (!has) return;

			_state.CurrentRoomID = room!.Id;

			_suppressNameUpdate = true;
			_nameBox.Text = room.Name;
			_suppressNameUpdate = false;

			_colorSwatch.Background = new SolidColorBrush(room.Color);

			_onChanged?.Invoke();
		}


		private void OnNameChanged(object? sender, TextChangedEventArgs e)
		{
			if (_suppressNameUpdate) return;

			var room = _state.Area?.GetRoom(_state.CurrentRoomID);
			if (room == null) return;

			room.Name = _nameBox!.Text!;
			_roomControl!.Refresh(_state.Area!.Rooms);
			_onChanged?.Invoke();
		}

		private void OnColorChanged(object? sender, ColorChangedEventArgs e)
		{
			var room = _state.Area?.GetRoom(_state.CurrentRoomID);
			if (room == null) return;

			room.Color = e.NewColor;

			_colorSwatch!.Background = new SolidColorBrush(e.NewColor);

			_roomControl!.Refresh(_state.Area!.Rooms);
			_onChanged?.Invoke();
		}

		private void OnNewRoom(object? sender, RoutedEventArgs e)
		{
			if (_state.Area == null) return;

			var room = _state.Area.CreateRoom();
			_state.CurrentRoomID = room.Id;

			RefreshList();
			_roomControl!.SelectById(room.Id);
			_onChanged?.Invoke();

			_nameBox!.Focus();
			_nameBox.SelectAll();
		}

		private async void OnDeleteRoom(object? sender, RoutedEventArgs e)
		{
			var room = _state.Area?.GetRoom(_state.CurrentRoomID);
			if (room == null) return;

			// 1. Capture the result from the async task
			var result = await MessageBoxManager.GetMessageBoxStandard(
				$"Delete \"{room.Name}\" (#{room.Id})?\n\nTiles painted with this room ID will remain tagged but the room will no longer be named.",
				"Delete Room",
				MsBox.Avalonia.Enums.ButtonEnum.YesNoCancel).ShowAsync();

			// 2. Check the result enum
			if (result != MsBox.Avalonia.Enums.ButtonResult.Yes) return;

			// 3. Proceed with deletion
			_state.Area!.DeleteRoom(_state.CurrentRoomID);
			_state.CurrentRoomID = 0;

			// 4. Reset UI state
			_nameBox!.Text = string.Empty;
			_nameBox.IsEnabled = false;
			_colorSwatch!.Background = MapperTheme.WindowBackground;
			_colorSwatch.IsEnabled = false;
			_deleteBtn!.IsEnabled = false;

			RefreshList();
			_onChanged?.Invoke();
		}
	}
}
