using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Damascus.Mapper.Theme;
using Glyphborn.Mapper.Editor;

namespace Damascus.Mapper.Dialogs
{
	public sealed record CreateTilesetResult(string TilesetName, TilesetType TilesetType);

	sealed class CreateTilesetDialog : Window
	{
		TextBox _nameBox;
		RadioButton _regional;
		RadioButton _local;
		RadioButton _interior;

		public CreateTilesetDialog()
		{
			Title = "Create Tileset";
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			CanMinimize = false;
			CanMaximize = false;
			CanResize = false;
			Width = 300;
			Height = 275;
			Background = MapperTheme.WindowBackground;
			Foreground = Brushes.White;
			Icon = MapperTheme.Icon;

			var root = new StackPanel { Margin = new Thickness(10), Spacing = 10 };

			root.Children.Add(new TextBlock { Text = "Tileset Name" });
			_nameBox = new TextBox();
			root.Children.Add(_nameBox);

			const string groupName = "TilesetType";
			_regional = new RadioButton { Content = "Regional", GroupName = groupName, IsChecked = true };
			_local = new RadioButton { Content = "Local", GroupName = groupName };
			_interior = new RadioButton { Content = "Interior", GroupName = groupName };

			var groupBorder = new Border
			{
				BorderBrush = Brushes.DimGray,
				BorderThickness = new Thickness(1),
				Padding = new Thickness(5),
				CornerRadius = new CornerRadius(5)
			};

			var groupStack = new StackPanel { Spacing = 5 };
			groupStack.Children.AddRange(new[] { _regional, _local, _interior });
			groupBorder.Child = groupStack;

			root.Children.Add(new TextBlock { Text = "Tileset Type" });
			root.Children.Add(groupBorder);

			// 3. Buttons
			var btnStack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
			var createBtn = new Button { Content = "Create", Width = 80 };
			var cancelBtn = new Button { Content = "Cancel", Width = 80 };

			createBtn.Click += (s, e) => {
				var TilesetName = _nameBox.Text?.Trim();
				TilesetType TilesetType = _regional.IsChecked == true ? TilesetType.Regional :
							  _local.IsChecked == true ? TilesetType.Local :
							  TilesetType.Interior;

				CreateTilesetResult result = new CreateTilesetResult(TilesetName!, TilesetType);

				Close(result);
			};

			cancelBtn.Click += (s, e) => Close(false);

			btnStack.Children.Add(createBtn);
			btnStack.Children.Add(cancelBtn);
			root.Children.Add(btnStack);

			this.Content = root;
		}
	}
}
