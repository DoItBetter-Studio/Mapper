using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

using Glyphborn.Mapper.Controls;
using Glyphborn.Mapper.Editor;

namespace Glyphborn.Mapper
{
	public partial class ImportGhostDialog : Form
	{
		public List<(int x, int y, MapDocument map)> SelectedGhostMaps { get; private set; }

		private ListView _areaView;
		private GhostAreaSelectControl _ghostAreaControl;

		public ImportGhostDialog()
		{
			Text = "Import Ghost Maps";
			Width = 600;  // Wider to fit both panels
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

		private void BuildUI()
		{
			var root = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 2,
				RowCount = 2
			};

			// Left column: area list (smaller)
			root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
			// Right column: area preview (fills remaining)
			root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

			// Top row: content (fills)
			root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
			// Bottom row: button (fixed)
			root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

			// Left panel: Area list
			_areaView = CreateAreaList();
			_areaView.SelectedIndexChanged += OnAreaSelected;
			root.Controls.Add(Wrap("Areas", _areaView), 0, 0);

			// Right panel: Ghost area selector
			var ghostPanel = new Panel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				BackColor = Color.FromArgb(30, 30, 30)
			};

			_ghostAreaControl = new GhostAreaSelectControl
			{
				BackColor = Color.FromArgb(30, 30, 30),
				Location = new Point(0, 0)
			};

			ghostPanel.Controls.Add(_ghostAreaControl);
			root.Controls.Add(Wrap("Select Maps", ghostPanel), 1, 0);

			// Button panel (spans both columns)
			var buttonPanel = new FlowLayoutPanel
			{
				Dock = DockStyle.Fill,
				FlowDirection = FlowDirection.RightToLeft
			};

			var importBtn = new Button
			{
				Width = 90,
				Text = "Import",
				BackColor = Color.FromArgb(45, 45, 45),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Margin = new Padding(6)
			};
			importBtn.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
			importBtn.FlatAppearance.BorderSize = 1;
			importBtn.Click += OnImport;

			var cancelBtn = new Button
			{
				Width = 90,
				Text = "Cancel",
				DialogResult = DialogResult.Cancel,
				BackColor = Color.FromArgb(45, 45, 45),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Margin = new Padding(6)
			};
			cancelBtn.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
			cancelBtn.FlatAppearance.BorderSize = 1;

			buttonPanel.Controls.Add(importBtn);
			buttonPanel.Controls.Add(cancelBtn);

			root.Controls.Add(buttonPanel, 0, 1);
			root.SetColumnSpan(buttonPanel, 2);  // Span both columns

			Controls.Add(root);
		}

		private void OnAreaSelected(object? sender, EventArgs e)
		{
			if (_areaView.SelectedItems.Count == 0)
				return;

			string path = (string) _areaView.SelectedItems[0].Tag!;
			var area = AreaSerializer.LoadBinary(path);
			_ghostAreaControl.SetArea(area);
		}

		private void OnImport(object? sender, EventArgs e)
		{
			SelectedGhostMaps = _ghostAreaControl.GetSelectedCells();

			if (SelectedGhostMaps.Count == 0)
			{
				MessageBox.Show("Please select at least one map to import as ghost.");
				return;
			}

			DialogResult = DialogResult.OK;
			Close();
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