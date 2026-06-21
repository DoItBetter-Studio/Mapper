using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Damascus.Mapper.Controls;
using Damascus.Mapper.Theme;
using Glyphborn.Mapper.Editor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Glyphborn.Mapper
{
	public sealed record ImportGhostResult(List<(int x, int y, MapDocument map)> SelectedGhostMaps);

	public class ImportGhostDialog : Window
	{
		private List<(int x, int y, MapDocument map)> _selectedGhostMaps { get; set; } = new();

		private ListBox _areaView = null!;
		private GhostAreaSelectControl _ghostAreaControl = null!;

		public ImportGhostDialog()
		{
			Title = "Import Ghost Maps";
			Width = 600;
			Height = 500;
			CanResize = false;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			Background = new SolidColorBrush(Color.FromRgb(45, 45, 48));
			Icon = MapperTheme.Icon;

			BuildUI();
			LoadAreas();
		}

		private void BuildUI()
		{
			var root = new Grid();
			root.ColumnDefinitions.Add(new ColumnDefinition(200, GridUnitType.Pixel));
			root.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
			root.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
			root.RowDefinitions.Add(new RowDefinition(50, GridUnitType.Pixel));

			// Left panel: Area list
			_areaView = new ListBox
			{
				Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
				Foreground = Brushes.White,
				BorderThickness = new Thickness(0)
			};
			_areaView.SelectionChanged += OnAreaSelected;

			var areaWrap = Wrap("Areas", _areaView);
			Grid.SetColumn(areaWrap, 0);
			Grid.SetRow(areaWrap, 0);
			root.Children.Add(areaWrap);

			// Right panel: Ghost area selector
			_ghostAreaControl = new GhostAreaSelectControl();

			var ghostScroll = new ScrollViewer
			{
				Content = _ghostAreaControl,
				Background = new SolidColorBrush(Color.FromRgb(30, 30, 30))
			};

			var ghostWrap = Wrap("Select Maps", ghostScroll);
			Grid.SetColumn(ghostWrap, 1);
			Grid.SetRow(ghostWrap, 0);
			root.Children.Add(ghostWrap);

			// Button panel (spans both columns)
			var buttonPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(6)
			};

			var cancelBtn = new Button
			{
				Content = "Cancel",
				Width = 90,
				Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
				Foreground = Brushes.White,
				Margin = new Thickness(6)
			};
			cancelBtn.Click += (_, _) => Close(false);

			var importBtn = new Button
			{
				Content = "Import",
				Width = 90,
				Background = new SolidColorBrush(Color.FromRgb(45, 45, 45)),
				Foreground = Brushes.White,
				Margin = new Thickness(6)
			};
			importBtn.Click += OnImport;

			buttonPanel.Children.Add(cancelBtn);
			buttonPanel.Children.Add(importBtn);

			Grid.SetColumn(buttonPanel, 0);
			Grid.SetRow(buttonPanel, 1);
			Grid.SetColumnSpan(buttonPanel, 2);
			root.Children.Add(buttonPanel);

			Content = root;
		}

		private void OnAreaSelected(object? sender, SelectionChangedEventArgs e)
		{
			if (_areaView.SelectedItem is not AreaItem item)
				return;

			var area = AreaSerializer.LoadBinary(item.Path);
			_ghostAreaControl.SetArea(area);
		}

		private async void OnImport(object? sender, RoutedEventArgs e)
		{
			_selectedGhostMaps = _ghostAreaControl.GetSelectedCells();

			if (_selectedGhostMaps.Count == 0)
			{
				var dialog = new Window
				{
					Title = "Import",
					Width = 320,
					Height = 130,
					CanResize = false,
					WindowStartupLocation = WindowStartupLocation.CenterOwner,
					Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
					Content = new TextBlock
					{
						Text = "Please select at least one map to import as ghost.",
						Foreground = Brushes.White,
						TextWrapping = TextWrapping.Wrap,
						Margin = new Thickness(16),
						VerticalAlignment = VerticalAlignment.Center
					}
				};
				await dialog.ShowDialog(this);
				return;
			}

			Close(_selectedGhostMaps);
		}

		private static Control Wrap(string title, Control content)
		{
			var panel = new DockPanel { LastChildFill = true };

			var label = new TextBlock
			{
				Text = title,
				Background = new SolidColorBrush(Color.FromRgb(20, 20, 20)),
				Foreground = Brushes.White,
				FontWeight = FontWeight.SemiBold,
				FontFamily = new FontFamily("Segoe UI"),
				FontSize = 12,
				Padding = new Thickness(6),
				Height = 28
			};

			DockPanel.SetDock(label, Dock.Top);
			panel.Children.Add(label);
			panel.Children.Add(content);

			return panel;
		}

		private void LoadAreas()
		{
			Populate(_areaView, EditorPaths.Maps);
		}

		private static void Populate(ListBox lv, string path)
		{
			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			lv.ItemsSource = Directory
				.EnumerateFiles(path, "*.gbm")
				.Select(f => new AreaItem(Path.GetFileNameWithoutExtension(f), f))
				.ToList();
		}

		private record AreaItem(string Name, string Path)
		{
			public override string ToString() => Name;
		}
	}
}