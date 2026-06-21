using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Damascus.Mapper.Controls;
using Damascus.Mapper.Theme;
using Glyphborn.Mapper.Editor;
using System;
using System.Numerics;

namespace Damascus.Mapper.Dialogs
{
	sealed class ViewportDialog : Window
	{
		private ViewportControl _view;
		private readonly Slider _yaw;
		private readonly Slider _pitch;

		public ViewportDialog(AreaDocument area)
		{
			Title = "3D Map Preview";
			Width = 900;
			Height = 700;
			Background = Brushes.Black;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			Icon = MapperTheme.Icon;

			var root = new Grid();

			_view = new ViewportControl
			{
				Area = area
			};

			root.Children.Add(_view);

			var overlay = new StackPanel
			{
				Orientation = Orientation.Vertical,
				Spacing = 12,
				Width = 200,
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Top,
				Margin = new Thickness(0, 8, 24, 0)
			};

			_yaw = new Slider
			{
				Minimum = -180,
				Maximum = 180,
				Value = 45
			};

			_pitch = new Slider
			{
				Minimum = -89,
				Maximum = 89,
				Value = -30
			};

			overlay.Children.Add(_yaw);
			overlay.Children.Add(_pitch);

			root.Children.Add(overlay);

			Content = root;

			_yaw.PropertyChanged += (_, e) =>
			{
				if (e.Property == RangeBase.ValueProperty)
					UpdateLight();
			};

			_pitch.PropertyChanged += (_, e) =>
			{
				if (e.Property == RangeBase.ValueProperty)
					UpdateLight();
			};

			UpdateLight();
		}

		private void UpdateLight()
		{
			float yaw = (float)_yaw.Value * MathF.PI / 180f;
			float pitch = (float)_pitch.Value * MathF.PI / 180f;

			_view.LightDirection = Vector3.Normalize(
				new Vector3(
					MathF.Cos(yaw) * MathF.Cos(pitch),
					MathF.Sin(pitch),
					MathF.Sin(yaw) * MathF.Cos(pitch)));

			_view.InvalidateVisual();
		}
	}
}
