using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Damascus.Mapper.Theme;
using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Maths;
using Glyphborn.Mapper.Tiles;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Glyphborn.Mapper
{
	// Avalonia port of the WinForms TilesetEditorDialog.
	// Visual language (pens/brushes/glyphs) is copied 1:1 from Damascus.Mapper.Controls.TilesetControl
	// so the grid here looks identical to the read-only tileset picker elsewhere in the app.
	//
	// Usage from a caller (replaces WinForms' `using var dlg = ...; dlg.ShowDialog()`):
	//     var dlg = new TilesetEditorDialog(tileset);
	//     bool? saved = await dlg.ShowDialog<bool?>(ownerWindow);
	public sealed class TilesetEditorDialog : Window
	{
		// ── data ──────────────────────────────────────────────────────────────
		private readonly Tileset _tileset;
		private readonly string? _tilesetPath;
		private bool _isDirty;
		private bool _ignoreChanges;
		private bool _savedSuccessfully;
		private bool _forceClose;

		// ── grid state ────────────────────────────────────────────────────────
		private TileGridCanvas? _gridCanvas;
		private int _selectedSlot = -1;

		private const int CellSize = 32;
		private const int CellPadding = 2;
		private const int Columns = 8;
		private const int PropsRowWidth = 600; // fixed content width used to right-align import buttons

		private int TotalSlots
		{
			get
			{
				int maxId = 0;
				foreach (var t in _tileset.Tiles)
					if (t.Id > maxId) maxId = t.Id;

				int total = Math.Max(_tileset.Tiles.Count, maxId + 32);
				int rows = (int)Math.Ceiling(total / (float)Columns);
				return rows * Columns;
			}
		}

		// ── properties panel controls ─────────────────────────────────────────
		private TextBlock? _slotLabel;
		private TextBox? _nameTextBox;
		private ComboBox? _collisionBox;
		private Border? _previewBorder;
		private Image? _previewImage;
		private Button? _clearSlotButton;

		// ── variant sub-panel controls ────────────────────────────────────────
		private Canvas? _variantPanel;
		private TextBlock? _previewLabel;
		private int _sharedBottomY;

		// =====================================================================
		//  Construction
		// =====================================================================

		public TilesetEditorDialog(Tileset tileset)
		{
			_tileset = tileset;
			_tilesetPath = null;

			Title = $"Edit Tileset: {tileset.Name}";
			Width = 940;
			Height = 660;
			MinWidth = 680;
			MinHeight = 480;
			CanResize = true;
			WindowStartupLocation = WindowStartupLocation.CenterOwner;
			Background = new SolidColorBrush(Color.FromRgb(25, 25, 28));
			Icon = MapperTheme.Icon;

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

		protected override void OnClosing(WindowClosingEventArgs e)
		{
			base.OnClosing(e);

			if (_forceClose)
			{
				DisposePreview();
				return;
			}

			if (_isDirty && !_savedSuccessfully)
			{
				e.Cancel = true;
				_ = ConfirmDiscardAndCloseAsync();
				return;
			}

			DisposePreview();
		}

		private async Task ConfirmDiscardAndCloseAsync()
		{
			bool discard = await ConfirmAsync(
				"Unsaved Changes",
				"You have unsaved changes. Discard them?");

			if (discard)
			{
				_forceClose = true;
				Close(false);
			}
		}

		private void DisposePreview()
		{
			if (_previewImage?.Source is IDisposable bmp)
			{
				bmp.Dispose();
				_previewImage.Source = null;
			}
		}

		// =====================================================================
		//  Top-level layout
		// =====================================================================

		private void BuildUI()
		{
			var root = new DockPanel();

			var bottomBar = BuildBottomBar();
			DockPanel.SetDock(bottomBar, Dock.Bottom);
			root.Children.Add(bottomBar);

			var mainGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("280,*") };

			var gridPanel = BuildGridPanel();
			Grid.SetColumn(gridPanel, 0);

			var propsPanel = BuildPropertiesPanel();
			Grid.SetColumn(propsPanel, 1);

			mainGrid.Children.Add(gridPanel);
			mainGrid.Children.Add(propsPanel);

			root.Children.Add(mainGrid); // last (un-docked) child fills

			Content = root;
		}

		private Canvas BuildBottomBar()
		{
			var bar = new Canvas
			{
				Height = 52,
				Background = new SolidColorBrush(Color.FromRgb(30, 30, 36))
			};

			var saveBtn = MakeButton("Save Tileset", 120);
			var cancelBtn = MakeButton("Cancel", 90);
			saveBtn.Click += SaveTileset_Click;
			cancelBtn.Click += (_, __) => Close(false);

			bar.Children.Add(saveBtn);
			bar.Children.Add(cancelBtn);

			void PositionButtons()
			{
				var w = bar.Bounds.Width > 0 ? bar.Bounds.Width : Width;
				Canvas.SetTop(cancelBtn, 12);
				Canvas.SetLeft(cancelBtn, w - cancelBtn.Width - 12);
				Canvas.SetTop(saveBtn, 12);
				Canvas.SetLeft(saveBtn, w - cancelBtn.Width - 12 - saveBtn.Width - 8);
			}

			bar.SizeChanged += (_, __) => PositionButtons();
			Opened += (_, __) => PositionButtons();

			return bar;
		}

		// =====================================================================
		//  Grid panel (left)
		// =====================================================================

		private Control BuildGridPanel()
		{
			var outer = new DockPanel
			{
				Background = new SolidColorBrush(Color.FromRgb(28, 28, 32))
			};

			var header = new Border
			{
				Height = 32,
				Background = MapperTheme.HeaderBackground,
				Padding = new Thickness(10, 0, 0, 0),
				Child = new TextBlock
				{
					Text = "Tiles",
					VerticalAlignment = VerticalAlignment.Center,
					FontWeight = FontWeight.Bold,
					Foreground = Brushes.White
				}
			};
			DockPanel.SetDock(header, Dock.Top);
			outer.Children.Add(header);

			_gridCanvas = new TileGridCanvas(this)
			{
				Width = Columns * CellSize,
				Height = GetCanvasHeight(),
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top
			};

			var scroll = new ScrollViewer
			{
				Content = _gridCanvas,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				Background = MapperTheme.ContainerBackground,
				Padding = new Thickness(6, 0, 0, 0)
			};
			outer.Children.Add(scroll); // fills remaining space

			return outer;
		}

		private int GetCanvasHeight() => (TotalSlots / Columns) * CellSize;

		private void RefreshCanvasHeight()
		{
			if (_gridCanvas == null) return;
			_gridCanvas.Height = GetCanvasHeight();
			_gridCanvas.InvalidateVisual();
		}

		// ── the grid control itself ──────────────────────────────────────────

		private sealed class TileGridCanvas : Control
		{
			private readonly TilesetEditorDialog _owner;
			private int _hoverSlot = -1;

			// Styling copied from Damascus.Mapper.Controls.TilesetControl to keep the same esthetic.
			private static readonly IPen SelectedPen = new Pen(Brush.Parse("#0E74CA"), 2);
			private static readonly IPen EmptyPen = new Pen(Brush.Parse("#37373A"), 1, dashStyle: DashStyle.DashDot);
			private static readonly IPen BorderPen = new Pen(Brushes.DimGray);
			private static readonly Typeface TileTypeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Normal);

			private static readonly FormattedText PlusGlyph = new FormattedText("+", System.Globalization.CultureInfo.CurrentCulture,
				FlowDirection.LeftToRight, Typeface.Default, 10, Brushes.LightGray);

			public TileGridCanvas(TilesetEditorDialog owner)
			{
				_owner = owner;
				Cursor = new Cursor(StandardCursorType.Hand);
			}

			protected override void OnPointerPressed(PointerPressedEventArgs e)
			{
				base.OnPointerPressed(e);

				var point = e.GetCurrentPoint(this);
				int slot = SlotAt(point.Position);
				if (slot < 0) return;

				if (point.Properties.PointerUpdateKind == PointerUpdateKind.RightButtonPressed)
				{
					ShowSlotMenu(slot);
					return;
				}

				if (!_owner.IsEmptySlot(slot))
					_owner.SelectSlot(slot);
			}

			protected override void OnPointerMoved(PointerEventArgs e)
			{
				base.OnPointerMoved(e);
				int slot = SlotAt(e.GetCurrentPoint(this).Position);
				if (slot == _hoverSlot) return;
				_hoverSlot = slot;
				InvalidateVisual();
			}

			protected override void OnPointerExited(PointerEventArgs e)
			{
				base.OnPointerExited(e);
				_hoverSlot = -1;
				InvalidateVisual();
			}

			private int SlotAt(Point p)
			{
				int col = (int)(p.X / CellSize);
				int row = (int)(p.Y / CellSize);
				if (col < 0 || col >= Columns || row < 0) return -1;
				int slot = row * Columns + col;
				return slot < _owner.TotalSlots ? slot : -1;
			}

			private void ShowSlotMenu(int slot)
			{
				string tileName = "";
				if (slot < _owner._tileset.Tiles.Count)
				{
					var t = _owner._tileset.Tiles[slot];
					if (!string.IsNullOrEmpty(t.Name)) tileName = t.Name;
				}

				string header = slot == 0
					? "Slot 0 — Air (reserved)"
					: $"Slot {slot}  —  {tileName}";

				var menu = new ContextMenu();
				menu.Items.Add(new MenuItem { Header = header, IsEnabled = false });
				menu.Items.Add(new Separator());

				if (_owner.IsEmptySlot(slot))
				{
					menu.Items.Add(MakeMenuItem("Create Generic Tile", () => _owner.CreateTileAtSlot(slot, TileType.TileGeneric)));
					menu.Items.Add(MakeMenuItem("Create Animated Tile", () => _owner.CreateTileAtSlot(slot, TileType.TileAnimated)));
					menu.Items.Add(MakeMenuItem("Create Door Tile", () => _owner.CreateTileAtSlot(slot, TileType.TileEntityDoor)));
					menu.Items.Add(MakeMenuItem("Create Crop Tile", () => _owner.CreateTileAtSlot(slot, TileType.TileEntityCrop)));
				}
				else
				{
					menu.Items.Add(MakeMenuItem("Select", () => _owner.SelectSlot(slot)));

					if (slot != 0)
						menu.Items.Add(MakeMenuItem("Clear Slot", () => _owner.ConfirmClearSlotAsync(slot), Color.FromRgb(250, 128, 114)));
				}

				menu.Open(this);
			}

			private static MenuItem MakeMenuItem(string text, Action onClick, Color? foreground = null)
			{
				var mi = new MenuItem { Header = text };
				if (foreground.HasValue) mi.Foreground = new SolidColorBrush(foreground.Value);
				mi.Click += (_, __) => onClick();
				return mi;
			}

			public override void Render(DrawingContext context)
			{
				base.Render(context);

				int total = _owner.TotalSlots;

				for (int i = 0; i < total; i++)
				{
					int col = i % Columns;
					int row = i / Columns;

					var slotRect = new Rect((col * CellSize), row * CellSize, CellSize, CellSize);
					var drawRect = slotRect.Deflate(CellPadding);

					TileDefinition? tile = i < _owner._tileset.Tiles.Count ? _owner._tileset.Tiles[i] : null;
					bool isEmpty = _owner.IsEmptySlot(i);

					if (!isEmpty && tile != null)
					{
						if (i == _hoverSlot && i != _owner._selectedSlot)
							context.FillRectangle(new SolidColorBrush(Colors.White, 0.12), drawRect);

						if (i == 0 || tile.TileType == TileType.None)
						{
							context.FillRectangle(MapperTheme.ContainerBackground, drawRect);
						}
						else
						{
							var primitive = tile.GetPrimitives().FirstOrDefault();
							if (primitive?.Texture != null)
							{
								var thumb = TextureToBitmap(primitive.Texture);
								context.DrawImage(thumb, drawRect);
							}
						}

						context.DrawRectangle(null, BorderPen, drawRect);

						if (!string.IsNullOrEmpty(tile.Name))
						{
							var text = new FormattedText(
								tile.Name,
								System.Globalization.CultureInfo.CurrentCulture,
								FlowDirection.LeftToRight,
								TileTypeface,
								9.0,
								Brushes.LightGray)
							{
								MaxTextWidth = drawRect.Width - 4,
								Trimming = TextTrimming.CharacterEllipsis
							};
							context.DrawText(text, new Point(drawRect.X + 2, drawRect.Bottom - 12));
						}
					}
					else
					{
						context.DrawRectangle(MapperTheme.ContainerBackground, EmptyPen, drawRect);

						var centerX = drawRect.X + (drawRect.Width / 2);
						var centerY = drawRect.Y + (drawRect.Height / 2);
						context.DrawText(PlusGlyph, new Point(centerX + 4, centerY));
					}

					var index = new FormattedText(i.ToString(), System.Globalization.CultureInfo.CurrentCulture,
						FlowDirection.LeftToRight, Typeface.Default, 10, Brushes.LightGray);
					context.DrawText(index, new Point(drawRect.X + 2, drawRect.Y + 2));

					if (i == _owner._selectedSlot)
					{
						context.DrawRectangle(null, SelectedPen, drawRect.Inflate(1));
					}
				}
			}
		}

		// ── slot operations ───────────────────────────────────────────────────

		private void SelectSlot(int slot)
		{
			_selectedSlot = slot;
			_gridCanvas?.InvalidateVisual();
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

			_tileset.Tiles.Add(tile);

			_selectedSlot = slot;
			RefreshCanvasHeight();
			SelectSlot(slot);
			MarkDirty();
		}

		private async void ConfirmClearSlotAsync(int slot)
		{
			if (slot <= 0 || slot >= _tileset.Tiles.Count) return;

			bool confirmed = await ConfirmAsync(
				"Clear Slot",
				$"Clear slot {slot} '{_tileset.Tiles[slot].Name}'?\n\n" +
				"The slot index will be preserved; its mesh and texture data will be removed.");

			if (!confirmed) return;

			_tileset.Tiles[slot] = new TileDefinition
			{
				Id = (ushort)slot,
				Name = "",
				Collision = CollisionType.None
			};

			while (_tileset.Tiles.Count > 1 && IsEmptySlot(_tileset.Tiles.Count - 1))
			{
				_tileset.Tiles.RemoveAt(_tileset.Tiles.Count - 1);
			}

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

		private Control BuildPropertiesPanel()
		{
			var canvas = new Canvas { Background = new SolidColorBrush(Color.FromRgb(40, 40, 50)) };

			int y = 12;

			Place(canvas, new TextBlock
			{
				Text = "Tile Properties",
				FontSize = 14,
				FontWeight = FontWeight.Bold,
				Foreground = Brushes.White
			}, 12, y, 370, 26);
			y += 34;

			_slotLabel = new TextBlock { Text = "No tile selected", Foreground = new SolidColorBrush(Color.FromRgb(120, 120, 145)) };
			Place(canvas, _slotLabel, 12, y, 370, 20);
			y += 28;

			Place(canvas, HRule(), 12, y);
			y += 14;

			Place(canvas, PropLabel("Name:"), 12, y);
			_nameTextBox = new TextBox { Width = 270, IsEnabled = false };
			_nameTextBox.TextChanged += (_, __) => PropertyChanged();
			Place(canvas, _nameTextBox, 100, y - 2);
			y += 34;

			Place(canvas, PropLabel("Collision:"), 12, y);
			_collisionBox = new ComboBox
			{
				Width = 270,
				ItemsSource = Enum.GetValues(typeof(CollisionType)),
				IsEnabled = false
			};
			_collisionBox.SelectionChanged += (_, __) => PropertyChanged();
			Place(canvas, _collisionBox, 100, y - 2);
			y += 42;

			Place(canvas, HRule(), 12, y);
			y += 14;

			// ── THE ACCORDION ZONE STARTS RIGHT HERE NOW ──
			_variantPanel = new Canvas { Height = 0 };
			Place(canvas, _variantPanel, 0, y);

			_sharedBottomY = y;

			_previewLabel = PropLabel("Preview:");
			Place(canvas, _previewLabel, 12, y);
			y += 22;

			_previewImage = new Image { Stretch = Stretch.Uniform };
			_previewBorder = new Border
			{
				Width = 128,
				Height = 128,
				BorderBrush = Brushes.Gray,
				BorderThickness = new Thickness(1),
				Background = new SolidColorBrush(Color.FromRgb(28, 28, 36)),
				Child = _previewImage
			};
			Place(canvas, _previewBorder, 12, y);
			y += 144;

			_clearSlotButton = new Button
			{
				Content = "Clear Slot",
				Width = 115,
				Height = 30,
				Background = new SolidColorBrush(Color.FromRgb(150, 38, 38)),
				Foreground = Brushes.White,
				BorderBrush = new SolidColorBrush(Color.FromRgb(100, 28, 28)),
				BorderThickness = new Thickness(1),
				IsEnabled = false
			};
			_clearSlotButton.Click += (_, __) =>
			{
				if (_selectedSlot > 0)
					ConfirmClearSlotAsync(_selectedSlot);
			};
			Place(canvas, _clearSlotButton, 12, y);

			return new ScrollViewer
			{
				Content = canvas,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto
			};
		}

		private int BuildPrimitiveControlsRow(TileDefinition tile, RenderPrimitive? primitive, int startY, int primitiveIndex = 0)
		{
			int y = startY;

			// 1. Mesh row
			string meshName = primitive?.MeshSourcePath != null ? Path.GetFileName(primitive.MeshSourcePath) : "(none)";
			var meshLabel = PropLabel($"Mesh [{primitiveIndex}]: {meshName}");
			Place(_variantPanel!, meshLabel, 12, y, 230, 20);

			var meshBtn = MakeButton("Import OBJ", 105);
			meshBtn.Click += (_, __) => DynamicImportMesh(tile, primitiveIndex);
			Place(_variantPanel!, meshBtn, PropsRowWidth - 117, y - 2);
			y += 32;

			// 2. Texture row
			string texName = primitive?.TextureSourcePath != null ? Path.GetFileName(primitive.TextureSourcePath) : "(none)";
			var texLabel = PropLabel($"Texture [{primitiveIndex}]: {texName}");
			Place(_variantPanel!, texLabel, 12, y, 230, 20);

			var texBtn = MakeButton("Import Tex", 105);
			texBtn.IsEnabled = primitive?.Mesh != null; // only allow texturing if a mesh is present
			texBtn.Click += (_, __) => DynamicImportTexture(tile, primitiveIndex);
			Place(_variantPanel!, texBtn, PropsRowWidth - 117, y - 2);
			y += 42;

			return y - startY;
		}

		private void AdjustLowerLayout(int variantPanelHeight)
		{
			if (_variantPanel == null || _previewLabel == null || _previewBorder == null || _clearSlotButton == null)
				return;

			_variantPanel.Height = variantPanelHeight;

			int currentY = _sharedBottomY + variantPanelHeight;
			if (variantPanelHeight > 0) currentY += 14;

			MoveTo(_previewLabel, 12, currentY);
			currentY += 22;

			MoveTo(_previewBorder, 12, currentY);
			currentY += 144;

			MoveTo(_clearSlotButton, 12, currentY);
		}

		private void LoadTileProperties(TileDefinition tile)
		{
			if (_variantPanel == null || _slotLabel == null) return;

			_ignoreChanges = true;

			bool isEmpty = IsEmptySlot(tile.Id);
			bool isAir = tile.Id == 0;

			_slotLabel.Text = isAir ? "Slot 0  —  Air (reserved)"
							: isEmpty ? $"Slot {tile.Id}  —  (empty)"
							: $"Slot {tile.Id}";

			_nameTextBox!.Text = tile.Name;
			_nameTextBox.IsEnabled = !isAir;

			_collisionBox!.SelectedItem = tile.Collision;
			_collisionBox.IsEnabled = !isAir;

			DisposePreview();
			var visual = tile.GetPrimitives().FirstOrDefault();
			_previewImage!.Source = visual?.Texture != null ? TextureToBitmap(visual.Texture) : null;

			_clearSlotButton!.IsEnabled = !isAir;

			_variantPanel.Children.Clear();
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
			_nameTextBox.IsEnabled = false;
			_collisionBox!.IsEnabled = false;

			DisposePreview();

			_clearSlotButton!.IsEnabled = false;

			_variantPanel!.Children.Clear();
			AdjustLowerLayout(0);

			_ignoreChanges = false;
		}

		private int PopulateGenericProperties(TileGeneric tile) => BuildPrimitiveControlsRow(tile, tile.Primitive, 0, 0);

		private int PopulateAnimatedProperties(TileAnimated tile)
		{
			int y = 0;
			Place(_variantPanel!, PropLabel("Framerate:"), 12, y);

			var input = new NumericUpDown { Width = 270, Minimum = 0, Maximum = 255, Value = tile.FrameRate };
			input.ValueChanged += (_, __) => PropertyChanged();
			Place(_variantPanel!, input, 100, y - 2);
			y += 42;

			y += BuildPrimitiveControlsRow(tile, tile.Primitive, y, 0);
			return y;
		}

		private int PopulateDoorProperties(TileEntityDoor tile)
		{
			int y = 0;
			Place(_variantPanel!, PropLabel("Framerate:"), 12, y);

			var fpsInput = new NumericUpDown { Width = 270, Minimum = 0, Maximum = 255, Value = tile.FrameRate };
			fpsInput.ValueChanged += (_, __) => PropertyChanged();
			Place(_variantPanel!, fpsInput, 100, y - 2);
			y += 32;

			Place(_variantPanel!, PropLabel("Open Preview:"), 12, y);
			var check = new CheckBox { IsChecked = tile.OpenState };
			check.IsCheckedChanged += (_, __) => PropertyChanged();
			Place(_variantPanel!, check, 100, y - 4);
			y += 42;

			for (int i = 0; i < tile.Primitives.Count; i++)
				y += BuildPrimitiveControlsRow(tile, tile.Primitives[i], y, i);

			var addLayerBtn = MakeButton("+ Add Primitive Layer", 358);
			addLayerBtn.Click += (_, __) => { tile.Primitives.Add(null!); LoadTileProperties(tile); MarkDirty(); };
			Place(_variantPanel!, addLayerBtn, 12, y);
			y += 38;

			return y;
		}

		private int PopulateCropProperties(TileEntityCrop tile)
		{
			int y = 0;
			Place(_variantPanel!, PropLabel("Growth Rate:"), 12, y);

			var input = new NumericUpDown { Width = 270, Minimum = 0, Maximum = ushort.MaxValue, Value = tile.GrowthRate };
			input.ValueChanged += (_, __) => PropertyChanged();
			Place(_variantPanel!, input, 100, y - 2);
			y += 42;

			for (int i = 0; i < tile.Primitives.Count; i++)
				y += BuildPrimitiveControlsRow(tile, tile.Primitives[i], y, i);

			var addLayerBtn = MakeButton("+ Add Growth Stage Layer", 358);
			addLayerBtn.Click += (_, __) => { tile.Primitives.Add(null!); LoadTileProperties(tile); MarkDirty(); };
			Place(_variantPanel!, addLayerBtn, 12, y);
			y += 38;

			return y;
		}

		private new void PropertyChanged()
		{
			if (_ignoreChanges || _selectedSlot < 0 || _selectedSlot >= _tileset.Tiles.Count)
				return;

			var tile = _tileset.Tiles[_selectedSlot];
			tile.Name = _nameTextBox!.Text ?? "";

			if (_collisionBox!.SelectedItem is CollisionType collision)
				tile.Collision = collision;

			_gridCanvas?.InvalidateVisual();
			MarkDirty();
		}

		// =====================================================================
		//  Mesh / Texture import
		// =====================================================================

		private IStorageProvider? GetStorageProvider() => TopLevel.GetTopLevel(this)?.StorageProvider;

		private async void DynamicImportMesh(TileDefinition tile, int index)
		{
			var storage = GetStorageProvider();
			if (storage == null) return;

			var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = $"Import Mesh for Primitive [{index}]",
				AllowMultiple = false,
				FileTypeFilter = new[] { new FilePickerFileType("OBJ Files") { Patterns = new[] { "*.obj" } } }
			});

			var file = files.FirstOrDefault();
			if (file == null) return;

			try
			{
				var mesh = MeshLoader.LoadOBJ(file.Path.LocalPath);

				var existingPrimitives = tile.GetPrimitives().ToList();
				RenderPrimitive? currentPrim = index < existingPrimitives.Count ? existingPrimitives[index] : null;
				var existingTex = currentPrim?.Texture ?? new Texture(1, 1, new uint[] { 0x00000000 });

				var newVisual = new RenderPrimitive(mesh, existingTex)
				{
					MeshSourcePath = file.Path.LocalPath,
					TextureSourcePath = currentPrim?.TextureSourcePath
				};

				ApplyVisual(tile, index, newVisual);

				LoadTileProperties(tile);
				_gridCanvas?.InvalidateVisual();
				MarkDirty();
			}
			catch (Exception ex)
			{
				await ShowErrorAsync("Error", $"Failed to import mesh:\n{ex.Message}");
			}
		}

		private async void DynamicImportTexture(TileDefinition tile, int index)
		{
			var storage = GetStorageProvider();
			if (storage == null) return;

			var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = $"Import Texture for Primitive [{index}]",
				AllowMultiple = false,
				FileTypeFilter = new[] { new FilePickerFileType("Image Files") { Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp" } } }
			});

			var file = files.FirstOrDefault();
			if (file == null) return;

			try
			{
				Texture texture;
				await using (var stream = await file.OpenReadAsync())
				using (var bmp = new Bitmap(stream))
				{
					texture = BitmapToTexture(bmp);
				}

				if (tile is TileEntity entity)
				{
					var currentMesh = entity.Primitives[index].Mesh;
					var currentMeshPath = entity.Primitives[index].MeshSourcePath;
					entity.Primitives[index] = new RenderPrimitive(currentMesh, texture)
					{
						MeshSourcePath = currentMeshPath,
						TextureSourcePath = file.Path.LocalPath
					};
				}
				else if (tile is TileGeneric generic && generic.Primitive != null)
				{
					generic.Primitive = new RenderPrimitive(generic.Primitive.Mesh, texture)
					{
						MeshSourcePath = generic.Primitive.MeshSourcePath,
						TextureSourcePath = file.Path.LocalPath
					};
				}
				else if (tile is TileAnimated animated && animated.Primitive != null)
				{
					animated.Primitive = new RenderPrimitive(animated.Primitive.Mesh, texture)
					{
						MeshSourcePath = animated.Primitive.MeshSourcePath,
						TextureSourcePath = file.Path.LocalPath
					};
				}

				LoadTileProperties(tile);
				_gridCanvas?.InvalidateVisual();
				MarkDirty();
			}
			catch (Exception ex)
			{
				await ShowErrorAsync("Error", $"Failed to import texture:\n{ex.Message}");
			}
		}

		private static void ApplyVisual(TileDefinition tile, int index, RenderPrimitive visual)
		{
			if (tile is TileEntity entity)
			{
				if (index < entity.Primitives.Count) entity.Primitives[index] = visual;
				else entity.Primitives.Add(visual);
			}
			else if (tile is TileGeneric generic) generic.Primitive = visual;
			else if (tile is TileAnimated animated) animated.Primitive = visual;
		}

		// =====================================================================
		//  Save
		// =====================================================================

		private void SaveTileset_Click(object? sender, RoutedEventArgs e)
		{
			try
			{
				TilesetSerializer.SaveBinary(_tileset);
				_isDirty = false;
				_savedSuccessfully = true;
				Close(true);
			}
			catch (Exception ex)
			{
				_ = ShowErrorAsync("Error", $"Failed to save tileset:\n{ex.Message}");
			}
		}

		// =====================================================================
		//  Minimal modal helpers (placeholder for MessageBox — swap for your
		//  app's existing dialog helper if you already have one)
		// =====================================================================

		private async Task<bool> ConfirmAsync(string title, string message)
		{
			var dialog = new Window
			{
				Title = title,
				Width = 380,
				SizeToContent = SizeToContent.Height,
				CanResize = false,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				Background = new SolidColorBrush(Color.FromRgb(30, 30, 36))
			};

			bool result = false;

			var yesBtn = MakeButton("Yes", 90);
			var noBtn = MakeButton("No", 90);
			yesBtn.Click += (_, __) => { result = true; dialog.Close(); };
			noBtn.Click += (_, __) => { result = false; dialog.Close(); };

			var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
			buttons.Children.Add(yesBtn);
			buttons.Children.Add(noBtn);

			var layout = new StackPanel { Margin = new Thickness(20), Spacing = 20 };
			layout.Children.Add(new TextBlock { Text = message, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
			layout.Children.Add(buttons);

			dialog.Content = layout;

			await dialog.ShowDialog(this);
			return result;
		}

		private async Task ShowErrorAsync(string title, string message)
		{
			var dialog = new Window
			{
				Title = title,
				Width = 380,
				SizeToContent = SizeToContent.Height,
				CanResize = false,
				WindowStartupLocation = WindowStartupLocation.CenterOwner,
				Background = new SolidColorBrush(Color.FromRgb(30, 30, 36))
			};

			var okBtn = MakeButton("OK", 90);
			okBtn.HorizontalAlignment = HorizontalAlignment.Right;
			okBtn.Click += (_, __) => dialog.Close();

			var layout = new StackPanel { Margin = new Thickness(20), Spacing = 20 };
			layout.Children.Add(new TextBlock { Text = message, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
			layout.Children.Add(okBtn);

			dialog.Content = layout;

			await dialog.ShowDialog(this);
		}

		// =====================================================================
		//  Helpers
		// =====================================================================

		private void MarkDirty() => _isDirty = true;

		private static void Place(Canvas parent, Control control, double x, double y, double? width = null, double? height = null)
		{
			if (width.HasValue) control.Width = width.Value;
			if (height.HasValue) control.Height = height.Value;
			Canvas.SetLeft(control, x);
			Canvas.SetTop(control, y);
			parent.Children.Add(control);
		}

		private static void MoveTo(Control control, double x, double y)
		{
			Canvas.SetLeft(control, x);
			Canvas.SetTop(control, y);
		}

		private static Button MakeButton(string text, double width)
		{
			return new Button
			{
				Content = text,
				Width = width,
				Height = 28,
				Background = new SolidColorBrush(Color.FromRgb(52, 52, 64)),
				Foreground = Brushes.White,
				BorderBrush = new SolidColorBrush(Color.FromRgb(68, 68, 82)),
				BorderThickness = new Thickness(1)
			};
		}

		private static TextBlock PropLabel(string text) => new TextBlock
		{
			Text = text,
			Width = 86,
			Height = 20,
			Foreground = Brushes.White
		};

		private static Border HRule() => new Border
		{
			Width = 370,
			Height = 1,
			Background = new SolidColorBrush(Color.FromRgb(58, 58, 72))
		};

		private static Texture BitmapToTexture(Bitmap bmp)
		{
			int w = bmp.PixelSize.Width;
			int h = bmp.PixelSize.Height;
			int stride = w * 4;
			var buffer = new byte[stride * h];

			var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
			try
			{
				bmp.CopyPixels(new PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), buffer.Length, stride);
			}
			finally
			{
				handle.Free();
			}

			var pixels = new uint[w * h];
			for (int py = 0; py < h; py++)
			{
				for (int px = 0; px < w; px++)
				{
					int src = py * stride + px * 4;
					// Assumes Bgra8888 byte order from CopyPixels — verify against your Avalonia version.
					byte b = buffer[src + 0];
					byte g = buffer[src + 1];
					byte r = buffer[src + 2];
					byte a = buffer[src + 3];
					pixels[py * w + px] = ((uint)a << 24) | ((uint)r << 16) | ((uint)g << 8) | b;
				}
			}

			return new Texture(w, h, pixels);
		}

		private static WriteableBitmap TextureToBitmap(Texture tex)
		{
			var wb = new WriteableBitmap(
				new PixelSize(tex.Width, tex.Height),
				new Vector(96, 96),
				PixelFormat.Bgra8888,
				AlphaFormat.Unpremul);

			using var fb = wb.Lock();
			int stride = fb.RowBytes;
			var row = new byte[stride];

			for (int y = 0; y < tex.Height; y++)
			{
				Array.Clear(row, 0, row.Length);
				for (int x = 0; x < tex.Width; x++)
				{
					uint p = tex.Pixels[y * tex.Width + x];
					int o = x * 4;
					row[o + 0] = (byte)p;          // B
					row[o + 1] = (byte)(p >> 8);    // G
					row[o + 2] = (byte)(p >> 16);   // R
					row[o + 3] = (byte)(p >> 24);   // A
				}
				Marshal.Copy(row, 0, fb.Address + y * stride, stride);
			}

			return wb;
		}
	}

	// =========================================================================
	//  MeshLoader — unchanged from original (no WinForms dependency)
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
			List<Vec3> positions,
			List<Vec2> uvs,
			List<Vertex> vertices,
			List<ushort> indices,
			Dictionary<(int, int), ushort> vertexMap)
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