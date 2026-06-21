using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Damascus.Mapper.Theme;
using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;
using MsBox.Avalonia;
using System;
using System.IO;
using System.Linq;

namespace Damascus.Mapper.Dialogs
{
	public sealed record NewMapResult(Tileset Regional, Tileset Local, Tileset? Interior, string MapName);

	public sealed record TilesetEntry(string Name, string Path)
	{
		public override string ToString() => Name;
	}

	public class NewMapDialog : Window
	{
		private ListBox? _regionalList;
		private ListBox? _localList;
		private ListBox? _interiorList;
		private CheckBox? _enableInterior;
		private TextBox? _mapNameTextBox;

		public NewMapDialog()
		{
			Title = "New Map";
			Width = 700;
			Height = 500;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			Background = MapperTheme.WindowBackground;
			Foreground = Brushes.White;
			CanResize = false;
			Icon = MapperTheme.Icon;

			BuildUI();
			LoadTilesets();
		}

		private void BuildUI()
		{
			var mainGrid = new Grid
			{
				RowDefinitions = new RowDefinitions("Auto, *, Auto"),
				ColumnDefinitions = new ColumnDefinitions("*, *, *"),
				Margin = new Thickness(10)
			};

			var headerStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
			headerStack.Children.Add(new TextBlock { Text = "Name:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
			_mapNameTextBox = new TextBox { Width = 200 };
			headerStack.Children.Add(_mapNameTextBox);

			Grid.SetRow(headerStack, 0);
			Grid.SetColumnSpan(headerStack, 3);
			mainGrid.Children.Add(headerStack);

			_regionalList = CreateTilesetList();
			_localList = CreateTilesetList();
			_interiorList = CreateTilesetList();

			_interiorList.IsEnabled = false;

			mainGrid.Children.Add(Wrap("Regional", _regionalList, 0, 1));
			mainGrid.Children.Add(Wrap("Local", _localList, 1, 1));
			mainGrid.Children.Add(Wrap("Interior", _interiorList, 2, 1));

			var footerGrid = new Grid
			{
				Margin = new Thickness(10, 0, 10, 10),
				ColumnDefinitions = new ColumnDefinitions("Auto, *, Auto") // Left, Spacer, Right
			};

			_enableInterior = new CheckBox { Content = "Enable Interior Tileset", VerticalAlignment = VerticalAlignment.Center };

			_enableInterior.IsCheckedChanged += (s, _) =>
			{
				if (s is CheckBox checkbox)
				{
					_interiorList.IsEnabled = checkbox.IsChecked ?? false;
				}
			};

			var buttonStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
			var createTilesetBtn = new Button { Content = "Create Tileset", Padding = new Thickness(10, 5) };
			createTilesetBtn.Click += OnCreateTileset;

			var createMapBtn = new Button { Content = "Create Map", Padding = new Thickness(10, 5) };
			createMapBtn.Click += OnCreateMap;

			buttonStack.Children.Add(createTilesetBtn);
			buttonStack.Children.Add(createMapBtn);

			footerGrid.Children.Add(_enableInterior);
			Grid.SetColumn(buttonStack, 2);
			footerGrid.Children.Add(buttonStack);

			Grid.SetRow(footerGrid, 2);
			Grid.SetColumnSpan(footerGrid, 3);
			mainGrid.Children.Add(footerGrid);

			Content = mainGrid;
		}

		private ListBox CreateTilesetList()
        {
            return new ListBox
            {
                Background = SolidColorBrush.Parse("#1E1E1E"),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(5)
            };
        }

        private Control Wrap(string title, Control content, int col, int row)
        {
			var border = new Border
			{
				BorderBrush = MapperTheme.BorderBrush, // Subtle divider color
				BorderThickness = new Thickness(1),
				Margin = new Thickness(5),
				Child = new StackPanel
				{
					Background = MapperTheme.ContainerBackground,
					Children =
					{
						// The Header with a distinct background
						new TextBlock
						{
							Text = title,
							Background = MapperTheme.HeaderBackground,
							Foreground = Brushes.White,
							Padding = new Thickness(10, 5),
							FontWeight = FontWeight.Bold
						},
						// The ListBox
						content
					}
				}
			};

			Grid.SetColumn(border, col);
			Grid.SetRow(border, row);
			return border;
		}

		private void LoadTilesets()
		{
			Populate(_regionalList!, EditorPaths.Regional);
			Populate(_localList!, EditorPaths.Local);
			Populate(_interiorList!, EditorPaths.Interior);
		}

		private void Populate(ListBox listBox, string path)
		{
			listBox.Items.Clear();

			// DUMP THE PATH TO SEE WHERE IT'S LOOKING
			string absolutePath = Path.GetFullPath(path);
			System.Diagnostics.Debug.WriteLine($"Looking for tilesets in: {absolutePath}");

			if (!Directory.Exists(path))
			{
				Console.WriteLine("Directory does not exist!");
				return;
			}

			var files = Directory.EnumerateFiles(path, "*.gbts").ToList();
			System.Diagnostics.Debug.WriteLine($"Found {files.Count} files.");

			foreach (var file in files)
			{
				listBox.Items.Add(new TilesetEntry(Path.GetFileNameWithoutExtension(file), file));
			}
		}

		private async void OnCreateMap(object? sender, RoutedEventArgs e)
		{
			var regional = (TilesetEntry)_regionalList!.SelectedItem!;
			var local = (TilesetEntry)_localList!.SelectedItem!;
			var interior = (TilesetEntry)_interiorList!.SelectedItem!;

			if (regional == null || local == null)
			{
				await MessageBoxManager.GetMessageBoxStandard("Error", "Regional and Local tilesets are required.").ShowAsync();
				return;
			}

			if (string.IsNullOrEmpty(_mapNameTextBox!.Text))
			{
				await MessageBoxManager.GetMessageBoxStandard("Error", "Map name is required.").ShowAsync();
				return;
			}

			System.Diagnostics.Debug.WriteLine($"Regional Tileset Selected: {regional}");

			var regionalTileset = TilesetSerializer.LoadBinary(regional.Path);
			var localTileset = TilesetSerializer.LoadBinary(local.Path);
			Tileset? interiorTileset = null;

			if (_enableInterior!.IsChecked == true && interior != null)
			{
				interiorTileset = TilesetSerializer.LoadBinary(interior.Path);
			}

			var mapName = _mapNameTextBox.Text.Trim();

			NewMapResult result = new NewMapResult(regionalTileset, localTileset, interiorTileset, mapName);

			Close(result);
		}

		private async void OnCreateTileset(object? sender, RoutedEventArgs e)
		{
			var dlg = new CreateTilesetDialog();
			var result = await dlg.ShowDialog<CreateTilesetResult?>(this);

			if (result == null) return;

			string basePath = result.TilesetType == TilesetType.Regional ? EditorPaths.Regional :
							  result.TilesetType == TilesetType.Local ? EditorPaths.Local : EditorPaths.Interior;

			string path = Path.Combine(basePath, $"{result.TilesetName}.gbts");

			var tileset = new Tileset
			{
				Name = result.TilesetName,
				Type = result.TilesetType
			};

			tileset.Tiles.Add(new TileDefinition
			{
				Id = 0,
				Name = "Air",
				Collision = CollisionType.None
			});

			TilesetSerializer.SaveBinary(tileset);
			LoadTilesets();
		}
	}
}
