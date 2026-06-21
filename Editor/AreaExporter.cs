using System;
using System.IO;
using System.Linq;
using Avalonia.Threading;
using Glyphborn.Mapper.Tiles;
using MsBox.Avalonia;

namespace Glyphborn.Mapper.Editor
{
	public static class AreaExporter
	{
		private const uint MAGIC_GEOMETRY = 0x474D4247;  // "GBMG"
		private const uint MAGIC_COLLISION = 0x434D4247;  // "GBMC"
		private const uint MAGIC_TILESETS = 0x53544C47;  // "GBTS"
		private const uint MAGIC_ROOM = 0x524D4247;  // "GBRM"

		private const ushort VERSION = 2;

		private static string DataRoot => Path.Combine(AppContext.BaseDirectory, "../..", "data");
		private static string Layouts => Path.Combine(DataRoot, "layouts");
		private static string Tilesets => Path.Combine(DataRoot, "tilesets");

		public static bool ExportBinary(AreaDocument doc)
		{
			if (doc.Width > byte.MaxValue || doc.Height > byte.MaxValue)
				throw new InvalidDataException("Area dimensions exceed storage limits (max 255).");

			if (!WriteTilesets(doc)) return false;
			if (!WriteGeometry(doc)) return false;
			if (!WriteCollision(doc)) return false;
			if (!WriteRooms(doc)) return false;

			return true;
		}

		// ── Tilesets ──────────────────────────────────────────────────────────────

		private static bool WriteTilesets(AreaDocument doc)
		{
			try
			{
				foreach (var tileset in doc.Tilesets)
				{
					string tilesetPath = Resolve(tileset);
					string dir = Path.GetDirectoryName(tilesetPath) ?? Tilesets;
					if (!Directory.Exists(dir))
						Directory.CreateDirectory(dir);

					// Normalise to lowercase/underscores to match runtime expectations
					string name = tilesetPath.ToLower().Replace(' ', '_').Replace('-', '_');

					using var fs = new FileStream(name, FileMode.Create);
					using var bw = new BinaryWriter(fs);

					bw.Write(MAGIC_TILESETS);
					bw.Write(VERSION);
					bw.Write((ushort)tileset.Tiles.Count);

					foreach (var tile in tileset.Tiles)
						WriteTile(bw, tile);
				}

				return true;
			}
			catch (Exception ex)
			{
				Dispatcher.UIThread.Post(() => MessageBoxManager.GetMessageBoxStandard("Error", ex.Message).ShowAsync());
				return false;
			}
		}

		private static string Resolve(Tileset tileset)
		{
			string folder = tileset.Type switch
			{
				TilesetType.Regional => "regional",
				TilesetType.Local => "local",
				TilesetType.Interior => "interior",
				_ => throw new ArgumentOutOfRangeException()
			};

			return Path.Combine(Tilesets, folder, $"{tileset.Name}.bin");
		}

		private static void WriteTile(BinaryWriter bw, TileDefinition tile)
		{
			// 1. Write the TileType byte first so the runtime loader can construct the correct tile class
			bw.Write((byte)tile.TileType);

			var primitives = tile.GetPrimitives().ToList();
			bw.Write((byte)primitives.Count);

			if (tile.TileType != TileType.None)
			{
				foreach (var visual in primitives)
				{
					bw.Write((byte)visual.Mesh.Vertices.Length);
					foreach (var v in visual.Mesh.Vertices)
					{
						bw.Write(v.Position.x);
						bw.Write(v.Position.y);
						bw.Write(v.Position.z);
						bw.Write(v.UV.x);
						bw.Write(v.UV.y);
					}

					bw.Write((byte)visual.Mesh.Indices.Length);
					foreach (var idx in visual.Mesh.Indices)
						bw.Write(idx);

					bw.Write((ushort)visual.Texture.Width);
					bw.Write((ushort)visual.Texture.Height);
					foreach (var pixel in visual.Texture.Pixels)
						bw.Write(pixel);
				}
			}
			else
			{
				// Clear structure padding markers for empty slots/air tiles
				bw.Write((byte)0);     // vertex_count
				bw.Write((byte)0);     // index_count
				bw.Write((ushort)0);   // texture_width
				bw.Write((ushort)0);   // texture_height
			}
		}

		// ── Geometry ──────────────────────────────────────────────────────────────

