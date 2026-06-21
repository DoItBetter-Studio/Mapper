using Avalonia.Media;
using Glyphborn.Mapper.Tiles;
using System.IO;
using System.Text;

namespace Glyphborn.Mapper.Editor
{
	internal static class AreaSerializer
	{
		private const uint MAGIC = 0x204D4247;  // "GBM "
		private const ushort VERSION = 2;            // v2: adds RoomID per tile + rooms block

		public static void SaveBinary(AreaDocument doc)
		{
			// Validate format limits so we don't silently truncate values.
			if (doc.Width > byte.MaxValue || doc.Height > byte.MaxValue)
				throw new InvalidDataException("Area dimensions exceed storage limits (max 255).");
			if (doc.Tilesets.Count > byte.MaxValue)
				throw new InvalidDataException("Too many tilesets (max 255).");
			if (doc.Rooms.Count > ushort.MaxValue)
				throw new InvalidDataException("Too many rooms (max 65535).");

			string path = Path.Combine(EditorPaths.Maps, $"{doc.Name}.gbm");

			var dir = Path.GetDirectoryName(path) ?? EditorPaths.Maps;
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			using var fs = new FileStream(path, FileMode.Create);
			using var bw = new BinaryWriter(fs);

			// ── File header ───────────────────────────────────────────────────────
			bw.Write(MAGIC);
			bw.Write(VERSION);

			byte[] nameBytes = Encoding.UTF8.GetBytes(doc.Name);
			bw.Write((ushort)nameBytes.Length);
			bw.Write(nameBytes);

			bw.Write((byte)doc.Width);
			bw.Write((byte)doc.Height);

			// ── Tileset references ────────────────────────────────────────────────
			bw.Write((byte)doc.Tilesets.Count);
			foreach (var tileset in doc.Tilesets)
			{
				string relativePath = $"{tileset.Type.ToString().ToLower()}/{tileset.Name}.gbts";
				byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
				bw.Write((ushort)pathBytes.Length);
				bw.Write(pathBytes);
			}

			// ── Map layouts ───────────────────────────────────────────────────────
			for (int areaY = 0; areaY < doc.Height; areaY++)
				for (int areaX = 0; areaX < doc.Width; areaX++)
				{
					var map = doc.GetMap(areaX, areaY);

					if (map == null || map.IsGhost)
					{
						bw.Write((byte)0);
						continue;
					}

					bw.Write((byte)1);

					for (int l = 0; l < MapDocument.LAYERS; l++)
						for (int y = 0; y < MapDocument.HEIGHT; y++)
							for (int x = 0; x < MapDocument.WIDTH; x++)
							{
								var tile = map.Tiles[l][y][x];

								ushort packed = (ushort)((tile.Tileset << 14) | tile.TileId);
								bw.Write(packed);
								bw.Write((ushort)(tile.RoomID ?? 0));  // v2: room ID per tile
							}

					map.IsDirty = false;
				}

			// ── Room definitions ──────────────────────────────────────────────────
			// Written after layouts so v1 readers skip cleanly on version mismatch.
			bw.Write((ushort)doc.Rooms.Count);
			foreach (var room in doc.Rooms)
			{
				bw.Write((ushort)room.Id);

				byte[] roomName = Encoding.UTF8.GetBytes(room.Name);
				bw.Write((ushort)roomName.Length);
				bw.Write(roomName);

				bw.Write(room.Color.R);
				bw.Write(room.Color.G);
				bw.Write(room.Color.B);
			}
		}

		public static AreaDocument LoadBinary(string path)
		{
			using var fs = new FileStream(path, FileMode.Open);
			using var br = new BinaryReader(fs);

			// ── File header ───────────────────────────────────────────────────────
			uint magic = br.ReadUInt32();
			if (magic != MAGIC)
				throw new InvalidDataException("Invalid GBM file.");

			ushort version = br.ReadUInt16();
			if (version != 1 && version != 2)
				throw new InvalidDataException($"Unsupported GBM version: {version}.");

			ushort nameLen = br.ReadUInt16();
			string name = Encoding.UTF8.GetString(br.ReadBytes(nameLen));

			byte width = br.ReadByte();
			byte height = br.ReadByte();

			var doc = new AreaDocument(width, height) { Name = name };

			// ── Tileset references ────────────────────────────────────────────────
			byte tilesetCount = br.ReadByte();
			for (int i = 0; i < tilesetCount; i++)
			{
				ushort len = br.ReadUInt16();
				string tilesetPath = Encoding.UTF8.GetString(br.ReadBytes(len));
				doc.Tilesets.Add(TilesetSerializer.LoadBinary(tilesetPath));
			}

			// ── Map layouts ───────────────────────────────────────────────────────
			for (int ay = 0; ay < doc.Height; ay++)
				for (int ax = 0; ax < doc.Width; ax++)
				{
					byte exists = br.ReadByte();

					if (exists == 0)
					{
						doc.SetMap(ax, ay, null);
						continue;
					}

					var map = new MapDocument();

					for (int l = 0; l < MapDocument.LAYERS; l++)
						for (int y = 0; y < MapDocument.HEIGHT; y++)
							for (int x = 0; x < MapDocument.WIDTH; x++)
							{
								ushort packed = br.ReadUInt16();
								uint? roomId = null;

								if (version >= 2)
								{
									ushort raw = br.ReadUInt16();
									roomId = raw == 0 ? null : (uint?)raw;
								}

								map.Tiles[l][y][x] = new TileRef
								{
									Tileset = (byte)((packed >> 14) & 0x3),
									TileId = (ushort)(packed & 0x3FFF),
									RoomID = roomId
								};
							}

					doc.SetMap(ax, ay, map);
				}

			// ── Room definitions (v2 only) ────────────────────────────────────────
			if (version >= 2)
			{
				ushort roomCount = br.ReadUInt16();
				for (int i = 0; i < roomCount; i++)
				{
					ushort roomId = br.ReadUInt16();
					ushort roomNameLen = br.ReadUInt16();
					string roomName = Encoding.UTF8.GetString(br.ReadBytes(roomNameLen));
					byte r = br.ReadByte();
					byte g = br.ReadByte();
					byte b = br.ReadByte();

					doc.Rooms.Add(new RoomDefinition
					{
						Id = roomId,
						Name = roomName,
						Color = Color.FromArgb(255, r, g, b)
					});
				}
			}

			return doc;
		}
	}
}