using Avalonia.Controls;
using Damascus.Mapper.Theme;

namespace Damascus.Mapper.Dialogs
{
	sealed class AboutDialog : Window
	{
		public AboutDialog()
		{
			Title = "About";
			Width = 400;
			Height = 300;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			CanMaximize = false;
			CanMinimize = false;
			CanResize = false;
			Background = MapperTheme.ContainerBackground;
			Icon = MapperTheme.Icon;

			var stackPanel = new StackPanel()
			{
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
			};

			var label = new Label
			{
				Content = $"Mapper {VersionChecker.LOCAL_VERSION}\r\nWorld Authoring Tool\r\n\r\nDoItBetter Studio\r\nStarted: December 2025\r\n\r\nProprietary Software\r\nAll Rights Reserved\r\n",
			};

			stackPanel.Children.Add(label);

			Content = stackPanel;
		}
	}
}
