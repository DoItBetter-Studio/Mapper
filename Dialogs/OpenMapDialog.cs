using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Damascus.Mapper.Theme;
using Glyphborn.Mapper.Editor;
using System.Diagnostics;
using System.IO;

namespace Damascus.Mapper.Dialogs
{
	public sealed record OpenMapResult(AreaDocument Document);

	sealed class OpenMapDialog : Window
	{
		private ListBox? _areaView;

		public OpenMapDialog()
		{
			Title = "Map Library";
			Width = 300;
			Height = 500;
			CanMaximize = false;
			CanMinimize = false;
			CanResize = false;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			Background = MapperTheme.WindowBackground;
			Foreground = MapperTheme.TextPrimary;
			Icon = MapperTheme.Icon;

			BuildUI();
			LoadAreas();
		}

		private void BuildUI()
		{
			var content = new Grid
			{
				RowDefinitions = new RowDefinitions("*, 50")
			};

			var listContainer = new DockPanel { Background = MapperTheme.ContainerBackground };

			var header = new TextBlock
			{
				Text = "Area",
				Height = 28,
				Padding = new Thickness(6, 6, 6, 0),
				Background = MapperTheme.HeaderBackground,
				Foreground = Brushes.White,
				FontWeight = FontWeight.SemiBold
			};
			DockPanel.SetDock(header, Dock.Top);
			listContainer.Children.Add(header);

			_areaView = new ListBox
			{
				Background = Brushes.Transparent,
				Margin = new Thickness(0)
			};
			_areaView.DoubleTapped += (s, e) => OpenSelected();
			listContainer.Children.Add(_areaView);

			content.Children.Add(listContainer);

			// Button
			var openBtn = new Button
			{
				Content = "Open Map",
				Width = 90,
				Height = 35,
				Margin = new Thickness(6),
				HorizontalAlignment = HorizontalAlignment.Right,
				Background = MapperTheme.ButtonHighlight,
				Foreground = Brushes.White
			};
			openBtn.Click += (s, e) => OpenSelected();

			Grid.SetRow(openBtn, 1);
			content.Children.Add(openBtn);

			Content = content;
		}

		private void LoadAreas()
		{
			if (!Directory.Exists(EditorPaths.Maps))
				Directory.CreateDirectory(EditorPaths.Maps);

			foreach (var file in Directory.EnumerateFiles(EditorPaths.Maps, "*.gbm"))
			{
				_areaView!.Items.Add(new ListBoxItem
				{
					Content = Path.GetFileNameWithoutExtension(file),
					Tag = file
				});
			}
		}

		private void OpenSelected()
		{
			if (_areaView!.SelectedItem is ListBoxItem selected)
			{
				// Using your struct-based or direct-result passing approach
				var doc = AreaSerializer.LoadBinary((string)selected.Tag!);
				Close(new OpenMapResult(doc));
			}
		}
	}
}
