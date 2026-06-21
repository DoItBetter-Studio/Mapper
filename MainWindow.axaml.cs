using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Damascus.Mapper.Controls;
using Damascus.Mapper.Dialogs;
using Damascus.Mapper.Theme;
using Glyphborn.Mapper;
using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using System;
using System.Linq;

namespace Damascus.Mapper
{
	public partial class MainWindow : Window
	{
		private AreaDocument? _areaDocument;
		private MapDocument? _activeMap;
		private EditorState _editorState = new();
		private MapCanvasControl? _mapCanvasControl;
		private Grid? _tilesetColumn;
		private AreaControl? _areaControl;
		private bool _forceClose = false;

		private Grid? _mainRoot;
		private Border? _titleBar;
		private Menu? _titleBarMenu;
		private Grid? _content;
		private TextBlock? _titleBlock;

		private static Thickness OutterPanelMargin = new Thickness(4);
		private static Thickness InnerPanelMargin = new Thickness(0, 4);

		private MenuItem? _newMapItem;
		private MenuItem? _openMapItem;
		private MenuItem? _saveMapItem;
		private MenuItem? _saveAsMapItem;
		private MenuItem? _importMapItem;
		private MenuItem? _exportMapItem;
		private MenuItem? _undoItem;
		private MenuItem? _redoItem;
		private MenuItem? _fillItem;
		private MenuItem? _clearItem;
		private MenuItem? _showItem;
		private MenuItem? _viewItem;
		private MenuItem? _aboutItem;
		private MenuItem? _shortItem;

		public MainWindow()
		{
			// Apply Window Chrome settings
			this.CanResize = true;
			this.WindowDecorations = WindowDecorations.BorderOnly;
			this.ExtendClientAreaToDecorationsHint = true;

			// Define the layout
			_mainRoot = new Grid
			{
				RowDefinitions = new RowDefinitions("32, *")
			};

			// Create Chrome
			InitializeTitleBar();

			// Create Content
			InitializeUI();

			_mainRoot.Children.Add(_titleBar!);
			_mainRoot.Children.Add(_content!);

			Grid.SetRow(_titleBar!, 0);
			Grid.SetRow(_content!, 1);

			this.Content = _mainRoot;
		}

