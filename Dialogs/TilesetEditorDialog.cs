using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Maths;
using Glyphborn.Mapper.Tiles;

namespace Glyphborn.Mapper
{
	public partial class TilesetEditorDialog : Form
	{
		// ── data ──────────────────────────────────────────────────────────────
		private readonly Tileset _tileset;
		private readonly string? _tilesetPath;
		private bool _isDirty;
		private bool _ignoreChanges;

		// ── grid state ────────────────────────────────────────────────────────
		private Panel? _gridCanvas;
		private int _selectedSlot = -1;
		private int _hoverSlot = -1;

		private const int CellSize = 32;
		private const int CellPadding = 2;
		private const int Columns = 8;

		private int CanvasWidth => Columns * CellSize;

		private int TotalSlots
		{
			get
			{
				int maxId = 0;
				foreach (var t in _tileset.Tiles)
					if (t.Id > maxId) maxId = t.Id;

				int total = Math.Max(256, maxId + 1);
				int rows = (int)Math.Ceiling(total / (float)Columns);
				return rows * Columns;
			}
		}

		// ── properties panel controls ─────────────────────────────────────────
		private Label? _slotLabel;
		private TextBox? _nameTextBox;
		private ComboBox? _collisionBox;
		private Label? _meshLabel;
		private Button? _importMeshButton;
		private Label? _textureLabel;
		private Button? _importTextureButton;
		private PictureBox? _previewBox;
		private Button? _clearSlotButton;

		// =====================================================================
		//  Construction
		// =====================================================================

		public TilesetEditorDialog(Tileset tileset)
		{
			_tileset = tileset;
			_tilesetPath = null;

			Text = $"Edit Tileset: {tileset.Name}";
			Size = new Size(940, 660);
			MinimumSize = new Size(680, 480);
			FormBorderStyle = FormBorderStyle.Sizable;
			StartPosition = FormStartPosition.CenterParent;
			BackColor = Color.FromArgb(25, 25, 28);
			ForeColor = Color.White;

			EnsureAirTile();
			BuildUI();
		}

		public TilesetEditorDialog(Tileset tileset, string tilesetPath) : this(tileset)
		{
			_tilesetPath = tilesetPath;
		}

		private void EnsureAirTile()
		{
			if (_tileset.Tiles.Count == 0)
			{
				_tileset.Tiles.Add(new TileDefinition
				{
					Id = 0,
					Name = "Air",
					Collision = CollisionType.None,
					Primitive = null
				});
			}
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			base.OnFormClosing(e);
			if (_isDirty && DialogResult != DialogResult.OK)
			{
				var r = MessageBox.Show(
					"You have unsaved changes. Discard them?",
					"Unsaved Changes",
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Warning);
				if (r == DialogResult.No)
					e.Cancel = true;
			}
		}

		// =====================================================================
		//  Top-level layout
		// =====================================================================

		private void BuildUI()
		{
			var mainLayout = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 2,
				RowCount = 1,
				Padding = new Padding(0)
			};

			mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));  // grid
			mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // properties

			mainLayout.Controls.Add(BuildGridPanel(), 0, 0);
			mainLayout.Controls.Add(BuildPropertiesPanel(), 1, 0);

			var bottomBar = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 52,
				BackColor = Color.FromArgb(30, 30, 36)
			};

			var saveBtn = MakeButton("Save Tileset", 120);
			var cancelBtn = MakeButton("Cancel", 90);
			cancelBtn.DialogResult = DialogResult.Cancel;
			saveBtn.Click += SaveTileset_Click;

			void PositionBottomButtons()
			{
				cancelBtn.Location = new Point(bottomBar.Width - cancelBtn.Width - 12, 12);
				saveBtn.Location = new Point(cancelBtn.Left - saveBtn.Width - 8, 12);
			}

			bottomBar.Controls.Add(saveBtn);
			bottomBar.Controls.Add(cancelBtn);
			bottomBar.Resize += (_, __) => PositionBottomButtons();
			Shown += (_, __) => PositionBottomButtons();

			Controls.Add(mainLayout);
			Controls.Add(bottomBar);
		}

		// =====================================================================
		//  Grid panel (left)
		// =====================================================================

		// Avoids reflection — DoubleBuffered is protected on Control
		private sealed class BufferedPanel : Panel
		{
			public BufferedPanel() { DoubleBuffered = true; }
		}

		private Panel BuildGridPanel()
		{
			var outer = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = Color.FromArgb(28, 28, 32)
			};

			var scroll = new Panel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				BackColor = Color.FromArgb(28, 28, 32)
			};

			_gridCanvas = new BufferedPanel
			{
				BackColor = Color.FromArgb(28, 28, 32),
				Cursor = Cursors.Hand,
				Width = CanvasWidth,
				Height = GetCanvasHeight()
			};

			_gridCanvas.Paint += GridCanvas_Paint;
			_gridCanvas.MouseDown += GridCanvas_MouseDown;
			_gridCanvas.MouseMove += GridCanvas_MouseMove;
			_gridCanvas.MouseLeave += (_, __) => { _hoverSlot = -1; _gridCanvas?.Invalidate(); };

			var ctxMenu = new ContextMenuStrip
			{
				BackColor = Color.FromArgb(40, 40, 50),
				ForeColor = Color.White
			};
			ctxMenu.Opening += GridContextMenu_Opening;
			_gridCanvas.ContextMenuStrip = ctxMenu;

			scroll.Controls.Add(_gridCanvas);
			outer.Controls.Add(scroll);

			outer.Controls.Add(new Label
			{
				Text = "Tiles",
				Dock = DockStyle.Top,
				Height = 32,
				TextAlign = ContentAlignment.MiddleLeft,
				Padding = new Padding(10, 0, 0, 0),
				Font = new Font("Segoe UI", 10f, FontStyle.Bold),
				ForeColor = Color.White,
				BackColor = Color.FromArgb(38, 38, 46)
			});

			return outer;
		}

		private int GetCanvasHeight() => (TotalSlots / Columns) * CellSize;

		private void RefreshCanvasHeight()
		{
			if (_gridCanvas == null) return;
			_gridCanvas.Height = GetCanvasHeight();
			_gridCanvas.Invalidate();
		}

		// ── painting ──────────────────────────────────────────────────────────

		private void GridCanvas_Paint(object? sender, PaintEventArgs e)
		{
			var g = e.Graphics;
			int total = TotalSlots;

			using var selectedPen = new Pen(Color.FromArgb(14, 116, 202), 2);
			using var emptyPen = new Pen(Color.FromArgb(55, 55, 58), 1) { DashStyle = DashStyle.Dash };
			using var borderPen = new Pen(Color.FromArgb(45, 45, 48), 1);
			using var indexBrush = new SolidBrush(Color.FromArgb(120, 120, 135));
			using var nameBrush = new SolidBrush(Color.LightGray);
			using var plusBrush = new SolidBrush(Color.FromArgb(110, 110, 115));
			using var indexFont = new Font("Segoe UI", 6.5f);
			using var nameFont = new Font("Segoe UI", 7f);

			for (int i = 0; i < total; i++)
			{
				int col = i % Columns;
				int row = i / Columns;

				// Slot rect: aligned to raw grid boundaries (keeps mouse hit-testing correct)
				var slotRect = new Rectangle(col * CellSize, row * CellSize, CellSize, CellSize);

				// Draw rect: inset by padding so adjacent borders don't bleed together
				var drawRect = new Rectangle(
					slotRect.X + CellPadding,
					slotRect.Y + CellPadding,
					slotRect.Width - CellPadding * 2,
					slotRect.Height - CellPadding * 2);

				// Direct index — Tiles[i].Id == i is always guaranteed by EnsureSlots
				TileDefinition? tile = i < _tileset.Tiles.Count ? _tileset.Tiles[i] : null;

				bool isEmpty = IsEmptySlot(i);
				bool isAir = i == 0;

				if (!isEmpty && tile != null)
				{
					// Hover tint
					if (i == _hoverSlot && i != _selectedSlot)
					{
						using var hoverBrush = new SolidBrush(Color.FromArgb(30, 255, 255, 255));
						g.FillRectangle(hoverBrush, drawRect);
					}

					if (isAir)
					{
						using var hatch = new HatchBrush(HatchStyle.DiagonalCross,
							Color.FromArgb(50, 50, 65), Color.FromArgb(30, 30, 40));
						g.FillRectangle(hatch, drawRect);
					}
					else if (tile.Primitive?.Texture != null)
					{
						try
						{
							var thumb = TextureToBitmap(tile.Primitive.Texture);
							g.DrawImage(thumb, drawRect);
						}
						catch { g.FillRectangle(Brushes.DimGray, drawRect); }
					}
					else
					{
						// Named tile but no texture yet — red X so it's clearly incomplete
						g.FillRectangle(Brushes.Black, drawRect);
						using var xPen = new Pen(Color.FromArgb(180, 50, 50), 1.5f);
						g.DrawLine(xPen, drawRect.Left + 3, drawRect.Top + 3,
										 drawRect.Right - 3, drawRect.Bottom - 3);
						g.DrawLine(xPen, drawRect.Right - 3, drawRect.Top + 3,
										 drawRect.Left + 3, drawRect.Bottom - 3);
					}

					g.DrawRectangle(borderPen, drawRect);

					// Tile name along the bottom strip
					if (!isAir && !string.IsNullOrEmpty(tile.Name))
					{
						var nameRect = new RectangleF(drawRect.X + 2, drawRect.Bottom - 13, drawRect.Width - 4, 12);
						var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
						g.DrawString(tile.Name, indexFont, nameBrush, nameRect, fmt);
					}
				}
				else
				{
					// Named tile but no texture yet — red X so it's clearly incomplete
					g.FillRectangle(Brushes.Black, drawRect);
					using var xPen = new Pen(Color.FromArgb(180, 50, 50), 1.5f);
					g.DrawLine(xPen, drawRect.Left + 3, drawRect.Top + 3,
									 drawRect.Right - 3, drawRect.Bottom - 3);
					g.DrawLine(xPen, drawRect.Right - 3, drawRect.Top + 3,
									 drawRect.Left + 3, drawRect.Bottom - 3);
				}

				// Slot index — always visible in the top-left corner
				g.DrawString(i.ToString(), indexFont, indexBrush, drawRect.X + 2, drawRect.Y + 2);

				// Selection outline — drawn slightly outside drawRect for visual pop
				if (i == _selectedSlot)
				{
					g.DrawRectangle(selectedPen,
						drawRect.X - 1, drawRect.Y - 1,
						drawRect.Width + 2, drawRect.Height + 2);
				}
			}
		}

		// ── mouse ─────────────────────────────────────────────────────────────

		private void GridCanvas_MouseMove(object? sender, MouseEventArgs e)
		{
			int slot = SlotAt(e.X, e.Y);
			if (slot == _hoverSlot) return;
			_hoverSlot = slot;
			_gridCanvas!.Invalidate();
		}

		private void GridCanvas_MouseDown(object? sender, MouseEventArgs e)
		{
			if (e.Button != MouseButtons.Left) return;
			int slot = SlotAt(e.X, e.Y);
			if (slot < 0) return;

			if (IsEmptySlot(slot))
			{
				var r = MessageBox.Show(
					$"Create a new tile at slot {slot}?",
					"Add Tile",
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Question);
				if (r == DialogResult.Yes)
					CreateTileAtSlot(slot);
			}
			else
			{
				SelectSlot(slot);
			}
		}

		private void GridContextMenu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
		{
			var ctx = (ContextMenuStrip)sender!;
			ctx.Items.Clear();

			var pos = _gridCanvas!.PointToClient(Cursor.Position);
			int slot = SlotAt(pos.X, pos.Y);
			if (slot < 0) { e.Cancel = true; return; }

			if (IsEmptySlot(slot))
			{
				var addItem = new ToolStripMenuItem($"Add Tile at Slot {slot}");
				addItem.Click += (_, __) => CreateTileAtSlot(slot);
				ctx.Items.Add(addItem);
			}
			else
			{
				string header = slot == 0
					? "Slot 0 — Air (reserved)"
					: $"Slot {slot}  —  {_tileset.Tiles[slot].Name}";
				ctx.Items.Add(new ToolStripMenuItem(header) { Enabled = false });
				ctx.Items.Add(new ToolStripSeparator());

				var selectItem = new ToolStripMenuItem("Select");
				selectItem.Click += (_, __) => SelectSlot(slot);
				ctx.Items.Add(selectItem);

				if (slot != 0)
				{
					var clearItem = new ToolStripMenuItem("Clear Slot") { ForeColor = Color.Salmon };
					clearItem.Click += (_, __) => ConfirmClearSlot(slot);
					ctx.Items.Add(clearItem);
				}
			}
		}

		private int SlotAt(int mx, int my)
		{
			int col = mx / CellSize;
			int row = my / CellSize;
			if (col < 0 || col >= Columns || row < 0) return -1;
			int slot = row * Columns + col;
			return slot < TotalSlots ? slot : -1;
		}

		// ── slot operations ───────────────────────────────────────────────────

		private void SelectSlot(int slot)
		{
			_selectedSlot = slot;
			_gridCanvas!.Invalidate();
			LoadTileProperties(_tileset.Tiles[slot]);
		}

		private void CreateTileAtSlot(int slot)
		{
			EnsureSlots(slot + 1);

			var tile = _tileset.Tiles[slot];
			tile.Name = $"Tile {slot}";
			tile.Collision = CollisionType.None;
			// Primitive stays null until a mesh is imported

			_selectedSlot = slot;
			RefreshCanvasHeight();
			SelectSlot(slot);
			MarkDirty();
		}

		private void ConfirmClearSlot(int slot)
		{
			if (slot <= 0 || slot >= _tileset.Tiles.Count) return;

			var r = MessageBox.Show(
				$"Clear slot {slot} '{_tileset.Tiles[slot].Name}'?\n\n" +
				"The slot index will be preserved; its mesh and texture data will be removed.",
				"Clear Slot",
				MessageBoxButtons.YesNo,
				MessageBoxIcon.Warning);

			if (r != DialogResult.Yes) return;

			var tile = _tileset.Tiles[slot];
			tile.Name = "";
			tile.Primitive = null;
			tile.MeshSourcePath = null;
			tile.TextureSourcePath = null;
			tile.Collision = CollisionType.None;

			if (_selectedSlot == slot)
			{
				_selectedSlot = -1;
				ClearPropertiesPanel();
			}

			RefreshCanvasHeight();
			MarkDirty();
		}

		// Extend _tileset.Tiles to at least `count` entries, padding gaps with
		// empty placeholders. Tiles[i].Id == i is always true after this call.
		private void EnsureSlots(int count)
		{
			while (_tileset.Tiles.Count < count)
			{
				int id = _tileset.Tiles.Count;
				_tileset.Tiles.Add(new TileDefinition
				{
					Id = (ushort)id,
					Name = "",
					Collision = CollisionType.None,
					Primitive = null
				});
			}
		}

		// A slot is empty when it has no name AND no mesh data.
		// EnsureSlots pads with Name = ""   → empty.
		// CreateTileAtSlot sets Name = "Tile N" → not empty.
		// ConfirmClearSlot resets Name = ""  → empty again.
		// Slot 0 (Air) is never empty.
		private bool IsEmptySlot(int slot)
		{
			if (slot == 0) return false;
			if (slot >= _tileset.Tiles.Count) return true;
			var t = _tileset.Tiles[slot];
			return string.IsNullOrEmpty(t.Name) && t.Primitive == null && t.MeshSourcePath == null;
		}

		// =====================================================================
		//  Properties panel (right)
		// =====================================================================

		private Panel BuildPropertiesPanel()
		{
			var panel = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = Color.FromArgb(40, 40, 50),
				AutoScroll = true
			};

			int y = 12;

			panel.Controls.Add(new Label
			{
				Text = "Tile Properties",
				Location = new Point(12, y),
				Size = new Size(370, 26),
				Font = new Font("Segoe UI", 11f, FontStyle.Bold),
				ForeColor = Color.White
			});
			y += 34;

			_slotLabel = new Label
			{
				Text = "No tile selected",
				Location = new Point(12, y),
				Size = new Size(370, 20),
				ForeColor = Color.FromArgb(120, 120, 145)
			};
			panel.Controls.Add(_slotLabel);
			y += 28;

			panel.Controls.Add(HRule(y)); y += 14;

			// Name
			panel.Controls.Add(PropLabel("Name:", new Point(12, y)));
			_nameTextBox = new TextBox
			{
				Location = new Point(100, y - 2),
				Width = 270,
				Enabled = false,
				Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
			};
			_nameTextBox.TextChanged += PropertyChanged;
			panel.Controls.Add(_nameTextBox);
			y += 34;

			// Collision
			panel.Controls.Add(PropLabel("Collision:", new Point(12, y)));
			_collisionBox = new ComboBox
			{
				Location = new Point(100, y - 2),
				Width = 270,
				DataSource = Enum.GetValues(typeof(CollisionType)),
				DropDownStyle = ComboBoxStyle.DropDownList,
				Enabled = false,
				Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
			};
			_collisionBox.SelectedIndexChanged += PropertyChanged;
			panel.Controls.Add(_collisionBox);
			y += 42;

			panel.Controls.Add(HRule(y)); y += 14;

			// Mesh row
			_meshLabel = PropLabel("Mesh: (none)", new Point(12, y));
			_meshLabel.Size = new Size(230, 20);
			panel.Controls.Add(_meshLabel);

			_importMeshButton = MakeButton("Import OBJ", 105);
			_importMeshButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			_importMeshButton.Enabled = false;
			_importMeshButton.Click += ImportMesh_Click;
			panel.Controls.Add(_importMeshButton);
			int meshBtnY = y - 2;
			y += 32;

			// Texture row
			_textureLabel = PropLabel("Texture: (none)", new Point(12, y));
			_textureLabel.Size = new Size(230, 20);
			panel.Controls.Add(_textureLabel);

			_importTextureButton = MakeButton("Import Texture", 115);
			_importTextureButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			_importTextureButton.Enabled = false;
			_importTextureButton.Click += ImportTexture_Click;
			panel.Controls.Add(_importTextureButton);
			int texBtnY = y - 2;
			y += 42;

			panel.Controls.Add(HRule(y)); y += 14;

			panel.Controls.Add(PropLabel("Preview:", new Point(12, y)));
			y += 22;

			_previewBox = new PictureBox
			{
				Location = new Point(12, y),
				Size = new Size(128, 128),
				BorderStyle = BorderStyle.FixedSingle,
				BackColor = Color.FromArgb(28, 28, 36),
				SizeMode = PictureBoxSizeMode.Zoom
			};
			panel.Controls.Add(_previewBox);
			y += 144;

			_clearSlotButton = new Button
			{
				Text = "Clear Slot",
				Location = new Point(12, y),
				Size = new Size(115, 30),
				BackColor = Color.FromArgb(150, 38, 38),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat,
				Enabled = false
			};
			_clearSlotButton.FlatAppearance.BorderColor = Color.FromArgb(100, 28, 28);
			_clearSlotButton.Click += (_, __) =>
			{
				if (_selectedSlot > 0)
					ConfirmClearSlot(_selectedSlot);
			};
			panel.Controls.Add(_clearSlotButton);

			void PositionImportButtons()
			{
				int right = panel.ClientSize.Width - 12;
				_importMeshButton!.Location = new Point(right - _importMeshButton.Width, meshBtnY);
				_importTextureButton!.Location = new Point(right - _importTextureButton.Width, texBtnY);
			}

			panel.Resize += (_, __) => PositionImportButtons();
			Shown += (_, __) => PositionImportButtons();

			return panel;
		}

		private void LoadTileProperties(TileDefinition tile)
		{
			_ignoreChanges = true;

			bool isEmpty = IsEmptySlot(tile.Id);
			bool isAir = tile.Id == 0;

			_slotLabel!.Text = isAir ? "Slot 0  —  Air (reserved)"
							 : isEmpty ? $"Slot {tile.Id}  —  (empty)"
							 : $"Slot {tile.Id}";

			_nameTextBox!.Text = tile.Name;
			_nameTextBox.Enabled = !isAir;

			_collisionBox!.SelectedItem = tile.Collision;
			_collisionBox.Enabled = !isAir;

			bool hasMesh = tile.Primitive?.Mesh != null;

			_importMeshButton!.Enabled = !isAir;
			_importTextureButton!.Enabled = !isAir && hasMesh;

			_meshLabel!.Text = "Mesh: " + (tile.MeshSourcePath != null ? Path.GetFileName(tile.MeshSourcePath) : "(none)");
			_textureLabel!.Text = "Texture: " + (tile.TextureSourcePath != null ? Path.GetFileName(tile.TextureSourcePath) : "(none)");

			_previewBox!.Image = tile.Primitive?.Texture != null
				? TextureToBitmap(tile.Primitive.Texture)
				: null;

			_clearSlotButton!.Enabled = !isAir;

			_ignoreChanges = false;
		}

		private void ClearPropertiesPanel()
		{
			_ignoreChanges = true;

			_slotLabel!.Text = "No tile selected";
			_nameTextBox!.Text = "";
			_nameTextBox.Enabled = false;
			_collisionBox!.Enabled = false;
			_meshLabel!.Text = "Mesh: (none)";
			_textureLabel!.Text = "Texture: (none)";
			_previewBox!.Image = null;
			_importMeshButton!.Enabled = false;
			_importTextureButton!.Enabled = false;
			_clearSlotButton!.Enabled = false;

			_ignoreChanges = false;
		}

		private void PropertyChanged(object? sender, EventArgs e)
		{
			if (_ignoreChanges || _selectedSlot < 0 || _selectedSlot >= _tileset.Tiles.Count)
				return;

			var tile = _tileset.Tiles[_selectedSlot];
			tile.Name = _nameTextBox!.Text;

			if (_collisionBox!.SelectedIndex >= 0)
				tile.Collision = (CollisionType)_collisionBox.SelectedItem!;

			_gridCanvas!.Invalidate(); // keep the cell name in sync as the user types
			MarkDirty();
		}

		// =====================================================================
		//  Mesh / Texture import
		// =====================================================================

		private void ImportMesh_Click(object? sender, EventArgs e)
		{
			if (_selectedSlot < 0 || _selectedSlot >= _tileset.Tiles.Count) return;
			var tile = _tileset.Tiles[_selectedSlot];

			using var ofd = new OpenFileDialog { Filter = "OBJ Files|*.obj", Title = "Import Mesh" };
			if (ofd.ShowDialog() != DialogResult.OK) return;

			try
			{
				var mesh = MeshLoader.LoadOBJ(ofd.FileName);
				var existingTex = tile.Primitive?.Texture ?? new Texture(1, 1, new uint[] { 0x00000000 });

				tile.Primitive = new RenderPrimitive(mesh, existingTex);
				tile.MeshSourcePath = ofd.FileName;

				LoadTileProperties(tile);
				_gridCanvas!.Invalidate();
				MarkDirty();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to import mesh:\n{ex.Message}", "Error");
			}
		}

		private void ImportTexture_Click(object? sender, EventArgs e)
		{
			if (_selectedSlot < 0 || _selectedSlot >= _tileset.Tiles.Count) return;
			var tile = _tileset.Tiles[_selectedSlot];

			using var ofd = new OpenFileDialog
			{
				Filter = "Image Files|*.png;*.jpg;*.bmp",
				Title = "Import Texture"
			};
			if (ofd.ShowDialog() != DialogResult.OK) return;

			try
			{
				if (tile.Primitive == null)
					throw new InvalidOperationException("Import a mesh before importing a texture.");

				using var bmp = new Bitmap(ofd.FileName);
				var texture = BitmapToTexture(bmp);

				tile.Primitive = new RenderPrimitive(tile.Primitive.Mesh, texture);
				tile.TextureSourcePath = ofd.FileName;

				LoadTileProperties(tile);
				_gridCanvas!.Invalidate();
				MarkDirty();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to import texture:\n{ex.Message}", "Error");
			}
		}

		// =====================================================================
		//  Save
		// =====================================================================

		private void SaveTileset_Click(object? sender, EventArgs e)
		{
			try
			{
				TilesetSerializer.SaveBinary(_tileset);
				_isDirty = false;
				DialogResult = DialogResult.OK;
				Close();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to save tileset:\n{ex.Message}", "Error");
			}
		}

		// =====================================================================
		//  Helpers
		// =====================================================================

		private void MarkDirty() => _isDirty = true;

		private static Button MakeButton(string text, int width)
		{
			var b = new Button
			{
				Text = text,
				Width = width,
				Height = 28,
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(52, 52, 64),
				ForeColor = Color.White
			};
			b.FlatAppearance.BorderColor = Color.FromArgb(68, 68, 82);
			b.FlatAppearance.BorderSize = 1;
			return b;
		}

		private static Label PropLabel(string text, Point loc) => new Label
		{
			Text = text,
			Location = loc,
			Size = new Size(86, 20),
			ForeColor = Color.White
		};

		private static Label HRule(int y) => new Label
		{
			Location = new Point(12, y),
			Size = new Size(370, 1),
			BackColor = Color.FromArgb(58, 58, 72),
			Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
		};

		private static Texture BitmapToTexture(Bitmap bmp)
		{
			var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
			var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			try
			{
				int stride = Math.Abs(bmpData.Stride);
				byte[] data = new byte[stride * bmp.Height];
				Marshal.Copy(bmpData.Scan0, data, 0, data.Length);

				var pixels = new uint[bmp.Width * bmp.Height];
				for (int py = 0; py < bmp.Height; py++)
					for (int px = 0; px < bmp.Width; px++)
					{
						int src = py * stride + px * 4;
						pixels[py * bmp.Width + px] =
							((uint)data[src + 3] << 24) |
							((uint)data[src + 2] << 16) |
							((uint)data[src + 1] << 8) |
								   data[src + 0];
					}

				return new Texture(bmp.Width, bmp.Height, pixels);
			}
			finally { bmp.UnlockBits(bmpData); }
		}

		private static Bitmap TextureToBitmap(Texture tex)
		{
			var bmp = new Bitmap(tex.Width, tex.Height, PixelFormat.Format32bppArgb);
			for (int py = 0; py < tex.Height; py++)
				for (int px = 0; px < tex.Width; px++)
				{
					uint p = tex.Pixels[py * tex.Width + px];
					bmp.SetPixel(px, py, Color.FromArgb(
						(int)(p >> 24 & 0xFF),
						(int)(p >> 16 & 0xFF),
						(int)(p >> 8 & 0xFF),
						(int)(p & 0xFF)));
				}
			return bmp;
		}
	}

	// =========================================================================
	//  MeshLoader — unchanged from original
	// =========================================================================

	internal class MeshLoader
	{
		internal static Mesh LoadOBJ(string path)
		{
			var positions = new System.Collections.Generic.List<Vec3>();
			var uvs = new System.Collections.Generic.List<Vec2>();
			var vertices = new System.Collections.Generic.List<Vertex>();
			var indices = new System.Collections.Generic.List<ushort>();
			var vertexMap = new System.Collections.Generic.Dictionary<(int pos, int uv), ushort>();

			foreach (var line in File.ReadLines(path))
			{
				if (line.StartsWith("v "))
				{
					var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					positions.Add(new Vec3(float.Parse(p[1]), float.Parse(p[2]), float.Parse(p[3])));
				}
				else if (line.StartsWith("vt "))
				{
					var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					uvs.Add(new Vec2(float.Parse(p[1]), 1.0f - float.Parse(p[2])));
				}
				else if (line.StartsWith("f "))
				{
					var p = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					for (int i = 2; i < p.Length - 1; i++)
					{
						int baseIndex = indices.Count;
						AddFaceVertex(p[1], positions, uvs, vertices, indices, vertexMap);
						AddFaceVertex(p[i], positions, uvs, vertices, indices, vertexMap);
						AddFaceVertex(p[i + 1], positions, uvs, vertices, indices, vertexMap);

						var v0 = vertices[indices[baseIndex]].Position;
						var v1 = vertices[indices[baseIndex + 1]].Position;
						var v2 = vertices[indices[baseIndex + 2]].Position;

						float ax = v1.x - v0.x, ay = v1.y - v0.y, az = v1.z - v0.z;
						float bx = v2.x - v0.x, bz = v2.z - v0.z;
						float ny = az * bx - ax * bz;

						if (ny < 0f)
						{
							var tmp = indices[baseIndex + 1];
							indices[baseIndex + 1] = indices[baseIndex + 2];
							indices[baseIndex + 2] = tmp;
						}
					}
				}
			}

			return new Mesh(vertices.ToArray(), indices.ToArray());
		}

		private static void AddFaceVertex(
			string token,
			System.Collections.Generic.List<Vec3> positions,
			System.Collections.Generic.List<Vec2> uvs,
			System.Collections.Generic.List<Vertex> vertices,
			System.Collections.Generic.List<ushort> indices,
			System.Collections.Generic.Dictionary<(int, int), ushort> vertexMap)
		{
			var parts = token.Split('/');
			int posIndex = int.Parse(parts[0]) - 1;
			int uvIndex = parts.Length > 1 && parts[1] != "" ? int.Parse(parts[1]) - 1 : -1;

			var key = (posIndex, uvIndex);
			if (!vertexMap.TryGetValue(key, out ushort index))
			{
				index = (ushort)vertices.Count;
				vertices.Add(new Vertex
				{
					Position = positions[posIndex],
					UV = uvIndex >= 0 ? uvs[uvIndex] : Vec2.Zero
				});
				vertexMap[key] = index;
			}

			indices.Add(index);
		}
	}
}