		private static bool WriteGeometry(AreaDocument doc)
		{
			try
			{
				for (int areaY = 0; areaY < doc.Height; areaY++)
					for (int areaX = 0; areaX < doc.Width; areaX++)
					{
						string chunkDir = ChunkDir(doc, areaX, areaY);
						string filePath = Path.Combine(chunkDir, "geometry.bin");

						using var fs = new FileStream(filePath, FileMode.Create);
						using var bw = new BinaryWriter(fs);

						bw.Write(MAGIC_GEOMETRY);
						bw.Write(VERSION);

						var map = doc.GetMap(areaX, areaY);
						if (map == null || map.IsGhost) continue;

						for (int l = 0; l < MapDocument.LAYERS; l++)
							for (int y = 0; y < MapDocument.HEIGHT; y++)
								for (int x = 0; x < MapDocument.WIDTH; x++)
								{
									var tile = map.Tiles[l][y][x];
									ushort packed = (ushort)((tile.Tileset << 14) | tile.TileId);
									bw.Write(packed);
								}
					}

				return true;
			}
			catch (Exception ex)
			{
				Dispatcher.UIThread.Post(() => MessageBoxManager.GetMessageBoxStandard("Error", ex.Message).ShowAsync());
				return false;
			}
		}

		// ── Collision ─────────────────────────────────────────────────────────────

		private static bool WriteCollision(AreaDocument doc)
		{
			try
			{
				for (int areaY = 0; areaY < doc.Height; areaY++)
					for (int areaX = 0; areaX < doc.Width; areaX++)
					{
						string chunkDir = ChunkDir(doc, areaX, areaY);
						string filePath = Path.Combine(chunkDir, "collision.bin");

						using var fs = new FileStream(filePath, FileMode.Create);
						using var bw = new BinaryWriter(fs);

						bw.Write(MAGIC_COLLISION);
						bw.Write(VERSION);

						var map = doc.GetMap(areaX, areaY);
						if (map == null) continue;

						for (int l = 0; l < MapDocument.LAYERS; l++)
							for (int y = 0; y < MapDocument.HEIGHT; y++)
								for (int x = 0; x < MapDocument.WIDTH; x++)
								{
									var tile = map.Tiles[l][y][x];
									var tileDefinition = doc.Tilesets[tile.Tileset].Tiles[tile.TileId];
									bw.Write((byte)tileDefinition.Collision);
								}
					}

				return true;
			}
			catch (Exception ex)
			{
				Dispatcher.UIThread.Post(() => MessageBoxManager.GetMessageBoxStandard("Error", ex.Message).ShowAsync());
				return false;
			}
		}

		// ── Rooms ─────────────────────────────────────────────────────────────────

		private static bool WriteRooms(AreaDocument doc)
		{
			try
			{
				for (int areaY = 0; areaY < doc.Height; areaY++)
					for (int areaX = 0; areaX < doc.Width; areaX++)
					{
						string chunkDir = ChunkDir(doc, areaX, areaY);
						string filePath = Path.Combine(chunkDir, "rooms.bin");

						using var fs = new FileStream(filePath, FileMode.Create);
						using var bw = new BinaryWriter(fs);

						bw.Write(MAGIC_ROOM);
						bw.Write(VERSION);

						var map = doc.GetMap(areaX, areaY);
						if (map == null || map.IsGhost) continue;

						for (int l = 0; l < MapDocument.LAYERS; l++)
							for (int y = 0; y < MapDocument.HEIGHT; y++)
								for (int x = 0; x < MapDocument.WIDTH; x++)
								{
									var tile = map.Tiles[l][y][x];
									bw.Write((ushort)(tile.RoomID ?? 0));
								}
					}

				return true;
			}
			catch (Exception ex)
			{
				Dispatcher.UIThread.Post(() => MessageBoxManager.GetMessageBoxStandard("Error", ex.Message).ShowAsync());
				return false;
			}
		}

		// ── Helpers ───────────────────────────────────────────────────────────────

		// Returns the chunk layout directory, creating it if needed.
		private static string ChunkDir(AreaDocument doc, int areaX, int areaY)
		{
			string name = doc.Name.ToLower().Replace(' ', '_').Replace('-', '_');
			string dir = Path.Combine(Layouts, $"{name}_{areaX}_{areaY}");
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);
			return dir;
		}
	}
}