using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Damascus.Mapper.Theme;

namespace Damascus.Mapper
{
	public partial class App : Application
	{
		public override void Initialize()
		{
			AvaloniaXamlLoader.Load(this);
		}

		public override void OnFrameworkInitializationCompleted()
		{
			Resources["OverlayCornerRadius"] = new CornerRadius(0);

			if (Application.Current?.Resources is { } resources)
			{
				resources["ButtonBackgroundPointerOver"] = MapperTheme.ButtonHover;
				resources["ListBoxItemParagraphBackgroundPointerOver"] = MapperTheme.ButtonHover;
			}

			if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
			{
				desktop.MainWindow = new MainWindow();
			}

			base.OnFrameworkInitializationCompleted();
		}
	}
}