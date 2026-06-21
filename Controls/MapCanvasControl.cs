using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Damascus.Mapper.Theme;
using Glyphborn.Mapper.Editor;
using Glyphborn.Mapper.Tiles;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Damascus.Mapper.Controls
{
	public sealed class MapCanvasControl : Control
	{
		public AreaDocument? AreaDocument;
		public MapDocument? MapDocument;
		public EditorState? State;

		private bool _isPainting;
		private bool _isErasing;
		private MapEdge _hoverEdge = MapEdge.Inside;

		private readonly HashSet<long> _createMapsThisDrag = new();

		private bool _edgeCreateTriggered;

		private static Pen MapEdgePen = new Pen(Brushes.Crimson, 3);
		private static Pen MapGridPen = new Pen(Brushes.White);

		public MapCanvasControl() { }

		private int ComputeTileSize()
		{
			if (MapDocument == null)
				return 1;

			double sizeX = Bounds.Width / MapDocument.WIDTH;
			double sizeY = (Bounds.Height / MapDocument.HEIGHT) - 2;

			int result = (int)Math.Max(1, Math.Min(sizeX, sizeY));

			return result;
		}

		public override void Render(DrawingContext context)
		{
			base.Render(context);

			context.DrawRectangle(MapperTheme.ContainerBackground, null, new Rect(0, 0, Bounds.Width, Bounds.Height));

			if (MapDocument == null || State == null || AreaDocument == null) return;

			int tileSize = ComputeTileSize();

			int mapW = MapDocument.WIDTH * tileSize;
			int mapH = MapDocument.HEIGHT * tileSize;

			int ox = (int)(Bounds.Width - mapW) / 2;
			int oy = (int)(Bounds.Height - mapH) / 2;

			for (int ty = -1; ty <= MapDocument.HEIGHT; ty++)
			{
				for (int tx = -1; tx <= MapDocument.WIDTH; tx++)
				{
					int mapOffsetX = 0;
					int mapOffsetY = 0;
					int srcX = tx;
					int srcY = ty;

					if (tx < 0)
					{
						mapOffsetX = -1;
						srcX = MapDocument.WIDTH - 1;
					}
					else if (tx >= MapDocument.WIDTH)
					{
						mapOffsetX = 1;
						srcX = 0;
					}

					if (ty < 0)
					{
						mapOffsetY = -1;
						srcY = MapDocument.HEIGHT - 1;
					}
					else if (ty >= MapDocument.HEIGHT)
					{
						mapOffsetY = 1;
						srcY = 0;
					}

					MapDocument? srcMap = MapDocument;
					if (mapOffsetX != 0 || mapOffsetY != 0)
					{
						int neighborMapX = State.ActiveMapX + mapOffsetX;
						int neighborMapY = State.ActiveMapY + mapOffsetY;

						if (!AreaDocument.HasMap(neighborMapX, neighborMapY))
							continue;

						srcMap = AreaDocument.GetMap(neighborMapX, neighborMapY);
						if (srcMap == null)
							continue;
					}

					for (byte layer = 0; layer < State.CurrentLayer; layer++)
					{
						float distance = State.CurrentLayer - layer;
						float fadeStart = 0.0f;
						float fadeRange = 8.0f;
						float alpha;
						if (distance <= fadeStart) alpha = 1.0f;
						else
						{
							float t = (distance - fadeStart) / fadeRange;
							alpha = 1.0f - Math.Clamp(t, 0.25f, 1f);
						}

						DrawTile(context, srcMap, srcX, srcY, tileSize, ox, oy, layer, alpha);
					}

					DrawTile(context, srcMap, srcX, srcY, tileSize, ox, oy, State.CurrentLayer, 1.0f);
				}
			}

			if (State.ShowGrid)
				DrawGrid(context, tileSize, ox, oy);

			if (_hoverEdge != MapEdge.Inside)
				DrawEdgeHighlight(context, tileSize, ox, oy);
		}

		private void DrawTile(DrawingContext context, MapDocument srcMap, int srcX, int srcY, int tileSize, int ox, int oy, int layer, float alpha = 1.0f)
		{
			if (srcX < 0 || srcY < 0 || srcX >= MapDocument.WIDTH || srcY >= MapDocument.HEIGHT) return;

			var tileRef = srcMap.Tiles[layer][srcY][srcX];
			if (tileRef.TileId == 0) return;
			if (tileRef.Tileset >= AreaDocument!.Tilesets.Count) return;

			var tileset = AreaDocument.Tilesets[tileRef.Tileset];

			TileDefinition? def = null;

			foreach (var tile in tileset.Tiles)
			{
				if (tile.Id == tileRef.TileId)
				{
					def = tile;
					break;
				}
			}

			if (def == null || def.TileType == TileType.None) return;

			int px, py;
			if (ReferenceEquals(srcMap, MapDocument))
			{
				px = ox + srcX * tileSize;
				py = oy + srcY * tileSize;
			}
			else
			{
				int neighborMapX = 0, neighborMapY = 0;
				bool found = false;
				for (int mx = 0; mx < AreaDocument.Maps.GetLength(0) && !found; mx++)
					for (int my = 0; my < AreaDocument.Maps.GetLength(1) && !found; my++)
						if (AreaDocument.Maps[mx, my] == srcMap)
						{ neighborMapX = mx; neighborMapY = my; found = true; }

				if (!found) return;

				int dx = neighborMapX - State!.ActiveMapX;
				int dy = neighborMapY - State.ActiveMapY;
				px = ox + (dx * MapDocument.WIDTH + srcX) * tileSize;
				py = oy + (dy * MapDocument.HEIGHT + srcY) * tileSize;
			}

			var dest = new Rect(px, py, tileSize, tileSize);
			var preview = TilePreviewer.GetPreview(def.GetPrimitives().FirstOrDefault()!.Texture);

			bool hasAlpha = alpha < 1.0f;
			IDisposable? opacityState = hasAlpha ? context.PushOpacity(alpha) : null;

			try
			{
				context.DrawImage(preview, new Rect(0, 0, preview.Size.Width, preview.Size.Height), dest);
			}
			finally
			{
				opacityState?.Dispose();
			}

			if (State!.Tool == Tool.RoomBuilder && tileRef.RoomID.HasValue)
			{
				uint roomId = tileRef.RoomID.Value;
				var roomDef = AreaDocument?.Rooms.FirstOrDefault(r => r.Id == roomId);

				// Generate base color (Using Avalonia's Color.FromRgb)
				var roomColor = roomDef?.Color ?? Color.FromRgb(
					(byte)((roomId * 2654435761u >> 16) & 0xFF),
					(byte)((roomId * 2654435761u >> 8) & 0xFF),
					(byte)((roomId * 2654435761u) & 0xFF));

				// Map 0-255 byte alpha to 0.0-1.0 double opacity for Avalonia
				double overlayOpacity = (roomId == State.CurrentRoomID) ? (140 / 255.0) : (70 / 255.0);

				var brush = new SolidColorBrush(roomColor);

				using (context.PushOpacity(overlayOpacity))
				{
					context.FillRectangle(brush, dest);
				}

				// ID label on larger tile sizes only
				if (tileSize >= 12)
				{
					var typeface = new Typeface("Segoe UI", FontStyle.Normal, FontWeight.Bold);
					var formattedText = new FormattedText(
						roomId.ToString(),
						System.Globalization.CultureInfo.CurrentCulture,
						FlowDirection.LeftToRight,
						typeface,
						Math.Max(6.0, tileSize / 5.0),
						Brushes.White);

					context.DrawText(formattedText, new Point(px + 2, py + 2));
				}
			}
		}

		private void DrawGrid(DrawingContext context, int tileSize, int ox, int oy)
		{
			int mapW = MapDocument.WIDTH * tileSize;
			int mapH = MapDocument.HEIGHT * tileSize;

			for (int x = -1; x <= MapDocument.WIDTH + 1; x++)
			{
				int px = ox + x * tileSize;

				if (x == 0 || x == 32)
				{
					context.DrawLine(MapEdgePen, new Point(px, oy - tileSize), new Point(px, oy + mapH + tileSize));
				}
				else
				{
					context.DrawLine(MapGridPen, new Point(px, oy - tileSize), new Point(px, oy + mapH + tileSize));
				}
			}

			for (int y = -1; y <= MapDocument.HEIGHT + 1; y++)
			{
				int py = oy + y * tileSize;

				if (y == 0 || y == 32)
				{
					context.DrawLine(MapEdgePen, new Point(ox - tileSize, py), new Point(ox + mapW + tileSize, py));
				}
				else
				{
					context.DrawLine(MapGridPen, new Point(ox - tileSize, py), new Point(ox + mapW + tileSize, py));
				}
			}
		}

		private void DrawEdgeHighlight(DrawingContext context, int tileSize, int ox, int oy)
		{
			var brush = new SolidColorBrush(Color.FromArgb(80, 30, 144, 255)); // Equivalent to DodgerBlue

			Rect? rect = _hoverEdge switch
			{
				MapEdge.North => new Rect(ox, oy - tileSize, MapDocument.WIDTH * tileSize, tileSize),
				MapEdge.South => new Rect(ox, oy + MapDocument.HEIGHT * tileSize, MapDocument.WIDTH * tileSize, tileSize),
				MapEdge.West => new Rect(ox - tileSize, oy, tileSize, MapDocument.HEIGHT * tileSize),
				MapEdge.East => new Rect(ox + MapDocument.WIDTH * tileSize, oy, tileSize, MapDocument.HEIGHT * tileSize),

				MapEdge.NorthWest => new Rect(ox - tileSize, oy - tileSize, tileSize, tileSize),
				MapEdge.NorthEast => new Rect(ox + MapDocument.WIDTH * tileSize, oy - tileSize, tileSize, tileSize),
				MapEdge.SouthWest => new Rect(ox - tileSize, oy + MapDocument.HEIGHT * tileSize, tileSize, tileSize),
				MapEdge.SouthEast => new Rect(ox + MapDocument.WIDTH * tileSize, oy + MapDocument.HEIGHT * tileSize, tileSize, tileSize),

				_ => null
			};

			if (rect != null)
				context.DrawRectangle(brush, null, rect.Value);
		}

		protected override void OnPointerPressed(PointerPressedEventArgs e)
		{
			base.OnPointerPressed(e);

			_createMapsThisDrag.Clear();
			_edgeCreateTriggered = false;

			MapDocument?.BeginBatch();

			var point = e.GetCurrentPoint(this);

			if (point.Properties.IsLeftButtonPressed)
			{
				_isPainting = true;
				_isErasing = false;
				PaintTileAtMouse(point.Position, _isErasing);
			}
			else if (point.Properties.IsRightButtonPressed)
			{
				_isPainting = true;
				_isErasing = true;
				PaintTileAtMouse(point.Position, _isErasing);
			}
			else if (point.Properties.IsMiddleButtonPressed)
			{
				_isPainting = false;
				_isErasing = false;
				GetTileFromMouse(point.Position, out int tileX, out int tileY);

				if (State!.Tool == Tool.RoomBuilder)
				{
					MapDocument?.FloodFillRoomID(State.CurrentLayer, tileX, tileY, State.CurrentRoomID);
				}
				else
				{
					if (State.SelectedTile == null) return;

					var sel = State?.SelectedTile!.Value;
					var tile = new TileRef
					{
						Tileset = sel?.TilesetIndex ?? 0,
						TileId = sel?.TileIndex ?? 0
					};

					MapDocument?.FloodFill(State!.CurrentLayer, tileX, tileY, tile);
				}
			}
		}

		protected override void OnPointerMoved(PointerEventArgs e)
		{
			base.OnPointerMoved(e);

			MapEdge edge = MapEdge.Inside;

			var point = e.GetCurrentPoint(this);

			if (MapDocument != null)
			{
				GetTileFromMouse(point.Position, out int tileX, out int tileY);
				edge = ResolveEdge(tileX, tileY);
			}

			_hoverEdge = edge;

			InvalidateVisual();

			if (_isPainting)
			{
				PaintTileAtMouse(point.Position, erase: _isErasing);
			}
		}

		protected override void OnPointerReleased(PointerReleasedEventArgs e)
		{
			MapDocument?.EndBatch();
			base.OnPointerReleased(e);

			_isPainting = false;

			_createMapsThisDrag.Clear();
			_edgeCreateTriggered = false;
		}

		private void PaintTileAtMouse(Point position, bool erase)
		{
			if (MapDocument == null || State == null) return;

			if (State.Tool == Tool.MapBuilder && State.SelectedTile == null) return;

			GetTileFromMouse(position, out int tileX, out int tileY);
			PaintTile(tileX, tileY, erase);

			InvalidateVisual();
		}

		private void PaintTile(int tileX, int tileY, bool erase)
		{
			if (AreaDocument == null || State == null) return;

			int mapX = State.ActiveMapX;
			int mapY = State.ActiveMapY;

			var edge = ResolveEdge(tileX, tileY);

			MapDocument? newMap = null;

			if (edge != MapEdge.Inside)
			{
				Redirect(edge, ref mapX, ref mapY, ref tileX, ref tileY);

				long key = ((long)mapX << 32) | (uint)mapY;

				if (AreaDocument.HasMap(mapX, mapY))
				{
					newMap = AreaDocument.GetMap(mapX, mapY);
				}
				else if (!_createMapsThisDrag.Contains(key) && !_edgeCreateTriggered)
				{
					newMap = AreaDocument.GetOrCreateMap(mapX, mapY);
					_createMapsThisDrag.Add(key);
					_edgeCreateTriggered = true;
				}
				else
				{
					newMap = AreaDocument.GetMap(mapX, mapY);
				}

				if (newMap == null)
					return;
			}

			var target = MapDocument!;
			if (newMap != null)
				target = newMap;
			else
				target = MapDocument!;

			if (target.IsGhost) return;

			if (State.Tool == Tool.RoomBuilder)
			{
				uint? roomId = erase ? null : State.CurrentRoomID;
				target.SetTileRoomID(State.CurrentLayer, tileX, tileY, roomId);
				return;
			}

			if (erase)
				target.SetTile(State.CurrentLayer, tileX, tileY, default);
			else
			{
				var sel = State.SelectedTile!.Value;

				target.SetTile(State.CurrentLayer, tileX, tileY, new TileRef
				{
					Tileset = sel.TilesetIndex,
					TileId = sel.TileIndex,
					RoomID = null
				});
			}

			target.IsDirty = true;
		}

		private void GetTileFromMouse(Point position, out int tileX, out int tileY)
		{
			if (MapDocument == null)
			{
				tileX = tileY = 0;
				return;
			}

			int mouseX = (int)position.X;
			int mouseY = (int)position.Y;

			int tileSize = ComputeTileSize();
			int mapW = MapDocument.WIDTH * tileSize;
			int mapH = MapDocument.HEIGHT * tileSize;
			int ox = (int)(Bounds.Width - mapW) / 2;
			int oy = (int)(Bounds.Height - mapH) / 2;

			double fx = (mouseX - ox) / (double)tileSize;
			double fy = (mouseY - oy) / (double)tileSize;

			tileX = (int)Math.Floor(fx);
			tileY = (int)Math.Floor(fy);
		}

		static MapEdge ResolveEdge(int tileX, int tileY)
		{
			bool west = tileX < 0;
			bool east = tileX >= MapDocument.WIDTH;
			bool north = tileY < 0;
			bool south = tileY >= MapDocument.HEIGHT;

			if (!west && !east && !north && !south)
				return MapEdge.Inside;

			if (north && west) return MapEdge.NorthWest;
			if (north && east) return MapEdge.NorthEast;
			if (south && west) return MapEdge.SouthWest;
			if (south && east) return MapEdge.SouthEast;

			if (north) return MapEdge.North;
			if (south) return MapEdge.South;
			if (west) return MapEdge.West;
			if (east) return MapEdge.East;

			return MapEdge.Inside;
		}

		static void Redirect(MapEdge edge, ref int mapX, ref int mapY, ref int tileX, ref int tileY)
		{
			switch (edge)
			{
				case MapEdge.North:
					mapY--;
					tileY = MapDocument.HEIGHT - 1;
					break;

				case MapEdge.South:
					mapY++;
					tileY = 0;
					break;

				case MapEdge.West:
					mapX--;
					tileX = MapDocument.WIDTH - 1;
					break;

				case MapEdge.East:
					mapX++;
					tileX = 0;
					break;

				case MapEdge.NorthWest:
					mapX--;
					mapY--;
					tileX = MapDocument.WIDTH - 1;
					tileY = MapDocument.HEIGHT - 1;
					break;

				case MapEdge.NorthEast:
					mapX++;
					mapY--;
					tileX = 0;
					tileY = MapDocument.HEIGHT - 1;
					break;

				case MapEdge.SouthWest:
					mapX--;
					mapY++;
					tileX = MapDocument.WIDTH - 1;
					tileY = 0;
					break;

				case MapEdge.SouthEast:
					mapX++;
					mapY++;
					tileX = 0;
					tileY = 0;
					break;
			}
		}
	}
}