		private void InitializeTitleBar()
		{
			var platformSettings = Application.Current?.PlatformSettings;
			var accentColor = platformSettings?.GetColorValues().AccentColor1 ?? Color.Parse("#D2D2D2");
			var accentBrush = new SolidColorBrush(accentColor);

			_titleBar = new Border { Background = accentBrush };

			var chromeLayout = new Grid();
			chromeLayout.ColumnDefinitions.Add(new ColumnDefinition(28, GridUnitType.Pixel));  // Icon
			chromeLayout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));          // Menu
			chromeLayout.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));     // Title
			chromeLayout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));          // Window controls

			// Title spans all columns so it centers across the full bar
			var titleArea = new Border
			{
				Background = Brushes.Transparent,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
			};

			_titleBlock = new TextBlock
			{
				Text = "Damascus Mapper",
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				IsHitTestVisible = false
			};

			titleArea.Child = _titleBlock;

			titleArea.PointerPressed += (_, e) =>
			{
				if (e.GetCurrentPoint(titleArea).Properties.IsLeftButtonPressed)
					BeginMoveDrag(e);
			};

			WindowDecorationProperties.SetElementRole(titleArea, WindowDecorationsElementRole.TitleBar);

			Grid.SetColumn(titleArea, 0);
			Grid.SetColumnSpan(titleArea, 4);

			// Title goes in first — icon, menu, and controls render on top in z-order
			chromeLayout.Children.Add(titleArea);

			// Icon
			var icon = new Image
			{
				Source = MapperTheme.WindowBitmap,
				Width = 28,
				Height = 28,
				Margin = new Thickness(4, 0, 4, 0),
				VerticalAlignment = VerticalAlignment.Center
			};

			// Taskbar icon
			Icon = MapperTheme.Icon;

			Grid.SetColumn(icon, 0);
			chromeLayout.Children.Add(icon);

			// Menu
			InitializeMenu();
			Grid.SetColumn(_titleBarMenu!, 1);
			chromeLayout.Children.Add(_titleBarMenu!);

			// Window controls
			var controls = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
			Grid.SetColumn(controls, 3);

			var minBtn = new Button
			{
				Content = "🗕",
				Width = 40,
				Background = Brushes.Transparent,
				VerticalAlignment = VerticalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				CornerRadius = new CornerRadius(0)
			};
			minBtn.Click += (_, __) => WindowState = WindowState.Minimized;

			var maxBtn = new Button
			{
				Content = "🗖",
				Width = 40,
				Background = Brushes.Transparent,
				VerticalAlignment = VerticalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				CornerRadius = new CornerRadius(0)
			};
			maxBtn.Click += (_, __) => { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; };

			var closeBtn = new Button
			{
				Content = "✕",
				Width = 40,
				Background = Brushes.Transparent,
				VerticalAlignment = VerticalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				CornerRadius = new CornerRadius(0)
			};
			closeBtn.Click += (_, __) => Close();

			controls.Children.Add(minBtn);
			controls.Children.Add(maxBtn);
			controls.Children.Add(closeBtn);

			chromeLayout.Children.Add(controls);

			_titleBar.Child = chromeLayout;

			if (platformSettings != null)
			{
				platformSettings.ColorValuesChanged += (_, e) =>
				{
					var newBrush = new SolidColorBrush(e.AccentColor1);
					_titleBar.Background = newBrush;
					_titleBarMenu!.Background = newBrush;
				};
			}
		}

		private void InitializeMenu()
		{
			var platformSettings = Application.Current?.PlatformSettings;
			var accentColor = platformSettings?.GetColorValues().AccentColor1 ?? Color.Parse("#D2D2D2");
			var accentBrush = new SolidColorBrush(accentColor);

			_titleBarMenu = new Menu
			{
				Background = accentBrush,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
			};

			MenuItem fileMenu		= new MenuItem { Header = "_File", CornerRadius = new CornerRadius(0), FontSize = MapperTheme.HeaderFontSize };

			_newMapItem					= new MenuItem { Header = "_New Map", HotKey = new KeyGesture(Key.N, KeyModifiers.Control), FontSize = MapperTheme.HeaderFontSize };
			_newMapItem.Click		   += NewMapItem_Click;

			_openMapItem				= new MenuItem { Header = "_Open Map...", HotKey = new KeyGesture(Key.O, KeyModifiers.Control), FontSize = MapperTheme.HeaderFontSize };
			_openMapItem.Click		   += OpenMapItem_Click;

			_saveMapItem				= new MenuItem { Header = "_Save Map", HotKey = new KeyGesture(Key.S, KeyModifiers.Control), FontSize = MapperTheme.HeaderFontSize };
			_saveMapItem.Click		   += SaveMapItem_Click;

			_saveAsMapItem				= new MenuItem { Header = "_Save Map As...", HotKey = new KeyGesture(Key.S, KeyModifiers.Control | KeyModifiers.Shift), FontSize = MapperTheme.HeaderFontSize };
			_saveAsMapItem.Click	   += SaveAsMapItem_Click;

			_importMapItem				= new MenuItem { Header = "_Import Map", HotKey = new KeyGesture(Key.I, KeyModifiers.Control), FontSize = MapperTheme.HeaderFontSize };
			_importMapItem.Click	   += ImportMapItem_Click;

			_exportMapItem				= new MenuItem { Header = "_Export Map", HotKey = new KeyGesture(Key.E, KeyModifiers.Control), FontSize = MapperTheme.HeaderFontSize };
			_exportMapItem.Click       += ExportMapItem_Click;

			MenuItem closeItem			= new MenuItem { Header = "E_xit", HotKey = new KeyGesture(Key.X, KeyModifiers.Control | KeyModifiers.Shift), FontSize = MapperTheme.HeaderFontSize };
			closeItem.Click		       += CloseItem_Click;

			fileMenu.Items.Add(_newMapItem);
			fileMenu.Items.Add(_openMapItem);
			fileMenu.Items.Add(_saveMapItem);
			fileMenu.Items.Add(_saveAsMapItem);
			fileMenu.Items.Add(_importMapItem);
			fileMenu.Items.Add(_exportMapItem);
			fileMenu.Items.Add(new Separator());
			fileMenu.Items.Add(closeItem);

			MenuItem editMenu = new MenuItem { Header = "_Edit", FontSize = MapperTheme.HeaderFontSize };

			_undoItem = new MenuItem { Header = "Undo", HotKey = new KeyGesture(Key.Z, KeyModifiers.Control), FontSize = MapperTheme.HeaderFontSize };
			_undoItem.Click += (_, __) => { _activeMap!.Undo(); _mapCanvasControl!.InvalidateVisual(); };

			_redoItem = new MenuItem { Header = "Redo", HotKey = new KeyGesture(Key.Y, KeyModifiers.Control), FontSize = MapperTheme.HeaderFontSize };
			_redoItem.Click += (_, __) => { _activeMap!.Redo(); _mapCanvasControl!.InvalidateVisual(); };

			_fillItem = new MenuItem { Header = "Fill Layer", FontSize = MapperTheme.HeaderFontSize };
			_fillItem.Click += (_, __) =>
			{
				var sel = _editorState.SelectedTile!.Value;

				var fillTile = new TileRef
				{
					Tileset = sel.TilesetIndex,
					TileId = sel.TileIndex
				};
				_activeMap!.FillLayer(_editorState.CurrentLayer, fillTile);
				_mapCanvasControl!.InvalidateVisual();
			};

			_clearItem = new MenuItem { Header = "Clear Layer", FontSize = MapperTheme.HeaderFontSize };
			_clearItem.Click += (_, __) =>
			{
				var empty = new TileRef
				{
					Tileset = 0,
					TileId = 0
				};
				_activeMap!.Clear(_editorState.CurrentLayer, empty);
				_mapCanvasControl!.InvalidateVisual();
			};

			editMenu.Items.Add(_undoItem);
			editMenu.Items.Add(_redoItem);
			editMenu.Items.Add(_fillItem);
			editMenu.Items.Add(_clearItem);

			MenuItem viewMenu = new MenuItem { Header = "_View", FontSize = MapperTheme.HeaderFontSize };

			_showItem = new MenuItem { Header = "Show Grid", FontSize = MapperTheme.HeaderFontSize };
			_showItem.Click += (_, __) => { _editorState.ShowGrid = !_editorState.ShowGrid; _mapCanvasControl!.InvalidateVisual(); };
			_viewItem = new MenuItem { Header = "3D View", FontSize = MapperTheme.HeaderFontSize };
			_viewItem.Click += (_, __) => { var dlg = new ViewportDialog(_areaDocument!); dlg.Show(this); };

			viewMenu.Items.Add(_showItem);
			viewMenu.Items.Add(_viewItem);

			MenuItem helpMenu = new MenuItem { Header = "_Help", FontSize = MapperTheme.HeaderFontSize };

			_shortItem = new MenuItem { Header = "Shortcuts", FontSize = MapperTheme.HeaderFontSize };
			_shortItem.Click += (_, __) => { var dlg = new ShortcutsDialog(_titleBarMenu); dlg.Show(this); };
			_aboutItem = new MenuItem { Header = "About", FontSize = MapperTheme.HeaderFontSize };
			_aboutItem.Click += (_, __) => { var dlg = new AboutDialog(); dlg.Show(this); };

			helpMenu.Items.Add(_shortItem);
			helpMenu.Items.Add(_aboutItem);

			_titleBarMenu.Items.Add(fileMenu);
			_titleBarMenu.Items.Add(editMenu);
			_titleBarMenu.Items.Add(viewMenu);
			_titleBarMenu.Items.Add(helpMenu);
		}

		private void InitializeUI()
		{
			_content = new Grid
			{
				ColumnDefinitions = new ColumnDefinitions("288, *, 400"),
				Background = MapperTheme.WindowBackground
			};

			_tilesetColumn = new Grid { Background = MapperTheme.ContainerBackground, Margin = OutterPanelMargin, RowDefinitions = new RowDefinitions("*, *, *") };
			Grid.SetColumn(_tilesetColumn, 0);

			var mapRoot = new Grid { RowDefinitions = new RowDefinitions("42, *"), Margin = InnerPanelMargin };
			Grid.SetColumn(mapRoot, 1);

			var toolsPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Background = MapperTheme.HeaderBackground
			};

			var mapBtn = new Button
			{
				Content = "Map Builder",
				Height = 30,
				Background = MapperTheme.ButtonHighlight,
				VerticalAlignment = VerticalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				CornerRadius = new CornerRadius(0),
				Margin = new Thickness(4)
			};

			var roomBtn = new Button
			{
				Content = "Room Builder",
				Height = 30,
				Background = MapperTheme.ButtonBackground,
				VerticalAlignment = VerticalAlignment.Stretch,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				CornerRadius = new CornerRadius(0),
				Padding = new Thickness(4)
			};

			mapBtn.Click += (_, __) =>
			{
				mapBtn.Background = MapperTheme.ButtonHighlight;
				roomBtn.Background = MapperTheme.ButtonBackground;
				_editorState.Tool = Tool.MapBuilder;
				RefreshSidebar();
				_mapCanvasControl!.InvalidateVisual();
			};

			roomBtn.Click += (_, __) =>
			{
				mapBtn.Background = MapperTheme.ButtonBackground;
				roomBtn.Background = MapperTheme.ButtonHighlight;
				_editorState.Tool = Tool.RoomBuilder;
				RefreshSidebar();
				_mapCanvasControl!.InvalidateVisual();
			};

			toolsPanel.Children.Add(mapBtn);
			toolsPanel.Children.Add(roomBtn);

			var canvasGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("134, *") };

			_mapCanvasControl = new MapCanvasControl { State = _editorState };
			Grid.SetColumn(_mapCanvasControl, 1);

			var layersScroll = new ScrollViewer { Background = MapperTheme.ContainerBackground, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
			var layersPanel = new StackPanel();

			for (byte i = 0; i < MapDocument.LAYERS; i++)
			{
				byte layerIndex = i;

				var btn = new Button
				{
					Content = $"Layer {layerIndex}",
					Height = 28,
					Margin = new Thickness(4),
					Background = MapperTheme.ButtonBackground,
					Foreground = Brushes.White,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					HorizontalContentAlignment = HorizontalAlignment.Center,
					FontSize = MapperTheme.HeaderFontSize
				};

				btn.Click += (_, __) =>
				{
					_editorState.CurrentLayer = layerIndex;
					_mapCanvasControl.InvalidateVisual();
					UpdateLayerButtons(layersPanel, layerIndex);
				};

				layersPanel.Children.Add(btn);
			}

			UpdateLayerButtons(layersPanel, 0);

			layersScroll.Content = layersPanel;
			Grid.SetColumn(layersScroll, 0);

			canvasGrid.Children.Add(_mapCanvasControl);
			canvasGrid.Children.Add(layersScroll);

			mapRoot.Children.Add(toolsPanel);
			Grid.SetRow(canvasGrid, 1);
			mapRoot.Children.Add(canvasGrid);

			var areaPanel = new Panel
			{
				Background = MapperTheme.ContainerBackground,
				Margin = OutterPanelMargin
			};

			var areaBorder = new Border
			{
				BorderBrush = MapperTheme.BorderBrush,
				BorderThickness = new Thickness(2),
				Padding = new Thickness(1), // Adds a small gap between the border and the map
				VerticalAlignment = VerticalAlignment.Top
			};

			var areaScroll = new ScrollViewer
			{
				HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			};

			_areaControl = new AreaControl();
			_areaControl.State = _editorState;

			_areaControl.MapSelected += map =>
			{
				if (map == null) return;
				_editorState.Area = _areaDocument;
				SetActiveMap(map);
			};

			areaBorder.SizeChanged += (_, e) =>
			{
				var width = e.NewSize.Width;
				areaBorder.Width = width;
				areaBorder.Height = width;
			};

			_areaDocument = CreateStartupArea();

			_editorState.Area = _areaDocument;
			_editorState.ActiveMapX = 0;
			_editorState.ActiveMapY = 0;

			SetActiveMap(_areaDocument.Maps[0, 0]!);

			areaScroll.Content = _areaControl;
			areaBorder.Child = areaScroll;
			areaPanel.Children.Add(areaBorder);

			_content.Children.Add(_tilesetColumn);
			_content.Children.Add(mapRoot);
			_content.Children.Add(areaPanel);
			Grid.SetColumn(areaPanel, 2);

			_mapCanvasControl.InvalidateVisual();
		}

		private void UpdateLayerButtons(StackPanel layersFlow, byte layerIndex)
		{
			for (int i = 0; i < layersFlow.Children.Count; i++)
			{
				var btn = (Button)layersFlow.Children[i];
				btn.Background = (i == layerIndex) ? MapperTheme.ButtonHighlight : MapperTheme.ButtonBackground;
			}
		}

		private AreaDocument CreateStartupArea()
		{
			var area = new AreaDocument(1, 1)
			{
				Name = "New Area"
			};

			area.Tilesets.Add(new Tileset { Name = "Regional" });
			area.Tilesets.Add(new Tileset { Name = "Local" });
			area.Tilesets.Add(new Tileset { Name = "Interior" });

			var map = new MapDocument
			{
				IsPreview = true
			};

			area.Maps[0, 0] = map;

			return area;
		}

		private void RefreshSidebar()
		{
			if (_tilesetColumn == null || _areaDocument == null) return;

			_tilesetColumn.Children.Clear();
			_tilesetColumn.RowDefinitions.Clear();

			if (_editorState.Tool == Tool.RoomBuilder)
			{
				_tilesetColumn.RowDefinitions.Add(new RowDefinition(GridLength.Star));
				var roomPanel = new RoomPanel(_editorState, () => _mapCanvasControl?.InvalidateVisual());
				_tilesetColumn.Children.Add(roomPanel);
			}
			else
			{
				var validTilesets = _areaDocument.Tilesets
					.Where(ts => ts != null)
					.ToList();

				for (int i = 0; i < validTilesets.Count; i++)
				{
					// 3. Add a row for every valid tileset
					_tilesetColumn.RowDefinitions.Add(new RowDefinition(GridLength.Star));

					var ts = validTilesets[i];
					var panel = new TilesetPanel(ts.Name, (byte)i, ts, _editorState);

					_tilesetColumn.Children.Add(panel);
					Grid.SetRow(panel, i);
				}
			}
		}

		private void SetActiveMap(MapDocument map)
		{
			_activeMap = map;

			_mapCanvasControl!.State = _editorState;
			_editorState.Area = _areaDocument;

			_mapCanvasControl.MapDocument = map;
			_mapCanvasControl.AreaDocument = _areaDocument;
			BindMap(map);
			_mapCanvasControl.InvalidateVisual();
		}

		void BindMap(MapDocument map)
		{
			RefreshSidebar();
			_areaControl!.SetArea(_areaDocument!);
			map.Update += () => _areaControl.InvalidateVisual();
		}

		private async void NewMapItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			var dlg = new NewMapDialog();
			var result = await dlg.ShowDialog<NewMapResult?>(this);

			if (result == null) return;

			var area = new AreaDocument(1, 1);
			area.Name = result.MapName;

			area.Tilesets.Add(result.Regional);
			area.Tilesets.Add(result.Local);
			if (result.Interior != null)
				area.Tilesets.Add(result.Interior);

			var map = new MapDocument
			{
				IsPreview = false
			};

			area.Maps[0, 0] = map;

			_areaDocument = area;
			SetActiveMap(map);

			_saveMapItem!.IsEnabled = true;
			_saveAsMapItem!.IsEnabled = true;
			_exportMapItem!.IsEnabled = true;
			_importMapItem!.IsEnabled = true;
		}

		private async void OpenMapItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			var dlg = new OpenMapDialog();
			var result = await dlg.ShowDialog<OpenMapResult?>(this);

			if (result == null) return;

			_areaDocument = result.Document;

			byte activeMapX = 0;
			byte activeMapY = 0;
			bool found = false;

			for (byte y = 0; y < _areaDocument!.Height; y++)
			{
				for (byte x = 0; x < _areaDocument.Width; x++)
				{
					if (_areaDocument.GetMap(x, y) != null)
					{
						activeMapX = x;
						activeMapY = y;
						found = true;
						break;
					}
				}

				if (found)
					break;
			}

			if (!found)
			{
				await MessageBoxManager.GetMessageBoxStandard("Error", "This area contains no maps.", ButtonEnum.Ok).ShowAsync();
				return;
			}

			SetActiveMap(_areaDocument.GetMap(activeMapX, activeMapY)!);
			_editorState.ActiveMapX = activeMapX;
			_editorState.ActiveMapY = activeMapY;

			_saveMapItem!.IsEnabled = true;
			_saveAsMapItem!.IsEnabled = true;
			_exportMapItem!.IsEnabled = true;
			_importMapItem!.IsEnabled = true;
		}

		private async void SaveMapItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			if (_activeMap == null || _activeMap.IsPreview)
				return;

			AreaSerializer.SaveBinary(_areaDocument!);
			await MessageBoxManager.GetMessageBoxStandard("Map Saved!", "Success", ButtonEnum.Ok).ShowAsPopupAsync(this);
		}

		private async void SaveAsMapItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			System.Diagnostics.Debug.WriteLine("SaveAsMapItem_Click");

			var dlg = new SaveAsDialog();
			var result = await dlg.ShowDialog<SaveAsMapResult?>(this);

			if (result == null) return;

			_areaDocument!.Name = result.MapName;
			AreaSerializer.SaveBinary(_areaDocument);
		}

		private async void ImportMapItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			var dlg = new ImportGhostDialog();
			var result = await dlg.ShowDialog<ImportGhostResult?>(this);

			if (result == null) return;

			if (result.SelectedGhostMaps.Count == 0)
				return;

			int maxX = 0;
			int maxY = 0;

			foreach(var (x, y, _) in  result.SelectedGhostMaps)
			{
				if (x > maxX) maxX = x;
				if (y > maxY) maxY = y;
			}

			while (_areaDocument!.Width <= maxX)
				_areaDocument.ExpandEast();
			while (_areaDocument!.Height <= maxY)
				_areaDocument.ExpandSouth();

			foreach (var (x, y, map) in result.SelectedGhostMaps)
			{
				map.IsGhost = true;
				_areaDocument.SetMap(x, y, map);
			}

			_areaControl?.InvalidateVisual();
		}

		private async void ExportMapItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			if (AreaExporter.ExportBinary(_areaDocument!))
			{
				await MessageBoxManager.GetMessageBoxStandard("Success!", "Successfully exported map to data.", ButtonEnum.Ok).ShowAsync();
				return;
			}

			await MessageBoxManager.GetMessageBoxStandard("Error", "Could not export map to data.", ButtonEnum.Ok).ShowAsync();
		}

		private void CloseItem_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
		{
			Close();
		}

		protected override async void OnClosing(WindowClosingEventArgs e)
		{
			base.OnClosing(e);

			if (_forceClose || _activeMap == null || _activeMap.IsPreview || !_activeMap.IsDirty)
				return;

			e.Cancel = true;


			var result = await MessageBoxManager.GetMessageBoxStandard("Unsaved Changes", "You have unsaved changes. Save before exit?", ButtonEnum.YesNoCancel).ShowWindowDialogAsync(this);

			if (result == ButtonResult.Cancel)
				return;

			if (result == ButtonResult.Yes)
			{
				try
				{
					AreaSerializer.SaveBinary(_areaDocument!);
				}
				catch (Exception ex)
				{
					await MessageBoxManager.GetMessageBoxStandard("Error", $"Failed to save map: {ex.Message}", ButtonEnum.Ok).ShowWindowDialogAsync(this);
					return;
				}
			}

			_forceClose = true;
			Close();
		}

		protected override void OnClosed(EventArgs e)
		{
			try
			{
				TilePreviewer.ClearCache();
			}
			catch { }

			base.OnClosed(e);
		}
	}
}