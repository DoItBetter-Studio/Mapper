using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Damascus.Mapper.Theme;

namespace Damascus.Mapper.Dialogs
{
	public sealed record SaveAsMapResult(string MapName);

	sealed class SaveAsDialog : Window
	{
		private TextBox? _textBox;

		public SaveAsDialog()
		{
			Title = "Save Map As...";
			Width = 300;
			Height = 120;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			Background = MapperTheme.WindowBackground;
			CanResize = false;
			CanMinimize = false;
			CanMaximize = false;
			Icon = MapperTheme.Icon;

			BuildUI();
		}

		private void BuildUI()
		{
			var content = new Grid
			{
				RowDefinitions = new RowDefinitions("*, 50"),
				Margin = new Thickness(10)
			};

			// Input Row
			var inputStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
			inputStack.Children.Add(new TextBlock { Text = "Name:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });

			_textBox = new TextBox { Width = 200 };
			inputStack.Children.Add(_textBox);
			content.Children.Add(inputStack);

			// Buttons
			var btnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };

			var saveBtn = new Button { Content = "Save Map", Width = 90, Margin = new Thickness(5), Background = MapperTheme.ButtonHighlight };
			saveBtn.Click += (s, e) => SaveAndClose();

			var cancelBtn = new Button { Content = "Cancel", Width = 90, Margin = new Thickness(5) };
			cancelBtn.Click += (s, e) => Close();

			btnStack.Children.Add(saveBtn);
			btnStack.Children.Add(cancelBtn);

			Grid.SetRow(btnStack, 1);
			content.Children.Add(btnStack);

			Content = content;
		}

		private void SaveAndClose()
		{
			if (string.IsNullOrWhiteSpace(_textBox!.Text))
				return; // Or show a quick validation warning

			Close(new SaveAsMapResult(_textBox.Text.Trim()));
		}
	}
}
