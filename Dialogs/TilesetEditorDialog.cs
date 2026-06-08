using System;
using System.Collections.Generic;
using System.Drawing;
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
		private Tileset? _tileset;
		private Panel? _gridScrollPanel;
		private Panel? _gridCanvas;
		private int _selectedSlotIndex = 0;

		private const int CellSize = 32;
		private const int Columns = 8;
		private const int TotalSlots = 8;

		// Right panel controls
		private TextBox? _nameTextBox;
		private ComboBox? _collisionBox;
		private Label? _meshLabel;
		private Button? _importMeshButton;
		private Label? _textureLabel;
		private Button? _importTextureButton;
		private PictureBox? _previewBox;
		private Button? _deleteTileButton;

		private TileDefinition? _currentTile;
		private bool _ignoreChanges;

		private readonly string? _tilesetPath;
		private bool _isDirty;

		public TilesetEditorDialog(Tileset tileset)
		{
			_tileset = tileset;
			_tilesetPath = null;

			Text = $"Edit Tileset: {tileset.Name}";
			Size = new Size(800, 600);
			FormBorderStyle = FormBorderStyle.Sizable;
			StartPosition = FormStartPosition.CenterParent;
			MinimumSize = new Size(600, 400);
			BackColor = Color.FromArgb(25, 25, 28);
			ForeColor = Color.White;

			BuildUI();
			RefreshTileList();
		}

		public TilesetEditorDialog(Tileset tileset, string tilesetPath) : this(tileset)
		{
			_tilesetPath = tilesetPath;
		}

		protected override void OnFormClosing(FormClosingEventArgs e)
		{
			base.OnFormClosing(e);

			if (_isDirty && DialogResult != DialogResult.OK)
			{
				var result = MessageBox.Show(
					"You have unsaved changes. Discard them?",
					"Unsaved Changes",
					MessageBoxButtons.YesNo,
					MessageBoxIcon.Warning
				);

				if (result == DialogResult.No)
				{
					e.Cancel = true;
				}
			}
		}

		private void BuildUI()
		{
			var mainLayout = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				ColumnCount = 2,
				RowCount = 1,
				Padding = new Padding(10)
			};

			mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 288));
			mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

			// Left Panel: Tile List
			var leftPanel = CreateTileListPanel();
			mainLayout.Controls.Add(leftPanel, 0, 0);

			// Right Panel: Properties
			var rightPanel = CreatePropertiesPanel();
			mainLayout.Controls.Add(rightPanel, 1, 0);

			// Bottom buttons
			var buttonPanel = new FlowLayoutPanel
			{
				Dock = DockStyle.Bottom,
				FlowDirection = FlowDirection.RightToLeft,
				Height = 50,
				Padding = new Padding(10)
			};

			var saveButton = new Button
			{
				Text = "Save Tileset",
				Width = 100,
				Height = 30,
				FlatStyle = FlatStyle.Flat,
				Margin = new Padding(6)
			};

			saveButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
			saveButton.FlatAppearance.BorderSize = 1;
			saveButton.Click += SaveTileset_Click;

			var cancelButton = new Button
			{
				Text = "Cancel",
				Width = 100,
				Height = 30,
				DialogResult = DialogResult.Cancel,
				FlatStyle = FlatStyle.Flat,
				Margin = new Padding(6)
			};

			cancelButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
			cancelButton.FlatAppearance.BorderSize = 1;

			buttonPanel.Controls.Add(saveButton);
			buttonPanel.Controls.Add(cancelButton);

			Controls.Add(mainLayout);
			Controls.Add(buttonPanel);
		}

		private Panel CreateTileListPanel()
		{
			var panel = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = Color.FromArgb(30, 30, 30)
			};

			_gridScrollPanel = new Panel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				BackColor = Color.FromArgb(35, 35, 38)
			};

			_gridCanvas = new Panel
			{
				Location = new Point(0, 0),
				Width = Columns * CellSize,
				Height = (TotalSlots / Columns) * CellSize,
				BackColor = Color.FromArgb(35, 35, 38)
			};

			// Enable DoubleBuffering to stop flickering when scrolling or selecting tiles
			typeof(Panel).InvokeMember("DoubleBuffered",
				System.Reflection.BindingFlags.SetProperty | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
				null, _gridCanvas, new object[] { true });

			_gridCanvas.Paint += GridCanvas_Paint;
			_gridCanvas.MouseClick += GridCanvas_MouseClick;

			_gridScrollPanel.Controls.Add(_gridCanvas);
			panel.Controls.Add(_gridScrollPanel);

			return panel;
		}

		private Panel CreatePropertiesPanel()
		{
			var panel = new Panel
			{
				Dock = DockStyle.Fill,
				BackColor = Color.FromArgb(45, 45, 48),
				AutoScroll = true,
			};

			int y = 10;

			// Title
			var titleLabel = new Label
			{
				Text = "Tile Properties",
				Location = new Point(10, y),
				Size = new Size(300, 25),
				Font = new Font("Segoe UI", 12, FontStyle.Bold),
				ForeColor = Color.White
			};

			panel.Controls.Add(titleLabel);
			y += 35;

			panel.Controls.Add(new Label
			{
				Text = "Name:",
				Location = new Point(10, y),
				Size = new Size(80, 20),
				ForeColor = Color.White
			});

			_nameTextBox = new TextBox
			{
				Location = new Point(100, y),
				Width = 250
			};

			_nameTextBox.TextChanged += PropertyChanged;
			panel.Controls.Add(_nameTextBox);
			y += 30;

			panel.Controls.Add(new Label
			{
				Text = "Category:",
				Location = new Point(10, y),
				Size = new Size(80, 20),
				ForeColor = Color.White
			});

			_collisionBox = new ComboBox
			{
				Location = new Point(100, y),
				Width = 250,
				DataSource = Enum.GetValues(typeof(CollisionType))
			};

			_collisionBox.SelectedIndexChanged += PropertyChanged;
			panel.Controls.Add(_collisionBox);
			y += 40;

			// Mesh section
			_meshLabel = new Label
			{
				Text = "Mesh:",
				Location = new Point(10, y),
				Size = new Size(300, 20),
				Font = new Font("Segoe UI", 9, FontStyle.Bold),
				ForeColor = Color.White
			};
			panel.Controls.Add(_meshLabel);

			_importMeshButton = new Button
			{
				Text = "Import OBJ",
				Location = new Point(400, y - 2),
				Width = 100,
				Height = 25,
				Enabled = false,
				FlatStyle = FlatStyle.Flat,
				Margin = new Padding(6)
			};
			_importMeshButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
			_importMeshButton.FlatAppearance.BorderSize = 1;
			_importMeshButton.Click += ImportMesh_Click;
			panel.Controls.Add(_importMeshButton);
			y += 30;

			// Texture section
			_textureLabel = new Label
			{
				Text = "Texture:",
				Location = new Point(10, y),
				Size = new Size(300, 20),
				Font = new Font("Segoe UI", 9, FontStyle.Bold),
				ForeColor = Color.White,
			};
			panel.Controls.Add(_textureLabel);

			_importTextureButton = new Button
			{
				Text = "Import Texture",
				Location = new Point(400, y),
				Width = 100,
				Height = 25,
				FlatStyle = FlatStyle.Flat,
				Margin = new Padding(6),
				Enabled = false,
			};
			_importTextureButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
			_importTextureButton.FlatAppearance.BorderSize = 1;
			_importTextureButton.Click += ImportTexture_Click;
			panel.Controls.Add(_importTextureButton);
			y += 30;

			// Preview
			panel.Controls.Add(new Label
			{
				Text = "Preview:",
				Location = new Point(10, y),
				Size = new Size(80, 20),
				ForeColor = Color.White
			});
			y += 25;

			_previewBox = new PictureBox
			{
				Location = new Point(20, y),
				Size = new Size(128, 128),
				BorderStyle = BorderStyle.FixedSingle,
				BackColor = Color.FromArgb(30, 30, 30),
				SizeMode = PictureBoxSizeMode.Zoom
			};
			panel.Controls.Add(_previewBox);
			y += 140;

			// Delete button
			_deleteTileButton = new Button
			{
				Text = "Delete Tile",
				Location = new Point(20, y),
				Width = 150,
				Height = 30,
				BackColor = Color.FromArgb(180, 40, 40),
				ForeColor = Color.White,
				FlatStyle = FlatStyle.Flat
			};
			_deleteTileButton.Click += DeleteTile_Click;
			panel.Controls.Add(_deleteTileButton);

			return panel;
		}

		private void RefreshTileList()
		{
			UpdateGridSize();
			_gridCanvas?.Invalidate();

			if (_tileset!.Tiles.Count > 0)
			{
				TileDefinition? target = null;
				foreach (var t in _tileset.Tiles)
				{
					if (t.Id == _selectedSlotIndex) target = t;
				}

				if (target == null && _tileset.Tiles.Count > 0)
				{
					target = _tileset.Tiles[0];
					_selectedSlotIndex = target.Id;
				}

				if (target != null) LoadTileProperties(target);
			}
		}

		private void LoadTileProperties(TileDefinition tile)
		{
			_ignoreChanges = true;
			_currentTile = tile;

			_nameTextBox!.Text = tile.Name;
			_collisionBox!.SelectedItem = tile.Collision;

			bool hasMesh = tile.Primitive?.Mesh != null;
			bool hasTexture = tile.Primitive?.Texture != null;

			// Enable / disable import buttons
			_importMeshButton!.Enabled = true;
			_importTextureButton!.Enabled = hasMesh;

			_meshLabel!.Text = "Mesh: " +
				(_currentTile.MeshSourcePath != null
					? $"({Path.GetFileName(_currentTile.MeshSourcePath)})"
					: "(none)");

			_textureLabel!.Text = "Texture: " +
				(_currentTile.TextureSourcePath != null
					? $"({Path.GetFileName(_currentTile.TextureSourcePath)})"
					: "(none)");

			if (hasTexture)
			{
				_previewBox!.Image = TilePreviewer.GetPreview(tile.Primitive!.Texture);
			}
			else
			{
				_previewBox!.Image = null;
			}

			_deleteTileButton!.Enabled = tile.Id != 0;

			_ignoreChanges = false;
		}

		private void PropertyChanged(object? sender, EventArgs e)
		{
			if (_ignoreChanges || _currentTile == null)
				return;

			_currentTile.Name = _nameTextBox!.Text;
			if (_collisionBox!.SelectedIndex >= 0)
			{
				_currentTile.Collision = (CollisionType)_collisionBox.SelectedItem;
				MarkDirty();
			}

			// Update list display
			_gridCanvas!.Invalidate();
		}


		private void ImportMesh_Click(object? sender, EventArgs e)
		{
			if (_currentTile == null)
				return;

			var ofd = new OpenFileDialog
			{
				Filter = "OBJ Files|*.obj",
				Title = "Import Mesh"
			};

			if (ofd.ShowDialog() == DialogResult.OK)
			{
				try
				{
					var mesh = MeshLoader.LoadOBJ(ofd.FileName);

					// Preserve existing texture if available; otherwise create a 1x1 transparent texture
					var existingTex = _currentTile.Primitive?.Texture ?? new Texture(1, 1, new uint[] { 0x00000000 });
					_currentTile.Primitive = new RenderPrimitive(mesh, existingTex);

					// refresh preview / list
					_previewBox!.Image = _currentTile.Primitive != null ? TilePreviewer.GetPreview(_currentTile.Primitive.Texture) : null;
					_gridCanvas!.Invalidate();


					_currentTile.MeshSourcePath = ofd.FileName;
					_importTextureButton!.Enabled = true;
					LoadTileProperties(_currentTile);
					MarkDirty();
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Failed to import mesh: {ex.Message}", "Error");
				}
			}
		}

		private void ImportTexture_Click(object? sender, EventArgs e)
		{
			if (_currentTile == null)
				return;

			var ofd = new OpenFileDialog
			{
				Filter = "Image Files|*.png;*.jpg;*.bmp",
				Title = "Import Texture"
			};

			if (ofd.ShowDialog() == DialogResult.OK)
			{
				try
				{
					using (var bmp = new Bitmap(ofd.FileName))
					{
						var texture = BitmapToTexture(bmp);

						// Create or Update render primitive, preserving mesh selection
						if (_currentTile.Primitive == null)
							throw new InvalidOperationException("Tile has no mesh yet");
						_currentTile.Primitive = new RenderPrimitive(_currentTile.Primitive.Mesh, texture);

						// Update preview (editor-only previewer)
						_previewBox!.Image = TilePreviewer.GetPreview(texture);

						_gridCanvas!.Invalidate();
					}

					LoadTileProperties(_currentTile);
					_currentTile.TextureSourcePath = ofd.FileName;
					MarkDirty();
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Failed to import texture: {ex.Message}", "Error");
				}
			}
		}

		private void DeleteTile_Click(object? sender, EventArgs e)
		{
			if (_currentTile == null || _currentTile.Id == 0) return;

			var result = MessageBox.Show(
				$"Delete tile '{_currentTile.Name}'? This will leave its grid position empty.",
				"Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning
			);

			if (result == DialogResult.Yes)
			{
				_tileset!.Tiles.Remove(_currentTile);
				_currentTile = null;
				UpdateGridSize();
				RefreshTileList();
				MarkDirty();
			}
		}

		// Helper: conver System.Drawing.Bitmap -> engine texture
		private Texture BitmapToTexture(Bitmap bmp)
		{
			var rect = new Rectangle(0, 0, bmp.Width, bmp.Height);
			var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			try
			{
				int stride = Math.Abs(bmpData.Stride);
				int bytes = stride * bmp.Height;
				byte[] data = new byte[bytes];
				Marshal.Copy(bmpData.Scan0, data, 0, bytes);

				var pixels = new uint[bmp.Width * bmp.Height];

				for (int y = 0; y < bmp.Height; y++)
				{
					for (int x = 0; x < bmp.Width; x++)
					{
						int srcIdx = y * stride + x * 4;
						byte b = data[srcIdx + 0];
						byte g = data[srcIdx + 1];
						byte r = data[srcIdx + 2];
						byte a = data[srcIdx + 3];

						pixels[y * bmp.Width + x] = ((uint) a << 24) | ((uint) r << 16) | ((uint) g << 8) | b;
					}
				}

				return new Texture(bmp.Width, bmp.Height, pixels);
			}
			finally
			{
				bmp.UnlockBits(bmpData);
			}
		}

		private void SaveTileset_Click(object? sender, EventArgs e)
		{
			try
			{
				TilesetSerializer.SaveBinary(_tileset!);
				_isDirty = false;
				DialogResult = DialogResult.OK;
				Close();
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Failed to save tileset:\n{ex.Message}", "Error");
			}
		}

		private void MarkDirty()
		{
			_isDirty = true;
		}

		private void GridCanvas_Paint(object? sender, PaintEventArgs e)
		{
			if (_tileset == null) return;

			// Use a smoother rendering hint for text overlays
			e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

			using (var selectedPen = new Pen(Color.FromArgb(40, 120, 180), 2))
			using (var emptyPen = new Pen(Color.FromArgb(55, 55, 58), 1) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash })
			using (var borderPen = new Pen(Color.FromArgb(45, 45, 48), 1))
			using (var font = new Font("Segoe UI", 7f))
			using (var idBrush = new SolidBrush(Color.FromArgb(110, 110, 115)))
			{
				int totalRenderSlots = (_gridCanvas!.Height / CellSize) * Columns;

				for (int i = 0; i < totalRenderSlots; i++)
				{
					int row = i / Columns;
					int col = i % Columns;

					// Full bounding box of the slot
					var slotRect = new Rectangle(col * CellSize, row * CellSize, CellSize, CellSize);
					// Inner content bounding box (leaves a 2px gap around tiles)
					var contentRect = new Rectangle(slotRect.X + 2, slotRect.Y + 2, CellSize - 4, CellSize - 4);

					TileDefinition? tile = null;
					foreach (var t in _tileset.Tiles)
					{
						if (t.Id == i) { tile = t; break; }
					}

					// Draw Slot Background/Borders
					if (tile != null)
					{
						// Draw thumbnail preview
						if (tile.Primitive?.Texture != null)
						{
							try
							{
								var thumb = TilePreviewer.GetThumbnail(tile.Primitive.Texture, contentRect.Width, contentRect.Height);
								e.Graphics.DrawImage(thumb, contentRect);
							}
							catch
							{
								e.Graphics.FillRectangle(Brushes.DimGray, contentRect);
							}
						}
						else
						{
							e.Graphics.FillRectangle(Brushes.DimGray, contentRect);
						}

						// Subtle frame around active tiles
						e.Graphics.DrawRectangle(borderPen, contentRect);
					}
					else
					{
						// Dotted line frame for empty slots
						e.Graphics.DrawRectangle(emptyPen, contentRect);
						// Draw a faint central plus sign
						e.Graphics.DrawString("+", font, idBrush, slotRect.X + (CellSize / 2) - 4, slotRect.Y + (CellSize / 2) - 6);
					}

					// Draw Selection Highlight Border (Overlays on top of the tile bounds)
					if (i == _selectedSlotIndex)
					{
						e.Graphics.DrawRectangle(selectedPen, slotRect.X + 1, slotRect.Y + 1, CellSize - 2, CellSize - 2);
					}

					// Tiny index ID printed neatly in the top-left corner of the slot
					e.Graphics.DrawString(i.ToString(), font, idBrush, slotRect.X + 4, slotRect.Y + 3);
				}
			}
		}

		private void GridCanvas_MouseClick(object? sender, MouseEventArgs e)
		{
			int col = e.X / CellSize;
			int row = e.Y / CellSize;

			if (col >= Columns || col < 0 || row < 0) return;

			int slotIndex = row * Columns + col;
			_selectedSlotIndex = slotIndex;

			TileDefinition? tile = null;
			foreach (var t in _tileset!.Tiles)
			{
				if (t.Id == slotIndex) { tile = t; break; }
			}

			if (tile != null)
			{
				LoadTileProperties(tile);
			}
			else
			{
				// Clicked an empty slot! Automatically generate a blank tile assigned to this index location
				var newTile = new TileDefinition
				{
					Id = (ushort)slotIndex,
					Name = $"Tile {slotIndex}",
					Collision = CollisionType.None,
					Primitive = null
				};

				_tileset.Tiles.Add(newTile);
				UpdateGridSize();
				LoadTileProperties(newTile);
				MarkDirty();
			}

			_gridCanvas!.Invalidate();
		}

		private void UpdateGridSize()
		{
			if (_tileset == null || _gridCanvas == null) return;

			int maxTileId = 0;
			foreach (var tile in _tileset.Tiles)
			{
				if (tile.Id > maxTileId)
					maxTileId = tile.Id;
			}

			int currentUsedRows = (_tileset.Tiles.Count == 0) ? 0 : (maxTileId / Columns) + 1;

			// FIX: Find the baseline rows first, THEN add the 4 trailing blank rows.
			int minRows = TotalSlots / Columns;
			int totalRows = Math.Max(minRows, currentUsedRows) + 4;

			int neededHeight = totalRows * CellSize;

			if (_gridCanvas.Height != neededHeight)
			{
				_gridCanvas.Height = neededHeight;
			}
		}
	}

	internal class MeshLoader
	{
		internal static Mesh LoadOBJ(string path)
		{
			var positions = new List<Vec3>();
			var uvs = new List<Vec2>();
			var vertices = new List<Vertex>();
			var indices = new List<ushort>();

			var vertexMap = new Dictionary<(int post, int uv), ushort>();

			foreach (var line in File.ReadLines(path))
			{
				if (line.StartsWith("v "))
				{
					var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					positions.Add(new Vec3(
						float.Parse(parts[1]),
						float.Parse(parts[2]),
						float.Parse(parts[3])
					));
				}
				else if (line.StartsWith("vt "))
				{
					var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					uvs.Add(new Vec2(
						float.Parse(parts[1]),
						1.0f - float.Parse(parts[2])
					));
				}
				else if (line.StartsWith("f "))
				{
					var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

					for (int i = 2; i < parts.Length - 1; i++)
					{
						int baseIndex = indices.Count;

						AddFaceVertex(parts[1], positions, uvs, vertices, indices, vertexMap);
						AddFaceVertex(parts[i], positions, uvs, vertices, indices, vertexMap);
						AddFaceVertex(parts[i + 1], positions, uvs, vertices, indices, vertexMap);

						// Enforce CCW winding with upward-facing normals.
						// Compute the face normal from the three vertices just added.
						var v0 = vertices[indices[baseIndex]].Position;
						var v1 = vertices[indices[baseIndex + 1]].Position;
						var v2 = vertices[indices[baseIndex + 2]].Position;

						var ax = v1.x - v0.x; var ay = v1.y - v0.y; var az = v1.z - v0.z;
						var bx = v2.x - v0.x; var by = v2.y - v0.y; var bz = v2.z - v0.z;

						float ny = az * bx - ax * bz; // Y component of cross product a × b

						// If normal points downward, swap the last two indices to flip winding
						if (ny < 0.0f)
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

		private static void AddFaceVertex(string token, List<Vec3> positions, List<Vec2> uvs, List<Vertex> vertices, List<ushort> indices, Dictionary<(int post, int uv), ushort> vertexMap)
		{
			var parts = token.Split('/');
			int posIndex = int.Parse(parts[0]) - 1;
			int uvIndex = parts.Length > 1 && parts[1] != "" ? int.Parse(parts[1]) - 1 : -1;

			var key = (posIndex, uvIndex);

			if (!vertexMap.TryGetValue(key, out ushort index))
			{
				var v = new Vertex
				{
					Position = positions[posIndex],
					UV = uvIndex >= 0 ? uvs[uvIndex] : Vec2.Zero
				};

				index = (ushort) vertices.Count;
				vertices.Add(v);
				vertexMap[key] = index;
			}

			indices.Add(index);
		}
	}
}
