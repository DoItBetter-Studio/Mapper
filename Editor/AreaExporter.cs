using System;
using System.IO;
using System.Windows.Forms;

using Glyphborn.Mapper.Tiles;

namespace Glyphborn.Mapper.Editor
{
	public static class AreaExporter
	{
		private const uint MAGIC_GEOMETRY	= 0x474D4247; // "GBMG"
		private const uint MAGIC_COLLISION	= 0x434D4247; // "GBMC"
		private const uint MAGIC_TILESETS	= 0x53544C47;  // "GBTS"


		private const ushort VERSION = 1;
		private static string DataRoot => Path.Combine(AppContext.BaseDirectory, "../..", "data");
		private static string Layouts => Path.Combine(DataRoot, "layouts");
		private static string Tilesets => Path.Combine(DataRoot, "tilesets");


		public static bool ExportBinary(AreaDocument doc)
		{
			// Validate format limits so we don't silently truncate values.
			if (doc.Width > byte.MaxValue || doc.Height > byte.MaxValue)
				throw new InvalidDataException("Area dimensions exceed storage limits (max 255).");

			if (!WriteTilesets(doc))
				return false;
			if (!WriteGeometry(doc))
				return false;
			if (!WriteCollision(doc))
				return false;

			return true;
		}

		private static bool WriteTilesets(AreaDocument doc)
		{
			bool result = false;

			try
			{
				for (int i = 0; i < doc.Tilesets.Count; i++)
				{
					var tileset = doc.Tilesets[i];
					string tilesetPath = Resolve(tileset);
					var dir = Path.GetDirectoryName(tilesetPath) ?? Tilesets;
					if (!Directory.Exists(dir))
						Directory.CreateDirectory(dir);

					var name = tilesetPath.ToLower().Replace(' ', '_').Replace('-', '_');

					using (var fs = new FileStream(name, FileMode.Create))
					using (var bw = new BinaryWriter(fs))
					{
						bw.Write(MAGIC_TILESETS);
						bw.Write(VERSION);
						bw.Write((ushort) tileset.Tiles.Count);

						foreach (var tile in tileset.Tiles)
						{
							WriteTile(bw, tile);
						}
					}
				}

				result = true;
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
				result = false;
			}

			return result;
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
			if (tile.Primitive != null)
			{
				// Mesh Data
				bw.Write((uint) tile.Primitive.Mesh.Vertices.Length);

				foreach (var v in tile.Primitive.Mesh.Vertices)
				{
					bw.Write(v.Position.x);
					bw.Write(v.Position.y);
					bw.Write(v.Position.z);
					bw.Write(v.UV.x);
					bw.Write(v.UV.y);
				}

				bw.Write((uint) tile.Primitive.Mesh.Indices.Length);

				foreach (var idx in tile.Primitive.Mesh.Indices)
				{
					bw.Write(idx);
				}

				// Texture data
				bw.Write((ushort) tile.Primitive.Texture.Width);
				bw.Write((ushort) tile.Primitive.Texture.Height);

				foreach (var pixel in tile.Primitive.Texture.Pixels)
				{
					bw.Write(pixel);  // ARGB uint32
				}
			}
			else
			{
				// No render data (e.g., Air tile)
				bw.Write((uint) 0);  // vertex_count = 0
				bw.Write((uint) 0);  // index_count = 0
				bw.Write((ushort) 0); // texture width = 0
				bw.Write((ushort) 0); // texture height = 0
			}
		}

		private static bool WriteGeometry(AreaDocument doc)
		{
			bool result = false;
			try
			{
				for (int areaY = 0; areaY < doc.Height; areaY++)
					for (int areaX = 0; areaX < doc.Width; areaX++)
					{
						var name = doc.Name.ToLower().Replace(' ', '_').Replace('-', '_');

						string geometryPath = Path.Combine(Layouts, $"{name}_{areaX}_{areaY}", $"geometry.bin");
						var dir = Path.GetDirectoryName(geometryPath) ?? Layouts;
						if (!Directory.Exists(dir))
							Directory.CreateDirectory(dir);

						using (var fs = new FileStream(geometryPath, FileMode.Create))
						using (var bw = new BinaryWriter(fs))
						{
							bw.Write(MAGIC_GEOMETRY);
							bw.Write(VERSION);

							var map = doc.GetMap(areaX, areaY);

							if (map == null)
							{
								continue;
							}

							for (int layers = 0; layers < MapDocument.LAYERS; layers++)
								for (int y = 0; y < MapDocument.HEIGHT; y++)
									for (int x = 0; x < MapDocument.WIDTH; x++)
									{
										var tile = map.Tiles[layers][y][x];

										// Pack tileset (2 bits) + tileId (14 bits) into ushort
										ushort packed = (ushort) ((tile.Tileset << 14) | tile.TileId);
										bw.Write(packed);
									}
						}
					}

				result = true;
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
				result = false;
			}

			return result;
		}

		private static bool WriteCollision(AreaDocument doc)
		{
			bool result = false;

			try
			{
				for (int areaY = 0; areaY < doc.Height; areaY++)
					for (int areaX = 0; areaX < doc.Width; areaX++)
					{
						var name = doc.Name.ToLower().Replace(' ', '_').Replace('-', '_');

						string collisionPath = Path.Combine(Layouts, $"{name}_{areaX}_{areaY}", "collision.bin");
						var dir = Path.GetDirectoryName(collisionPath) ?? Layouts;
						if (!Directory.Exists(dir))
							Directory.CreateDirectory(dir);

						using (var fs = new FileStream(collisionPath, FileMode.Create))
						using (var bw = new BinaryWriter(fs))
						{
							bw.Write(MAGIC_COLLISION);
							bw.Write(VERSION);

							var map = doc.GetMap(areaX, areaY);

							if (map == null)
							{
								continue;
							}

							for (int layers = 0; layers < MapDocument.LAYERS; layers++)
								for (int y = 0; y < MapDocument.HEIGHT; y++)
									for (int x = 0; x < MapDocument.WIDTH; x++)
									{
										var tile = map.Tiles[layers][y][x];

										TileDefinition tileDefinition = doc.Tilesets[tile.Tileset].Tiles[tile.TileId];

										bw.Write((byte) tileDefinition.Collision);
									}
						}
					}

				result = true;
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message);
				result = false;
			}

			return result;
		}
	}
}
