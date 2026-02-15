using System.IO;
using System.Text;

namespace Glyphborn.Mapper.Editor
{
	internal static class AreaSerializer
	{
		private const uint MAGIC = 0x204D4247;  // "GBM "
		private const ushort VERSION = 1;

		public static void SaveBinary(AreaDocument doc)
		{
			// Validate format limits so we don't silently truncate values.
			if (doc.Width > byte.MaxValue || doc.Height > byte.MaxValue)
				throw new InvalidDataException("Area dimensions exceed storage limits (max 255).");
			if (doc.Tilesets.Count > byte.MaxValue)
				throw new InvalidDataException("Too many tilesets (max 255).");

			string path = Path.Combine(EditorPaths.Maps, $"{doc.Name}.gbm");

			// Ensure maps folder exists
			var dir = Path.GetDirectoryName(path) ?? EditorPaths.Maps;
			if (!Directory.Exists(dir))
				Directory.CreateDirectory(dir);

			using (var fs = new FileStream(path, FileMode.Create))
			using (var bw = new BinaryWriter(fs))
			{
				bw.Write(MAGIC);
				bw.Write(VERSION);

				byte[] nameBytes = Encoding.UTF8.GetBytes(doc.Name);
				bw.Write((ushort)nameBytes.Length);
				bw.Write(nameBytes);

				bw.Write((byte)doc.Width);
				bw.Write((byte)doc.Height);

				bw.Write((byte)doc.Tilesets.Count);
				// Write tileset paths (relative to Assets/)
				foreach (var tileset in doc.Tilesets)
				{
					string relativePath = $"{tileset.Type.ToString().ToLower()}/{tileset.Name}.gbts";
					byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath);
					bw.Write((ushort)pathBytes.Length);
					bw.Write(pathBytes);
				}

				for (int areaY = 0; areaY < doc.Height; areaY++)
				for (int areaX = 0; areaX < doc.Width; areaX++)
				{
					var map = doc.GetMap(areaX, areaY);

					if (map == null)
					{
						bw.Write((byte) 0);
						continue;
					}

					bw.Write((byte) 1);

					for (int layers = 0; layers < MapDocument.LAYERS; layers++)
					for (int y = 0; y < MapDocument.HEIGHT; y++)
					for (int x = 0; x < MapDocument.WIDTH; x++)
					{
						var tile = map.Tiles[layers][y][x];

						// Pack tileset (2 bits) + tileId (14 bits) into ushort
						ushort packed = (ushort)((tile.Tileset << 14) | tile.TileId);
						bw.Write(packed);
					}

					map.IsDirty = false;
				}
			}
		}

		public static AreaDocument LoadBinary(string path)
		{
			using var fs = new FileStream(path, FileMode.Open);
			using var br = new BinaryReader(fs);

			// Header
			uint magic = br.ReadUInt32();
			if (magic != MAGIC)
				throw new InvalidDataException("Invalid GBM file");

			ushort version = br.ReadUInt16();
			if (version != VERSION)
				throw new InvalidDataException($"Unsupported version: {version}");

			// Area name
			ushort nameLen = br.ReadUInt16();
			string name = Encoding.UTF8.GetString(br.ReadBytes(nameLen));

			// Dimensions
			byte width = br.ReadByte();
			byte height = br.ReadByte();

			var doc = new AreaDocument(width, height)
			{
				Name = name
			};

			// Tilesets
			byte tilesetCount = br.ReadByte();
			for (int i = 0; i < tilesetCount; i++)
			{
				ushort len = br.ReadUInt16();
				string tilesetPath = Encoding.UTF8.GetString(br.ReadBytes(len));
				doc.Tilesets.Add(TilesetSerializer.LoadBinary(tilesetPath));
			}

			// Layouts
			for (int ay = 0; ay < doc.Height; ay++)
			for (int ax = 0; ax < doc.Width; ax++)
			{
				var map = new MapDocument();

				byte exists = br.ReadByte();

				if (exists == 0)
				{
					doc.SetMap(ax, ay, null);
					continue;
				}

				for (int l = 0; l < MapDocument.LAYERS; l++)
				for (int y = 0; y < MapDocument.HEIGHT; y++)
				for (int x = 0; x < MapDocument.WIDTH; x++)
				{
					ushort packed = br.ReadUInt16();
					map.Tiles[l][y][x] = new TileRef
					{
						Tileset = (byte)((packed >> 14) & 0x3),
						TileId = (ushort)(packed & 0x3FFF),
					};
				}

				doc.SetMap(ax, ay, map);
			}

			return doc;
		}
	}
}
