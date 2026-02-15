using System;
using System.Drawing;
using System.Windows.Forms;

using Glyphborn.Mapper.Colors;
using Glyphborn.Mapper.Controls;
using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;

namespace Glyphborn.Mapper
{
	public partial class MapperForm : Form
	{
		private AreaDocument? _areaDocument;
		private MapDocument? _activeMap;
		private EditorState _editorState = new();
		private Panel? _clientHost;
		private MapCanvasControl? _mapCanvasControl;
		private TableLayoutPanel? _tilesetColumn;
		private AreaControl? _areaControl;

		public MapperForm()
		{
			InitializeComponent();

			// 1. Apply our dark renderer
			menuStrip.Renderer = new ToolStripProfessionalRenderer(new MenuStripColor());

			// 2. Style the top bar itself
			menuStrip.BackColor = Color.FromArgb(45, 45, 48);

			ApplyDarkThemeToMenu(menuStrip.Items);

			this.BackColor = Color.Black;
			this.ForeColor = Color.White;
		}

		private void MapperForm_Load(object sender, EventArgs e)
		{
			_clientHost = new Panel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				BackColor = Color.FromArgb(30, 30, 30),
				BorderStyle = BorderStyle.None,
				Padding = new Padding(0, 2, 0, 0)
			};

			var root = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 4,
				RowCount = 1,
				BackColor = Color.FromArgb(45, 45, 48)
			};

