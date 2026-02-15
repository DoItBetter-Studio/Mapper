using System.Drawing;
using System.Windows.Forms;

using Glyphborn.Mapper.Editor;

namespace Glyphborn.Mapper
{
	sealed class CreateTilesetDialog : Form
	{
		public string? TilesetName { get; private set; }
		public TilesetType TilesetType { get; private set; }

		TextBox _nameBox;
		RadioButton _regional, _local, _interior;

		public CreateTilesetDialog()
		{
			Text = "Create Tileset";
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterParent;
			MaximizeBox = false;
			MinimizeBox = false;
			ClientSize = new Size(300, 200);
			BackColor = Color.FromArgb(45, 45, 48);
			ForeColor = Color.White;

			// Root layout
			var root = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 1,
				RowCount = 4,
				Padding = new Padding(10)
			};

			root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

			Controls.Add(root);

			// --- Name ---
			var nameLabel = new Label
			{
				Text = "Tileset Name",
				Dock = DockStyle.Top,
				Height = 20
			};

			_nameBox = new TextBox { Dock = DockStyle.Top };

			root.Controls.Add(nameLabel);
			root.Controls.Add(_nameBox);

			// --- Type group ---
			var typeGroup = new GroupBox
			{
				Text = "Tileset Type",
				Dock = DockStyle.Top,
				Height = 100,
				ForeColor = Color.White
			};

			var typeLayout = new FlowLayoutPanel
			{
				Dock = DockStyle.Fill,
				FlowDirection = FlowDirection.TopDown,
				Padding = new Padding(8)
			};

			_regional = new RadioButton { Text = "Regional", Checked = true };
			_local = new RadioButton { Text = "Local" };
			_interior = new RadioButton { Text = "Interior" };

			typeLayout.Controls.Add(_regional);
			typeLayout.Controls.Add(_local);
			typeLayout.Controls.Add(_interior);

			typeGroup.Controls.Add(typeLayout);
			root.Controls.Add(typeGroup);

			// --- Buttons ---
			var buttons = new FlowLayoutPanel
			{
				Dock = DockStyle.Fill,
				FlowDirection = FlowDirection.RightToLeft
			};

			var create = new Button
			{
				Text = "Create",
				DialogResult = DialogResult.OK,
				Width = 80,
				BackColor = Color.FromArgb(45, 45, 45),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Margin = new Padding(6)
			};

			var cancel = new Button
			{
				Text = "Cancel",
				DialogResult = DialogResult.Cancel,
				Width = 80,
				BackColor = Color.FromArgb(45, 45, 45),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Margin = new Padding(6)
			};

			create.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
			create.FlatAppearance.BorderSize = 1;
			create.Click += (_, __) => OnCreate();

			cancel.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
			cancel.FlatAppearance.BorderSize = 1;

			buttons.Controls.Add(create);
			buttons.Controls.Add(cancel);

			root.Controls.Add(buttons);

			AcceptButton = create;
			CancelButton = cancel;
		}

		private void OnCreate()
		{
			TilesetName = _nameBox.Text.Trim();
			TilesetType =
				_regional.Checked ? TilesetType.Regional :
				_local.Checked ? TilesetType.Local :
								 TilesetType.Interior;
		}
	}
}
