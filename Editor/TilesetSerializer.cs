using System;
using System.IO;
using System.Numerics;
using System.Text;

using Glyphborn.Mapper.Maths;
using Glyphborn.Mapper.Tiles;

namespace Glyphborn.Mapper.Editor
{
	public static class TilesetSerializer
	{
		private const uint MAGIC = 0x53544C47;  // "GBTS"
		private const ushort VERSION = 1;

		public static void SaveBinary(Tileset tileset)
		{
			string path = Resolve(tileset);

			using (var fs = new FileStream(path, FileMode.Create))
			using (var bw = new BinaryWriter(fs))
			{
				bw.Write(MAGIC);
				bw.Write(VERSION);
				bw.Write((ushort) tileset.Tiles.Count);

				// Tileset name (64 bytes, null-padded)
				WriteFixedString(bw, tileset.Name, 64);
				bw.Write((byte) tileset.Type);

				foreach (var tile in tileset.Tiles)
				{
					WriteTile(bw, tile);
				}
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

			return Path.Combine(EditorPaths.Tilesets, folder, $"{tileset.Name}.gbts");
		}

		private static void WriteTile(BinaryWriter bw, TileDefinition tile)
		{
			bw.Write(tile.Id);
			WriteFixedString(bw, tile.Name, 64);
			bw.Write((byte) tile.Collision);

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

		public static Tileset LoadBinary(string path)
		{
			string fullPath = Path.IsPathRooted(path)
				? path
				: Path.Combine(EditorPaths.Tilesets, path);

			using (var fs = new FileStream(fullPath, FileMode.Open))
			using (var br = new BinaryReader(fs))
			{
				// Verify header
				uint magic = br.ReadUInt32();

				if (magic != MAGIC)
					throw new InvalidDataException("Invalid tileset file");

				ushort version = br.ReadUInt16();
				if (version != VERSION)
					throw new InvalidDataException($"Unsupported version: {version}");

				ushort tileCount = br.ReadUInt16();
				string name = ReadFixedString(br, 64);
				TilesetType type = (TilesetType) br.ReadByte();

				var tileset = new Tileset { Name = name, Type = type };

				for (int i = 0; i < tileCount; i++)
				{
					tileset.Tiles.Add(ReadTile(br));
				}

				return tileset;
			}
		}

		private static TileDefinition ReadTile(BinaryReader br)
		{
			var tile = new TileDefinition
			{
				Id = br.ReadUInt16(),
				Name = ReadFixedString(br, 64),
				Collision = (CollisionType) br.ReadByte(),
			};

			// Read mesh
			uint vertexCount = br.ReadUInt32();

			if (vertexCount > 0)
			{
				var vertices = new Vertex[vertexCount];

				for (int i = 0; i < vertexCount; i++)
				{
					vertices[i] = new Vertex
					{
						Position = new Vec3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
						UV = new Vec2(br.ReadSingle(), br.ReadSingle())
					};
				}

				uint indexCount = br.ReadUInt32();
				var indices = new ushort[indexCount];

				for (int i = 0; i < indexCount; i++)
				{
					indices[i] = br.ReadUInt16();
				}

				var mesh = new Mesh(vertices, indices);

				// Read texture
				ushort texWidth = br.ReadUInt16();
				ushort texHeight = br.ReadUInt16();

				var pixels = new uint[texWidth * texHeight];
				for (int i = 0; i < pixels.Length; i++)
				{
					pixels[i] = br.ReadUInt32();
				}

				var texture = new Texture(texWidth, texHeight, pixels);

				tile.Primitive = new RenderPrimitive(mesh, texture);
			}
			else
			{
				// Air tile or no render data
				br.ReadUInt32();  // Skip index_count
				br.ReadUInt16();  // Skip texture width
				br.ReadUInt16();  // Skip texture height
			}

			return tile;
		}

		private static void WriteFixedString(BinaryWriter bw, string str, int length)
		{
			byte[] bytes = new byte[length];
			if (!string.IsNullOrEmpty(str))
			{
				Encoding.UTF8.GetBytes(str, 0, Math.Min(str.Length, length - 1), bytes, 0);
			}
			bw.Write(bytes);
		}

		private static string ReadFixedString(BinaryReader br, int length)
		{
			byte[] bytes = br.ReadBytes(length);
			return Encoding.UTF8.GetString(bytes).Trim('\0');
		}
	}
}
