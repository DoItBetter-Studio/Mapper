using System;
using System.Drawing;
using System.Numerics;
using System.Windows.Forms;

using Glyphborn.Mapper.Controls;
using Glyphborn.Mapper.Editor;

namespace Glyphborn.Mapper
{
	public partial class ViewportDialog : Form
	{
		private readonly Viewport3D _view;
		private readonly TrackBar _yaw;
		private readonly TrackBar _pitch;

		public ViewportDialog(AreaDocument area)
		{
			Text = "3D Map Preview";
			Width = 900;
			Height = 700;
			StartPosition = FormStartPosition.CenterParent;
			BackColor = Color.Black;

			_view = new Viewport3D
			{
				Dock = DockStyle.Fill,
				Area = area
			};

			Controls.Add(_view);

			// Overlay controls
			_yaw = new TrackBar
			{
				Minimum = -180,
				Maximum = 180,
				Value = 45,
				TickFrequency = 30,
				Width = 200,
				Height = 20,
				Top = 8,
				Anchor = AnchorStyles.Top | AnchorStyles.Right,
				Left = Width - 200 - 24
			};

			_pitch = new TrackBar
			{
				Minimum = -89,
				Maximum = 89,
				Value = -30,
				TickFrequency = 15,
				Width = 200,
				Height = 20,
				Top = 58,
				Anchor = AnchorStyles.Top | AnchorStyles.Right,
				Left = Width - 200 - 24
			};

			Controls.Add(_yaw);
			Controls.Add(_pitch);

			_yaw.BringToFront();
			_pitch.BringToFront();

			_yaw.ValueChanged += (_, __) => UpdateLight();
			_pitch.ValueChanged += (_, __) => UpdateLight();

			UpdateLight();
		}

		private void UpdateLight()
		{
			float yaw = _yaw.Value * MathF.PI / 180f;
			float pitch = _pitch.Value * MathF.PI / 180f;

			_view.LightDirection = Vector3.Normalize(new Vector3(
				MathF.Cos(yaw) * MathF.Cos(pitch),
				MathF.Sin(pitch),
				MathF.Sin(yaw) * MathF.Cos(pitch)
			));

			_view.Invalidate();
		}
	}
}
