using Avalonia.Controls;
using Avalonia.Media;
using Damascus.Mapper.Theme;
using System.Collections.Generic;
using System.Linq;

namespace Damascus.Mapper.Dialogs
{
	public class ShortcutsDialog : Window
	{
		public ShortcutsDialog(Menu menu)
		{
			Title = "Keyboard Shortcuts";
			Width = 420;
			Height = 500;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			CanMaximize = false;
			CanMinimize = false;
			CanResize = false;
			Background = MapperTheme.ContainerBackground;
			Icon = MapperTheme.Icon;

			var stack = new StackPanel()
			{
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
			};

			// Column headers
			AddRow(stack, "Command", "Shortcut", bold: true);
			AddSeparator(stack);

			// Keyboard shortcuts walked from the live menu
			foreach (var (path, gesture) in GetAllShortcuts(menu))
				AddRow(stack, path, gesture.ToString());

			// Spacer
			AddRow(stack, "", "");

			// Mouse controls header
			AddRow(stack, "Mouse Controls", "", bold: true);
			AddSeparator(stack);

			// Mouse controls
			AddRow(stack, "Left Click", "Paint tile / Select");
			AddRow(stack, "Right Click", "Erase tile");
			AddRow(stack, "Middle Click", "Bucket fill");
			AddRow(stack, "Scroll Wheel", "Scroll layers / tileset");
			AddRow(stack, "Left Drag", "Paint continuously");
			AddRow(stack, "Right Drag", "Erase continuously");
			AddRow(stack, "3D View", "Orbit / Pan / Zoom");

			Content = new ScrollViewer { Content = stack };
		}

		private static void AddRow(StackPanel panel, string command, string shortcut, bool bold = false)
		{
			var grid = new Grid { Margin = new Avalonia.Thickness(4, 2) };
			grid.ColumnDefinitions.Add(new ColumnDefinition(200, GridUnitType.Pixel));
			grid.ColumnDefinitions.Add(new ColumnDefinition(170, GridUnitType.Pixel));

			var weight = bold ? FontWeight.Bold : FontWeight.Normal;

			var col1 = new TextBlock { Text = command, FontWeight = weight };
			var col2 = new TextBlock { Text = shortcut, FontWeight = weight };

			Grid.SetColumn(col1, 0);
			Grid.SetColumn(col2, 1);

			grid.Children.Add(col1);
			grid.Children.Add(col2);
			panel.Children.Add(grid);
		}

		private static void AddSeparator(StackPanel panel)
		{
			panel.Children.Add(new Separator { Margin = new Avalonia.Thickness(0, 2) });
		}

		private static IEnumerable<(string Path, Avalonia.Input.KeyGesture Gesture)> GetAllShortcuts(Menu menu)
		{
			foreach (var item in menu.Items.OfType<MenuItem>())
				foreach (var entry in Walk(item, item.Header?.ToString() ?? ""))
					yield return entry;
		}

		private static IEnumerable<(string Path, Avalonia.Input.KeyGesture Gesture)> Walk(MenuItem item, string path)
		{
			if (item.HotKey != null)
				yield return (path, item.HotKey);

			foreach (var child in item.Items.OfType<MenuItem>())
				foreach (var entry in Walk(child, $"{path} → {child.Header}"))
					yield return entry;
		}
	}
}