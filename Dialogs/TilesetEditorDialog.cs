using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Maths;
using Glyphborn.Mapper.Tiles;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

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

		// ── Variant sub-panel controls ────────────────────────────────────────
		private Panel? _variantPanel;
		private Label? _previewLabel; // Track this so we can move it
		private int _sharedBottomY;   // Remembers where the common controls end

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
				{
					e.Cancel = true;
					return;
				}
			}

			// FIX: Prevent residual GDI+ handle leakage when closing down the window
			if (!e.Cancel && _previewBox?.Image != null)
			{
				_previewBox.Image.Dispose();
				_previewBox.Image = null;
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

				var slotRect = new Rectangle(col * CellSize, row * CellSize, CellSize, CellSize);

				var drawRect = new Rectangle(
					slotRect.X + CellPadding,
					slotRect.Y + CellPadding,
					slotRect.Width - CellPadding * 2,
					slotRect.Height - CellPadding * 2);

				TileDefinition? tile = i < _tileset.Tiles.Count ? _tileset.Tiles[i] : null;
				RenderPrimitive? visual = tile?.GetPrimitives().FirstOrDefault();

				bool isEmpty = IsEmptySlot(i);
				bool isAir = i == 0;

				if (!isEmpty && tile != null)
				{
					if (i == _hoverSlot && i != _selectedSlot)
					{
						using var hoverBrush = new SolidBrush(Color.FromArgb(30, 255, 255, 255));
						g.FillRectangle(hoverBrush, drawRect);
					}

					if (isAir || tile.TileType == TileType.None)
					{
						using var hatch = new HatchBrush(HatchStyle.DiagonalCross,
							Color.FromArgb(50, 50, 65), Color.FromArgb(30, 30, 40));
						g.FillRectangle(hatch, drawRect);
					}
					else if (visual?.Texture != null)
					{
						try
						{
							// FIX: Wrapped in a using statement to destroy unmanaged Bitmap instances on every frame paint
							using var thumb = TextureToBitmap(visual.Texture);
							g.DrawImage(thumb, drawRect);
						}
						catch { g.FillRectangle(Brushes.DimGray, drawRect); }
					}
					else
					{
						g.FillRectangle(Brushes.Black, drawRect);
						using var xPen = new Pen(Color.FromArgb(180, 50, 50), 1.5f);
						g.DrawLine(xPen, drawRect.Left + 3, drawRect.Top + 3, drawRect.Right - 3, drawRect.Bottom - 3);
						g.DrawLine(xPen, drawRect.Right - 3, drawRect.Top + 3, drawRect.Left + 3, drawRect.Bottom - 3);
					}

					g.DrawRectangle(borderPen, drawRect);

					if (!isAir && tile.TileType != TileType.None && !string.IsNullOrEmpty(tile.Name))
					{
						var nameRect = new RectangleF(drawRect.X + 2, drawRect.Bottom - 13, drawRect.Width - 4, 12);
						var fmt = new StringFormat { Trimming = StringTrimming.EllipsisCharacter };
						g.DrawString(tile.Name, indexFont, nameBrush, nameRect, fmt);
					}
				}
				else
				{
					// FIX: Restored unallocated palette block view styling instead of the erroneous red X copy-paste error
					g.DrawRectangle(emptyPen, drawRect);

					int cx = drawRect.X + (drawRect.Width / 2);
					int cy = drawRect.Y + (drawRect.Height / 2);
					g.DrawString("+", nameFont, plusBrush, cx - 4, cy - 6);
				}

				g.DrawString(i.ToString(), indexFont, indexBrush, drawRect.X + 2, drawRect.Y + 2);

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

			if (!IsEmptySlot(slot))
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

			// Safe name resolution: if out of list bounds, it's inherently empty
			string tileName = "";
			if (slot < _tileset.Tiles.Count)
			{
				var t = _tileset.Tiles[slot];
				if (!string.IsNullOrEmpty(t.Name))
				{
					tileName = t.Name;
				}
			}

			string header = slot == 0
				? "Slot 0 — Air (reserved)"
				: $"Slot {slot}  —  {tileName}";

			ctx.Items.Add(new ToolStripMenuItem(header) { Enabled = false });
			ctx.Items.Add(new ToolStripSeparator());

			if (IsEmptySlot(slot))
			{
				var genericTile = new ToolStripMenuItem("Create Generic Tile");
				genericTile.Click += (_, __) => CreateTileAtSlot(slot, TileType.TileGeneric);
				ctx.Items.Add(genericTile);

				var animatedTile = new ToolStripMenuItem("Create Animated Tile");
				animatedTile.Click += (_, __) => CreateTileAtSlot(slot, TileType.TileAnimated);
				ctx.Items.Add(animatedTile);

				var doorTile = new ToolStripMenuItem("Create Door Tile");
				doorTile.Click += (_, __) => CreateTileAtSlot(slot, TileType.TileEntityDoor);
				ctx.Items.Add(doorTile);

				var cropTile = new ToolStripMenuItem("Create Crop Tile");
				cropTile.Click += (_, __) => CreateTileAtSlot(slot, TileType.TileEntityCrop);
				ctx.Items.Add(cropTile);

			}
			else
			{

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

		private void CreateTileAtSlot(int slot, TileType type)
		{
			EnsureSlots(slot);

			TileDefinition tile = type switch
			{
				TileType.TileGeneric => new TileGeneric(),
				TileType.TileAnimated => new TileAnimated(),
				TileType.TileEntityDoor => new TileEntityDoor(),
				TileType.TileEntityCrop => new TileEntityCrop(),
				TileType.None => throw new NotImplementedException(),
				_ => throw new InvalidDataException($"TileType unknown: {type}")
			};

			tile.Id = (ushort)slot;
			tile.Name = $"Tile {slot}";
			tile.Collision = CollisionType.None;

			_tileset.Tiles.Add( tile );

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

			// FIX 1: Overwrite the subclass instance with a raw base definition.
			// This resets its internal TileType back to TileType.None so IsEmptySlot() reads it correctly.
			_tileset.Tiles[slot] = new TileDefinition
			{
				Id = (ushort)slot,
				Name = "",
				Collision = CollisionType.None
			};

			// FIX 2: Cascade truncation loop.
			// Continually pop elements from the back of the list if they match our empty criteria,
			// but stop completely before hitting index 0 (the reserved Air tile).
			while (_tileset.Tiles.Count > 1 && IsEmptySlot(_tileset.Tiles.Count - 1))
			{
				_tileset.Tiles.RemoveAt(_tileset.Tiles.Count - 1);
			}

			// FIX 3: Out-of-bounds safety check for selection.
			// If the current selection was part of the truncated tail, drop focus immediately.
			if (_selectedSlot >= _tileset.Tiles.Count || _selectedSlot == slot)
			{
				_selectedSlot = -1;
				ClearPropertiesPanel();
			}

			RefreshCanvasHeight();
			MarkDirty();
		}

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
				});
			}
		}

		private bool IsEmptySlot(int slot)
		{
			if (slot == 0) return false;
			if (slot >= _tileset.Tiles.Count) return true;
			var t = _tileset.Tiles[slot];
			return string.IsNullOrEmpty(t.Name) && t.TileType == TileType.None;
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

			// ── THE ACCORDION ZONE STARTS RIGHT HERE NOW ──
			_variantPanel = new Panel
			{
				Location = new Point(0, y),
				Width = panel.Width,
				Height = 0,
				Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
			};
			panel.Controls.Add(_variantPanel);

			_sharedBottomY = y;

			_previewLabel = PropLabel("Preview:", new Point(12, y));
			panel.Controls.Add(_previewLabel);
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

			// Cleaned up original PositionImportButtons mechanics since they are now handled dynamically!
			return panel;
		}

		private int BuildPrimitiveControlsRow(TileDefinition tile, RenderPrimitive? primitive, int startY, int primitiveIndex = 0)
		{
			int y = startY;

			// 1. Mesh Row
			string meshName = primitive?.MeshSourcePath != null ? Path.GetFileName(primitive.MeshSourcePath) : "(none)";
			var meshLabel = PropLabel($"Mesh [{primitiveIndex}]: {meshName}", new Point(12, y));
			meshLabel.Size = new Size(230, 20);
			_variantPanel!.Controls.Add(meshLabel);

			var meshBtn = MakeButton("Import OBJ", 105);
			meshBtn.Location = new Point(_variantPanel.Width - 117, y - 2);
			meshBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			meshBtn.Click += (_, __) => DynamicImportMesh(tile, primitiveIndex);
			_variantPanel.Controls.Add(meshBtn);
			y += 32;

			// 2. Texture Row
			string texName = primitive?.TextureSourcePath != null ? Path.GetFileName(primitive.TextureSourcePath) : "(none)";
			var texLabel = PropLabel($"Texture [{primitiveIndex}]: {texName}", new Point(12, y));
			texLabel.Size = new Size(230, 20);
			_variantPanel.Controls.Add(texLabel);

			var texBtn = MakeButton("Import Tex", 105);
			texBtn.Location = new Point(_variantPanel.Width - 117, y - 2);
			texBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			texBtn.Enabled = (primitive?.Mesh != null); // Only allow texturing if a mesh is present!
			texBtn.Click += (_, __) => DynamicImportTexture(tile, primitiveIndex);
			_variantPanel.Controls.Add(texBtn);
			y += 42;

			return y - startY; // Returns total layout height consumed by this primitive row
		}

		private void AdjustLowerLayout(int variantPanelHeight)
		{
			if (_variantPanel == null || _previewLabel == null || _previewBox == null || _clearSlotButton == null)
				return;

			_variantPanel.Height = variantPanelHeight;

			// Pivot point starts right under the variant content zone
			int currentY = _sharedBottomY + variantPanelHeight;

			// Push down the remaining components natively
			if (variantPanelHeight > 0) currentY += 14; // Add padding space if variant is populated

			_previewLabel.Location = new Point(12, currentY);
			currentY += 22;

			_previewBox.Location = new Point(12, currentY);
			currentY += 144;

			_clearSlotButton.Location = new Point(12, currentY);
		}

		private void LoadTileProperties(TileDefinition tile)
		{
			// Keep the guard clause
			if (_variantPanel == null || _slotLabel == null) return;

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

			// --- REMOVED: _importMeshButton and _importTextureButton logic ---
			// The individual rows now handle their own button states.

			if (_previewBox!.Image != null)
			{
				_previewBox.Image.Dispose();
				_previewBox.Image = null;
			}

			RenderPrimitive? visual = tile.GetPrimitives().FirstOrDefault();
			_previewBox.Image = visual?.Texture != null
				? TextureToBitmap(visual.Texture)
				: null;

			_clearSlotButton!.Enabled = !isAir;

			// Clear previous custom inputs
			_variantPanel!.Controls.Clear();
			int variantHeight = 0;

			if (!isAir && !isEmpty)
			{
				variantHeight = tile switch
				{
					TileGeneric generic => PopulateGenericProperties(generic),
					TileAnimated animated => PopulateAnimatedProperties(animated),
					TileEntityDoor door => PopulateDoorProperties(door),
					TileEntityCrop crop => PopulateCropProperties(crop),
					_ => 0
				};
			}

			AdjustLowerLayout(variantHeight);

			_ignoreChanges = false;
		}

		private void ClearPropertiesPanel()
		{
			_ignoreChanges = true;

			_slotLabel!.Text = "No tile selected";
			_nameTextBox!.Text = "";
			_nameTextBox.Enabled = false;
			_collisionBox!.Enabled = false;

			if (_previewBox!.Image != null)
			{
				_previewBox.Image.Dispose();
				_previewBox.Image = null;
			}

			_clearSlotButton!.Enabled = false;

			_variantPanel!.Controls.Clear();
			AdjustLowerLayout(0);

			_ignoreChanges = false;
		}

		private int PopulateGenericProperties(TileGeneric tile)
		{
			// Simply display its single primitive layer row at y = 0
			return BuildPrimitiveControlsRow(tile, tile.Primitive, 0, 0);
		}

		private int PopulateAnimatedProperties(TileAnimated tile)
		{
			int y = 0;
			_variantPanel!.Controls.Add(PropLabel("Framerate:", new Point(12, y)));

			var input = new NumericUpDown
			{
				Location = new Point(100, y - 2),
				Width = 270,
				Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
				Minimum = 0,
				Maximum = 255,
				Value = tile.FrameRate
			};
			input.ValueChanged += (_, __) => { if (!_ignoreChanges) { tile.FrameRate = (byte)input.Value; MarkDirty(); } };
			_variantPanel.Controls.Add(input);
			y += 42;

			// Append its primitive row below the numerical input
			y += BuildPrimitiveControlsRow(tile, tile.Primitive, y, 0);
			return y;
		}

		private int PopulateDoorProperties(TileEntityDoor tile)
		{
			int y = 0;
			_variantPanel!.Controls.Add(PropLabel("Framerate:", new Point(12, y)));

			var fpsInput = new NumericUpDown
			{
				Location = new Point(100, y - 2),
				Width = 270,
				Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
				Minimum = 0,
				Maximum = 255,
				Value = tile.FrameRate
			};
			fpsInput.ValueChanged += (_, __) => { if (!_ignoreChanges) { tile.FrameRate = (byte)fpsInput.Value; MarkDirty(); } };
			_variantPanel.Controls.Add(fpsInput);
			y += 32;

			_variantPanel!.Controls.Add(PropLabel("Open Preview:", new Point(12, y)));
			var check = new CheckBox
			{
				Location = new Point(100, y - 4),
				Anchor = AnchorStyles.Left | AnchorStyles.Top,
				Checked = tile.OpenState
			};
			check.CheckedChanged += (_, __) => { if (!_ignoreChanges) { tile.OpenState = check.Checked; MarkDirty(); } };
			_variantPanel.Controls.Add(check);
			y += 42;

			// Loop through every existing primitive layer in this entity!
			for (int i = 0; i < tile.Primitives.Count; i++)
			{
				y += BuildPrimitiveControlsRow(tile, tile.Primitives[i], y, i);
			}

			// Add an explicit Append Button so developers can add layers sequentially
			var addLayerBtn = MakeButton("+ Add Primitive Layer", 358);
			addLayerBtn.Location = new Point(12, y);
			addLayerBtn.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
			addLayerBtn.Click += (_, __) => { tile.Primitives.Add(null!); LoadTileProperties(tile); MarkDirty(); };
			_variantPanel.Controls.Add(addLayerBtn);
			y += 38;

			return y;
		}

		private int PopulateCropProperties(TileEntityCrop tile)
		{
			int y = 0;
			_variantPanel!.Controls.Add(PropLabel("Growth Rate:", new Point(12, y)));

			var input = new NumericUpDown
			{
				Location = new Point(100, y - 2),
				Width = 270,
				Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
				Minimum = 0,
				Maximum = ushort.MaxValue,
				Value = tile.GrowthRate
			};
			input.ValueChanged += (_, __) => { if (!_ignoreChanges) { tile.GrowthRate = (ushort)input.Value; MarkDirty(); } };
			_variantPanel.Controls.Add(input);
			y += 42;

			// Render all available primitive layers
			for (int i = 0; i < tile.Primitives.Count; i++)
			{
				y += BuildPrimitiveControlsRow(tile, tile.Primitives[i], y, i);
			}

			// Add Append Button for expanding layers
			var addLayerBtn = MakeButton("+ Add Growth Stage Layer", 358);
			addLayerBtn.Location = new Point(12, y);
			addLayerBtn.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
			addLayerBtn.Click += (_, __) => { tile.Primitives.Add(null!); LoadTileProperties(tile); MarkDirty(); };
			_variantPanel.Controls.Add(addLayerBtn);
			y += 38;

			return y;
		}

		private void PropertyChanged(object? sender, EventArgs e)
		{
			if (_ignoreChanges || _selectedSlot < 0 || _selectedSlot >= _tileset.Tiles.Count)
				return;

			var tile = _tileset.Tiles[_selectedSlot];
			tile.Name = _nameTextBox!.Text;

			if (_collisionBox!.SelectedIndex >= 0)
				tile.Collision = (CollisionType)_collisionBox.SelectedItem!;

			_gridCanvas!.Invalidate();
			MarkDirty();
		}

		// =====================================================================
		//  Mesh / Texture import (Updated for Multi-Primitive TileEntities)
		// =====================================================================

		private void DynamicImportMesh(TileDefinition tile, int index)
		{
			using var ofd = new OpenFileDialog { Filter = "OBJ Files|*.obj", Title = $"Import Mesh for Primitive [{index}]" };
			if (ofd.ShowDialog() != DialogResult.OK) return;

			try
			{
				var mesh = MeshLoader.LoadOBJ(ofd.FileName);

				// Extract fallback texture metadata from current location if it exists
				var existingPrimitives = tile.GetPrimitives().ToList();
				RenderPrimitive? currentPrim = index < existingPrimitives.Count ? existingPrimitives[index] : null;
				var existingTex = currentPrim?.Texture ?? new Texture(1, 1, new uint[] { 0x00000000 });

				var newVisual = new RenderPrimitive(mesh, existingTex)
				{
					MeshSourcePath = ofd.FileName,
					TextureSourcePath = currentPrim?.TextureSourcePath
				};

				if (tile is TileEntity entity)
				{
					if (index < entity.Primitives.Count)
						entity.Primitives[index] = newVisual;
					else
						entity.Primitives.Add(newVisual);
				}
				else if (tile is TileGeneric generic)
				{
					generic.Primitive = newVisual;
				}
				else if (tile is TileAnimated animated)
				{
					animated.Primitive = newVisual;
				}

				LoadTileProperties(tile);
				_gridCanvas!.Invalidate();
				MarkDirty();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to import mesh:\n{ex.Message}", "Error");
			}
		}

		private void DynamicImportTexture(TileDefinition tile, int index)
		{
			using var ofd = new OpenFileDialog { Filter = "Image Files|*.png;*.jpg;*.bmp", Title = $"Import Texture for Primitive [{index}]" };
			if (ofd.ShowDialog() != DialogResult.OK) return;

			try
			{
				using var bmp = new Bitmap(ofd.FileName);
				var texture = BitmapToTexture(bmp);

				if (tile is TileEntity entity)
				{
					var currentMesh = entity.Primitives[index].Mesh;
					var currentMeshPath = entity.Primitives[index].MeshSourcePath;
					entity.Primitives[index] = new RenderPrimitive(currentMesh, texture)
					{
						MeshSourcePath = currentMeshPath,
						TextureSourcePath = ofd.FileName
					};
				}
				else if (tile is TileGeneric generic && generic.Primitive != null)
				{
					generic.Primitive = new RenderPrimitive(generic.Primitive.Mesh, texture)
					{
						MeshSourcePath = generic.Primitive.MeshSourcePath,
						TextureSourcePath = ofd.FileName
					};
				}
				else if (tile is TileAnimated animated && animated.Primitive != null)
				{
					animated.Primitive = new RenderPrimitive(animated.Primitive.Mesh, texture)
					{
						MeshSourcePath = animated.Primitive.MeshSourcePath,
						TextureSourcePath = ofd.FileName
					};
				}

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