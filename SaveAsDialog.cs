using System;
using System.Drawing;
using System.Windows.Forms;

using Glyphborn.Mapper.Tiles;

namespace Glyphborn.Mapper
{
	public partial class SaveAsDialog : Form
	{
		public string? MapName { get; private set; }

		private TextBox? _textBox;

		public SaveAsDialog()
		{
			MapName = null;

			Text = $"Save Map As...";
			Size = new Size(300, 110);
			FormBorderStyle = FormBorderStyle.FixedToolWindow;
			StartPosition = FormStartPosition.CenterParent;
			BackColor = Color.FromArgb(25, 25, 28);
			ForeColor = Color.White;

			BuildUI();
		}

		private void BuildUI()
		{
			var root = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 1,
				RowCount = 2,
			};

			root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
			root.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

			var mapNaming = new FlowLayoutPanel
			{
				Dock = DockStyle.Fill,
				FlowDirection = FlowDirection.LeftToRight,
				Height = 30,
			};

			var mapLabel = new Label
			{
				Text = "Name:",
				ForeColor = Color.White,
				Margin = new Padding(10),
				Width = 50
			};

			_textBox = new TextBox
			{
				Width = 200
			};

			mapNaming.Controls.Add(mapLabel);
			mapNaming.Controls.Add(_textBox);

			root.Controls.Add(mapNaming, 0, 0);

			var buttons = new FlowLayoutPanel
			{
				Dock = DockStyle.Fill,
				FlowDirection = FlowDirection.RightToLeft,
			};

			var saveAsBtn = new Button
			{
				Width = 90,
				Text = "Save Map",
				BackColor = Color.FromArgb(45, 45, 45),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Margin = new Padding(6)
			};

			saveAsBtn.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
			saveAsBtn.FlatAppearance.BorderSize = 1;
			saveAsBtn.Click += SaveAsBtn_Click;

			var cancelBtn = new Button
			{
				Width = 90,
				Text = "Cancel",
				BackColor = Color.FromArgb(45, 45, 45),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Margin = new Padding(6)
			};

			cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
			cancelBtn.FlatAppearance.BorderSize = 1;
			cancelBtn.Click += CancelBtn_Click;

			buttons.Controls.Add(saveAsBtn);
			buttons.Controls.Add(cancelBtn);

			root.Controls.Add(buttons, 0, 1);

			Controls.Add(root);
		}

		private void CancelBtn_Click(object? sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
		}

		private void SaveAsBtn_Click(object? sender, EventArgs e)
		{
			if (string.IsNullOrWhiteSpace(_textBox!.Text))
			{
				MessageBox.Show("Please enter a map name.");
				return;
			}

			MapName = _textBox.Text.Trim();

			DialogResult = DialogResult.OK;
		}
	}
}
