using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Damascus.Mapper.Theme;
using Glyphborn.Mapper;
using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;

namespace Damascus.Mapper.Controls
{
	sealed class TilesetPanel : DockPanel
	{
		private string _name;
		private byte _tilesetIndex;
		private Tileset _tileset;
		private EditorState _state;
		private TilesetControl? _tilesetControl;

		public TilesetPanel(string name, byte tilesetIndex, Tileset tileset, EditorState state)
		{
			_name = name;
			_tilesetIndex = tilesetIndex;
			_tileset = tileset;
			_state = state;

			Background = MapperTheme.ContainerBackground;

			LastChildFill = true;

			BuildUI();
		}

		private void BuildUI()
		{
			var header = new StackPanel { Height = 60, Background = MapperTheme.HeaderBackground, };
			SetDock(header, Dock.Top);

			var titlelabel = new TextBlock
			{
				Text = _name,
				Height = 30,
				Padding = new Thickness(6),
				Background = MapperTheme.HeaderBackground,
				Foreground = MapperTheme.TextPrimary,
				IsHitTestVisible = false
			};

			var editButton = new Button
			{
				Content = "Edit Tileset",
				Height = 30,
				Background = MapperTheme.ButtonHighlight,
				Foreground = MapperTheme.TextPrimary,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				CornerRadius = new CornerRadius(0),
				Cursor = new Cursor(Avalonia.Input.StandardCursorType.Hand),
			};
			editButton.Click += (_, __) => OpenTilesetEditor();

			header.Children.Add(titlelabel);
			header.Children.Add(editButton);

			var scroll = new ScrollViewer
			{
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
				VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
			};

			_tilesetControl = new TilesetControl
			{
				Tiles = _tileset.Tiles.ToArray(),
				TilesetIndex = _tilesetIndex,
				Width = 266,
				VerticalAlignment = VerticalAlignment.Top
			};

			_tilesetControl.TileSelected += sel => _state.SelectedTile = sel;

			_tilesetControl.Height = _tilesetControl.GetRequiredHeight();
			_tilesetControl.InvalidateVisual();

			scroll.Content = _tilesetControl;

			Children.Add(header);
			Children.Add(scroll);
		}

		private void OpenTilesetEditor()
		{
			var dialog = new TilesetEditorDialog(_tileset);
			var mainWindow = this.FindAncestorOfType<Window>();

			// Pass the dialog result directly
			dialog.ShowDialog(mainWindow!).ContinueWith(t =>
			{
				// Use Avalonia's UI thread dispatcher to ensure 
				// the control updates on the correct thread
				Avalonia.Threading.Dispatcher.UIThread.Post(() => {
					if (t.IsCompletedSuccessfully == true) // Assuming 'true' means saved
					{
						// Trigger the setter, which triggers InvalidateVisual and InvalidateMeasure
						_tilesetControl!.Tiles = _tileset.Tiles.ToArray();
						_tilesetControl.Height = _tilesetControl.GetRequiredHeight();
						_tilesetControl.InvalidateVisual();
					}
				});
			});
		}
	}
}
