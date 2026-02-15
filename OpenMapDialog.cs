using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using Glyphborn.Mapper.Editor;

namespace Glyphborn.Mapper
{
	public partial class OpenMapDialog : Form
	{
		public AreaDocument? AreaDocument { get; private set; }

		private ListView _areaView;

		public OpenMapDialog()
		{
			Text = "Map Library";
			Width = 300;
			Height = 500;
			FormBorderStyle = FormBorderStyle.FixedDialog;
			StartPosition = FormStartPosition.CenterParent;
			MaximizeBox = false;
			MinimizeBox = false;
			BackColor = Color.FromArgb(45, 45, 48);
			ForeColor = Color.White;

			BuildUI();
			LoadAreas();
		}

		private ListView CreateAreaList()
		{
			var lv = new ListView
			{
				View = View.Details,
				FullRowSelect = true,
				MultiSelect = false,
				Dock = DockStyle.Fill,
				HeaderStyle = ColumnHeaderStyle.None,
				BackColor = Color.FromArgb(30, 30, 30),
				ForeColor = Color.White,
				BorderStyle = BorderStyle.None
			};

			lv.Columns.Add("Areas", -2);
			return lv;
		}

		private void BuildUI()
		{
			var root = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				RowCount = 2
			};

			root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
			root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

			_areaView = CreateAreaList();
			root.Controls.Add(Wrap("Area", _areaView), 0, 0);

			var openMapBtn = new Button
			{
				Width = 90,
				Text = "Open Map",
				Dock = DockStyle.Right,
				BackColor = Color.FromArgb(45, 45, 45),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Margin = new Padding(6)
			};

			openMapBtn.Click += OnOpenMap;
			_areaView.DoubleClick += OnOpenMap;

			root.Controls.Add(openMapBtn, 0, 1);

			Controls.Add(root);
		}

		private void OnOpenMap(object? sender, EventArgs e)
		{
			if (_areaView!.SelectedItems.Count == 0)
			{
				MessageBox.Show("Area selection required.");
				return;
			}

			AreaDocument = AreaSerializer.LoadBinary((string) _areaView.SelectedItems[0].Tag!);

			DialogResult = DialogResult.OK;
			Close();
		}

		private Control Wrap(string title, Control content)
		{
			var panel = new Panel { Dock = DockStyle.Fill };

			panel.Controls.Add(content);
			panel.Controls.Add(new Label
			{
				Text = title,
				Dock = DockStyle.Top,
				Height = 28,
				Padding = new Padding(6, 6, 6, 0),
				BackColor = Color.FromArgb(20, 20, 20),
				ForeColor = Color.White,
				Font = new Font("Segoe UI Semibold", 9f)
			});

			return panel;
		}

		private void LoadAreas()
		{
			Populate(_areaView!, EditorPaths.Maps);
		}

		private void Populate(ListView lv, string path)
		{
			lv.Items.Clear();

			if (!Directory.Exists(path))
				Directory.CreateDirectory(path);

			foreach (var file in Directory.EnumerateFiles(path, "*.gbm"))
			{
				lv.Items.Add(new ListViewItem(Path.GetFileNameWithoutExtension(file))
				{
					Tag = file
				});
			}
		}
	}
}
