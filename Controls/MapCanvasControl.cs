using Glyphborn.Mapper.Editor;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace Glyphborn.Mapper.Controls
{
	public sealed class MapCanvasControl : Control
	{
		public AreaDocument? AreaDocument;
		public MapDocument? MapDocument;
		public EditorState? State;


		private bool _isPainting;
		private bool _isErasing;
		private MapEdge _hoverEdge = MapEdge.Inside;

		// Track maps created during the current mouse-drag so we only create each target once.
		private readonly HashSet<long> _createMapsThisDrag = new();

		// NEW: ensure edge-create happens only once per mouse press
		private bool _edgeCreateTriggered;

		public MapCanvasControl()
		{
			DoubleBuffered = true;
			SetStyle(ControlStyles.ResizeRedraw, true);
		}

		private int ComputeTileSize()
		{
			if (MapDocument == null)
				return 1;

			int sizeX = ClientSize.Width / MapDocument.WIDTH;
			int sizeY = ClientSize.Height / MapDocument.HEIGHT - 2;

			return Math.Max(1, Math.Min(sizeX, sizeY));
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);

			if (MapDocument == null || State == null || AreaDocument == null)
				return;

			var g = e.Graphics;

			int tileSize = ComputeTileSize();

			int mapW = MapDocument.WIDTH * tileSize;
			int mapH = MapDocument.HEIGHT * tileSize;

			int ox = (ClientSize.Width - mapW) / 2;
			int oy = (ClientSize.Height - mapH) / 2;

			// Iterate one cell beyond each edge so we can draw neighbor tiles that touch the edges.
			for (int ty = -1; ty <= MapDocument.HEIGHT; ty++)
			{
				for (int tx = -1; tx <= MapDocument.WIDTH; tx++)
				{
					// Determine which map supplies this tile (current or an adjacent neighbor).
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

					// If we are outside the current map bounds, find the neighbor map.
					MapDocument? srcMap = MapDocument;
					if (mapOffsetX != 0 || mapOffsetY != 0)
					{
						int neighborMapX = State.ActiveMapX + mapOffsetX;
						int neighborMapY = State.ActiveMapY + mapOffsetY;

						if (!AreaDocument.HasMap(neighborMapX, neighborMapY))
							continue; // neighbor not present -> nothing to draw here

						srcMap = AreaDocument.GetMap(neighborMapX, neighborMapY);
						if (srcMap == null)
							continue;
					}

					// For each tile, render underlying layers with fade then the current layer.
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

						DrawTile(g, srcMap, srcX, srcY, tileSize, ox, oy, layer, alpha);
					}

					DrawTile(g, srcMap, srcX, srcY, tileSize, ox, oy, State.CurrentLayer, 1.0f);
				}
			}

			if (State.ShowGrid)
				DrawGrid(g, tileSize, ox, oy);

			if (_hoverEdge != MapEdge.Inside)
				DrawEdgeHighlight(g, tileSize, ox, oy);
		}

		// Draw tile from an explicit source map at the given source tile coords (0..WIDTH-1 / 0..HEIGHT-1).
		private void DrawTile(Graphics g, MapDocument srcMap, int sx, int sy,
					  int tileSize, int ox, int oy, int layer, float alpha = 1.0f)
		{
			if (sx < 0 || sy < 0 || sx >= MapDocument.WIDTH || sy >= MapDocument.HEIGHT) return;

			var tileRef = srcMap.Tiles[layer][sy][sx];
			if (tileRef.TileId == 0) return;
			if (tileRef.Tileset >= AreaDocument!.Tilesets.Count) return;

			var tileset = AreaDocument.Tilesets[tileRef.Tileset];
			if (tileRef.TileId >= tileset.Tiles.Count) return;

			var def = tileset.Tiles[tileRef.TileId];
			if (def.Primitive == null) return;

			// Resolve screen position
			int px, py;
			if (ReferenceEquals(srcMap, MapDocument))
			{
				px = ox + sx * tileSize;
				py = oy + sy * tileSize;
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
				px = ox + (dx * MapDocument.WIDTH + sx) * tileSize;
				py = oy + (dy * MapDocument.HEIGHT + sy) * tileSize;
			}

			// Draw tile texture
			var dest = new Rectangle(px, py, tileSize, tileSize);
			var preview = TilePreviewer.GetPreview(def.Primitive.Texture);

			if (alpha < 1.0f)
			{
				var cm = new ColorMatrix { Matrix33 = alpha };
				using var ia = new ImageAttributes();
				ia.SetColorMatrix(cm);
				g.DrawImage(preview, dest, 0, 0, preview.Width, preview.Height, GraphicsUnit.Pixel, ia);
			}
			else
			{
				g.DrawImage(preview, dest);
			}

			// Room overlay — always draw if tile has a room, highlight active room
			if (State!.Tool == Tool.RoomBuilder && tileRef.RoomID.HasValue)
			{
				uint roomId = tileRef.RoomID.Value;
				var roomDef = AreaDocument?.Rooms.FirstOrDefault(r => r.Id == roomId);
				var roomColor = roomDef?.Color ?? Color.FromArgb(
					(int)(roomId * 2654435761u >> 16) & 0xFF,
					(int)(roomId * 2654435761u >> 8) & 0xFF,
					(int)(roomId * 2654435761u) & 0xFF);

				// Active room is fully opaque, others are dimmer
				int overlayAlpha = roomId == State.CurrentRoomID ? 140 : 70;
				using var brush = new SolidBrush(Color.FromArgb(overlayAlpha, roomColor));
				g.FillRectangle(brush, dest);

				// ID label on larger tile sizes only
				if (tileSize >= 12)
				{
					using var font = new Font("Segoe UI", Math.Max(6f, tileSize / 5f), FontStyle.Bold);
					g.DrawString(roomId.ToString(), font, Brushes.White, px + 2, py + 2);
				}
			}
		}

		private void DrawGrid(Graphics g, int tileSize, int ox, int oy)
		{
			using var pen1 = new Pen(Color.FromArgb(80, Color.Crimson), 3f);
			using var pen2 = new Pen(Color.FromArgb(80, Color.White), 1f);

			int mapW = MapDocument.WIDTH * tileSize;
			int mapH = MapDocument.HEIGHT * tileSize;

			for (int x = -1; x <= MapDocument.WIDTH + 1; x++)
			{
				int px = ox + x * tileSize;

				if (x == 0 || x == 32)
				{
					g.DrawLine(pen1, px, oy - tileSize, px, oy + mapH + tileSize);
				}
				else
				{
					g.DrawLine(pen2, px, oy - tileSize, px, oy + mapH + tileSize);
				}
			}

			for (int y = -1; y <= MapDocument.HEIGHT + 1; y++)
			{
				int py = oy + y * tileSize;

				if (y == 0 || y == 32)
				{
					g.DrawLine(pen1, ox - tileSize, py, ox + mapW + tileSize, py);
				}
				else
				{
					g.DrawLine(pen2, ox - tileSize, py, ox + mapW + tileSize, py);
				}
			}
		}

		private void DrawEdgeHighlight(Graphics g, int tileSize, int ox, int oy)
		{
			using var brush = new SolidBrush(Color.FromArgb(80, Color.DodgerBlue));

			Rectangle rect = _hoverEdge switch
			{
				MapEdge.North => new Rectangle(ox, oy - tileSize, MapDocument.WIDTH * tileSize, tileSize),
				MapEdge.South => new Rectangle(ox, oy + MapDocument.HEIGHT * tileSize, MapDocument.WIDTH * tileSize, tileSize),
				MapEdge.West => new Rectangle(ox - tileSize, oy, tileSize, MapDocument.HEIGHT * tileSize),
				MapEdge.East => new Rectangle(ox + MapDocument.WIDTH * tileSize, oy, tileSize, MapDocument.HEIGHT * tileSize),

				MapEdge.NorthWest => new Rectangle(ox - tileSize, oy - tileSize, tileSize, tileSize),
				MapEdge.NorthEast => new Rectangle(ox + MapDocument.WIDTH * tileSize, oy - tileSize, tileSize, tileSize),
				MapEdge.SouthWest => new Rectangle(ox - tileSize, oy + MapDocument.HEIGHT * tileSize, tileSize, tileSize),
				MapEdge.SouthEast => new Rectangle(ox + MapDocument.WIDTH * tileSize, oy + MapDocument.HEIGHT * tileSize, tileSize, tileSize),

				_ => Rectangle.Empty
			};

			if (!rect.IsEmpty)
				g.FillRectangle(brush, rect);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			// start a new press: clear per-press tracking and allow an edge-create once
			_createMapsThisDrag.Clear();
			_edgeCreateTriggered = false;

			MapDocument?.BeginBatch();
			base.OnMouseDown(e);

			if (e.Button == MouseButtons.Left)
			{
				_isPainting = true;
				_isErasing = false;
				PaintTileAtMouse(e.X, e.Y);
			}
			else if (e.Button == MouseButtons.Right)
			{
				_isPainting = true;
				_isErasing = true;
				PaintTileAtMouse(e.X, e.Y, erase: true);
			}
			else if (e.Button == MouseButtons.Middle)
			{
				_isPainting = false;
				_isErasing = false;
				GetTileFromMouse(e.X, e.Y, out int tileX, out int tileY);

				if (State!.Tool == Tool.RoomBuilder)
				{
					MapDocument?.FloodFillRoomID(State.CurrentLayer, tileX, tileY, State.CurrentRoomID);
				}
				else
				{
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

		private void GetTileFromMouse(int mouseX, int mouseY, out int tileX, out int tileY)
		{
			if (MapDocument == null)
			{
				tileX = tileY = 0;
				return;
			}

			int tileSize = ComputeTileSize();
			int mapW = MapDocument.WIDTH * tileSize;
			int mapH = MapDocument.HEIGHT * tileSize;
			int ox = (ClientSize.Width - mapW) / 2;
			int oy = (ClientSize.Height - mapH) / 2;

			double fx = (mouseX - ox) / (double) tileSize;
			double fy = (mouseY - oy) / (double) tileSize;

			tileX = (int) Math.Floor(fx);
			tileY = (int) Math.Floor(fy);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			MapEdge edge = MapEdge.Inside;

			if (MapDocument != null)
			{
				GetTileFromMouse(e.X, e.Y, out int tileX, out int tileY);
				edge = ResolveEdge(tileX, tileY);
			}

			// Preserve lastEdge so we can detect transitions if needed elsewhere.
			_hoverEdge = edge;

			Invalidate();

			if (_isPainting)
			{
				PaintTileAtMouse(e.X, e.Y, erase: _isErasing);
			}
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			MapDocument?.EndBatch();
			base.OnMouseUp(e);

			_isPainting = false;

			_createMapsThisDrag.Clear();
			_edgeCreateTriggered = false;
		}

		private void PaintTileAtMouse(int mouseX, int mouseY, bool erase = false)
		{
			if (MapDocument == null || State == null)
				return;

			if (State.Tool == Tool.MapBuilder && State.SelectedTile == null)
				return;

			GetTileFromMouse(mouseX, mouseY, out int tileX, out int tileY);
			PaintTile(tileX, tileY, erase);

			Invalidate();
		}

		private void PaintTile(int tileX, int tileY, bool erase)
		{
			if (AreaDocument == null || State == null)
				return;

			int mapX = State.ActiveMapX;
			int mapY = State.ActiveMapY;

			var edge = ResolveEdge(tileX, tileY);

			MapDocument? newMap = null;

			if (edge != MapEdge.Inside)
			{
				Redirect(edge, ref mapX, ref mapY, ref tileX, ref tileY);

				long key = ((long) mapX << 32) | (uint) mapY;

				// If map already exists, use it.
				if (AreaDocument.HasMap(mapX, mapY))
				{
					newMap = AreaDocument.GetMap(mapX, mapY);
				}
				// Otherwise create it only once per press/session.
				else if (!_createMapsThisDrag.Contains(key) && !_edgeCreateTriggered)
				{
					// Create once and mark that we've triggered an edge-create for this press.
					newMap = AreaDocument.GetOrCreateMap(mapX, mapY);
					_createMapsThisDrag.Add(key);
					_edgeCreateTriggered = true;
				}
				else
				{
					// Either we've already created this map this press, or an edge-create already happened.
					newMap = AreaDocument.GetMap(mapX, mapY);
				}

				// If we still don't have a map (should be rare), abort this tile.
				if (newMap == null)
					return;

				// Don't change active MapDocument/State here; creation only.
			}

			var target = MapDocument!;
			if (newMap != null)
				target = newMap;
			else
				target = MapDocument!;

			if (target.IsGhost)
				return;

			if (State.Tool == Tool.RoomBuilder)
			{
				uint? roomId = erase ? null : State.CurrentRoomID;
				target.SetTileRoomID(State.CurrentLayer, tileX, tileY, roomId);
				return;
			}

			if (erase)
			{
				target.SetTile(State.CurrentLayer, tileX, tileY, default);
			}
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