			root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 276));
			root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 134));
			root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
			root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 400));

			_mapCanvasControl = new MapCanvasControl
			{
				Dock = DockStyle.Fill,
				State = _editorState
			};

			root.Controls.Add(_mapCanvasControl, 2, 0);

			var layersPanel = new Panel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
			};

			root.Controls.Add(layersPanel, 1, 0);	

			var layersFlow = new FlowLayoutPanel
			{
				Dock = DockStyle.Top,
				FlowDirection = FlowDirection.TopDown,
				WrapContents = false,
				AutoSize = true,
				BackColor = Color.FromArgb(30, 30, 30)
			};

			layersPanel.Controls.Add(layersFlow);

			layersFlow.Controls.Clear();

			for (int i = 0; i < MapDocument.LAYERS; i++)
			{
				int layerIndex = i;

				var btn = new Button
				{
					Text = $"Layer {i}",
					Width = 90,
					Height = 28,
					Margin = new Padding(4),
					BackColor = Color.FromArgb(45, 45, 45),
					ForeColor = Color.White,
					FlatStyle = FlatStyle.Flat,
				};

				btn.FlatAppearance.BorderColor = Color.FromArgb(45, 45, 45);
				btn.FlatAppearance.BorderSize = 1;

				btn.Click += (_, __) =>
				{
					_editorState.CurrentLayer = layerIndex;
					_mapCanvasControl.Invalidate();
					UpdateLayerButtons(layersFlow, layerIndex);
				};

				layersFlow.Controls.Add(btn);
			}

			layersFlow.Controls[0].BackColor = Color.DodgerBlue;

			_tilesetColumn = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 1
			};

			root.Controls.Add(_tilesetColumn, 0, 0);

			var areaPanel = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = Color.FromArgb(30, 30, 30),
			};

			var areaContainer = new Panel
			{
				AutoScroll = true,
				BorderStyle = BorderStyle.Fixed3D
			};

			areaPanel.Resize += (_, __) =>
			{
				int size = areaPanel.Width;
				areaContainer.Size = new Size(size, size);
			};

			areaPanel.Controls.Add(areaContainer);

			_areaControl = new AreaControl();
			_areaControl.State = _editorState;

			_areaControl.MapSelected += map =>
			{
				if (map == null) return;

				_editorState.Area = _areaDocument;

				SetActiveMap(map);
			};

			areaContainer.Controls.Add(_areaControl);
			root.Controls.Add(areaPanel, 3, 0);

			_clientHost.Controls.Add(root);
			Controls.Add(_clientHost);
			_clientHost.BringToFront();

			_areaDocument = CreateStartupArea();

			_editorState.Area = _areaDocument;
			_editorState.ActiveMapX = 0;
			_editorState.ActiveMapY = 0;

			SetActiveMap(_areaDocument.Maps[0, 0]!);

			_areaDocument.Changed += () =>
			{
				_areaControl.Invalidate();
			};

			undoToolStripMenuItem.Click += (s, e) => { _activeMap!.Undo(); _mapCanvasControl.Invalidate(); };
			redoToolStripMenuItem.Click += (s, e) => { _activeMap!.Redo(); _mapCanvasControl.Invalidate(); };
		}

		private AreaDocument CreateStartupArea()
		{
			var area = new AreaDocument(1, 1)
			{
				Name = "New Area"
			};

			area.Tilesets.Add(new Tileset { Name = "Regional" });
			area.Tilesets.Add(new Tileset { Name = "Local" });
			area.Tilesets.Add(new Tileset { Name = "Interior" });

			var map = new MapDocument
			{
				IsPreview = true
			};

			area.Maps[0, 0] = map;

			return area;
		}

		private void UpdateLayerButtons(FlowLayoutPanel layersFlow, int layerIndex)
		{
			for (int i = 0; i < layersFlow.Controls.Count; i++)
			{
				var btn = (Button)layersFlow.Controls[i];
				btn.BackColor = (i == layerIndex) ? Color.DodgerBlue : Color.FromArgb(45, 45, 45);
			}
		}

		private void ApplyDarkThemeToMenu(ToolStripItemCollection items)
		{
			foreach (ToolStripItem item in items)
			{
				item.ForeColor = Color.White;

				if (item is ToolStripMenuItem menuItem)
				{
					ApplyDarkThemeToMenu(menuItem.DropDownItems);
				}
			}
		}

		void SetActiveMap(MapDocument map)
		{
			_activeMap = map;

			// Ensure editor state and canvas know the active area / map indices
			_mapCanvasControl!.State = _editorState;
			_editorState.Area = _areaDocument;

			_mapCanvasControl.MapDocument = map;
			_mapCanvasControl.AreaDocument = _areaDocument;
			BindMap(map);
			_mapCanvasControl.Invalidate();
		}

		void BindMap(MapDocument map)
		{
			_tilesetColumn!.SuspendLayout();
			_tilesetColumn.Controls.Clear();
			_tilesetColumn.RowStyles.Clear();

			var tilesets = _areaDocument!.Tilesets;

			_tilesetColumn.RowCount = tilesets.Count;

			for (int i = 0; i < tilesets.Count; i++)
			{
				_tilesetColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / tilesets.Count));
			}

			for (byte i = 0; i < tilesets.Count; i++)
			{
				var ts = tilesets[i];
				var pane = new TilesetPane(ts.Name, i, ts, _editorState);
				_tilesetColumn.Controls.Add(pane, 0, i);
			}

			_tilesetColumn.ResumeLayout();
			_areaControl!.SetArea(_areaDocument);

			map.Update += () => _areaControl.Invalidate();
		}

		private void NewMap_Click(object sender, EventArgs e)
		{
			using var dlg = new NewMapDialog();

			if (dlg.ShowDialog(this) != DialogResult.OK)
				return;

			var area = new AreaDocument(1, 1);

			area.Name = dlg.MapName!;

			area.Tilesets.Add(dlg.Regional!);
			area.Tilesets.Add(dlg.Local!);
			if (dlg.Interior != null)
				area.Tilesets.Add(dlg.Interior);

			var map = new MapDocument
			{
				IsPreview = false
			};

			area.Maps[0, 0] = map;

			_areaDocument = area;
			SetActiveMap(map);

			saveMapToolStripMenuItem.Enabled = true;
			saveMapAsToolStripMenuItem.Enabled = true;
			exportMapToolStripMenuItem.Enabled = true;
		}

		private void LoadMap_Click(object sender, EventArgs e)
		{
			var omd = new OpenMapDialog();

			if (omd.ShowDialog() == DialogResult.OK)
			{
				_areaDocument = omd.AreaDocument;

				byte activeMapX = 0;
				byte activeMapY = 0;
				bool found = false;

				for (byte y = 0; y < _areaDocument!.Height; y++)
				{
					for (byte x = 0; x < _areaDocument.Width; x++)
					{
						if (_areaDocument.GetMap(x, y) != null)
						{
							activeMapX = x;
							activeMapY = y;
							found = true;
							break;
						}
					}

					if (found)
						break;
				}

				if (!found)
				{
					MessageBox.Show("This area contains no maps.");
					return;
				}

				SetActiveMap(_areaDocument.GetMap(activeMapX, activeMapY)!);
				_editorState.ActiveMapX = activeMapX;
				_editorState.ActiveMapY = activeMapY;

				saveMapToolStripMenuItem.Enabled = true;
				saveMapAsToolStripMenuItem.Enabled = true;
				exportMapToolStripMenuItem.Enabled = true;
			}
		}

		private void SaveMap_Click(object sender, EventArgs e)
		{
			if (_activeMap == null || _activeMap.IsPreview)
				return;

			AreaSerializer.SaveBinary(_areaDocument!);
			MessageBox.Show("Map Saved!", "Success");
		}

		private void SaveMapAs_Click(object sender, EventArgs e)
		{
			var sad = new SaveAsDialog();

			if (sad.ShowDialog() == DialogResult.OK)
			{
				_areaDocument!.Name = sad.MapName!;
				AreaSerializer.SaveBinary(_areaDocument);
			}
		}

		private void Export_Click(object sender, EventArgs e)
		{
			if (AreaExporter.ExportBinary(_areaDocument!))
			{
				MessageBox.Show("Successfully exported map to data.");
				return;
			}

			MessageBox.Show("Could not export map to data.", "Error");
		}

		private void Exit_Click(object sender, EventArgs e)
		{
			Application.Exit();
		}

		private void ShowGrid_Click(object sender, EventArgs e)
		{
			_editorState.ShowGrid = !_editorState.ShowGrid;
			_mapCanvasControl!.Refresh();
		}

		private void _3DView_Click(object sender, EventArgs e)
		{
			if (_activeMap == null)
				return;

			var dlg = new ViewportDialog(_areaDocument!);
			dlg.Show();
		}

		// Prompt to save when closing. Cancelable.
		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			base.OnFormClosing(e);

			// If no document or it's preview, nothing to do
			if (_activeMap == null || _activeMap.IsPreview)
				return;

			if (!_activeMap.IsDirty)
				return;

			var result = MessageBox.Show(
				"You have unsaved changes. Save before exit?",
				"Unsaved Changes",
				MessageBoxButtons.YesNoCancel,
				MessageBoxIcon.Warning,
				MessageBoxDefaultButton.Button1
			);

			if (result == DialogResult.Cancel)
			{
				// Cancel the close
				e.Cancel = true;
				return;
			}

			if (result == DialogResult.Yes)
			{
				try
				{
					AreaSerializer.SaveBinary(_areaDocument!);
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Failed to save map: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
			}

			// if DialogResult.No we proceed and close
		}

		// Final cleanup after form closes
		protected override void OnFormClosed(FormClosedEventArgs e)
		{
			// Clear editor-only caches and dispose resources
			try
			{
				TilePreviewer.ClearCache();
			}
			catch { }

			base.OnFormClosed(e);
		}
	}
}
