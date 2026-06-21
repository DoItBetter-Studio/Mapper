using Glyphborn.Mapper.Maths;
using Glyphborn.Mapper.Tiles;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Glyphborn.Mapper.Editor
{
	public static class TilesetSerializer
	{
		private const uint MAGIC = 0x53544C47;  // "GBTS"
		private const ushort VERSION = 3;       // Bumped to 3 for the new packed multi-primitive format

		public static void SaveBinary(Tileset tileset)
		{
			string path = Resolve(tileset);

			using (var fs = new FileStream(path, FileMode.Create))
			using (var bw = new BinaryWriter(fs))
			{
				bw.Write(MAGIC);
				bw.Write(VERSION);
				bw.Write((ushort)tileset.Tiles.Count);

				// Tileset name (64 bytes, null-padded)
				WriteFixedString(bw, tileset.Name, 64);
				bw.Write((byte)tileset.Type);

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
			bw.Write((byte)tile.Collision);
			bw.Write((byte)tile.TileType);
			WriteFixedString(bw, tile.Name, 64);

			var primitives = tile.GetPrimitives().ToList();
			bw.Write((byte)primitives.Count); // Express explicitly how many items are packed inside

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
				{
					bw.Write(idx);
				}

				bw.Write((ushort)visual.Texture.Width);
				bw.Write((ushort)visual.Texture.Height);
				foreach (var pixel in visual.Texture.Pixels)
				{
					bw.Write(pixel);
				}

				WriteFixedString(bw, visual.TextureSourcePath ?? "", 128);
				WriteFixedString(bw, visual.MeshSourcePath ?? "", 128);
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
				if (version > VERSION || version < 1)
					throw new InvalidDataException($"Unsupported version: {version}");

				ushort tileCount = br.ReadUInt16();
				string name = ReadFixedString(br, 64);
				TilesetType type = (TilesetType)br.ReadByte();

				var tileset = new Tileset { Name = name, Type = type };

				for (int i = 0; i < tileCount; i++)
				{
					tileset.Tiles.Add(ReadTile(br, version));
				}

				return tileset;
			}
		}

		private static TileDefinition ReadTile(BinaryReader br, ushort version)
		{
			if (version < 3)
			{
				return ImportLesserVersion(br, version);
			}

			ushort id = br.ReadUInt16();
			CollisionType collision = (CollisionType)br.ReadByte();
			TileType tileType = (TileType)br.ReadByte();
			string name = ReadFixedString(br, 64);

			var tile = tileType switch
			{
				TileType.None => new TileDefinition(),
				TileType.TileGeneric => new TileGeneric(),
				TileType.TileAnimated => new TileAnimated(),
				TileType.TileEntityDoor => new TileEntityDoor(),
				TileType.TileEntityCrop => new TileEntityCrop(),
				_ => throw new InvalidDataException($"TileType unknown: {tileType}")
			};

			// Fix: Re-assign properties back onto the new instance
			tile.Id = id;
			tile.Collision = collision;
			tile.Name = name;

			// Fix: Safely parse the count and iterate to populate multiple primitives sequentially
			byte primitiveCount = br.ReadByte();

			for (int p = 0; p < primitiveCount; p++)
			{
				byte vertexCount = br.ReadByte();
				var vertices = new Vertex[vertexCount];

				for (int i = 0; i < vertexCount; i++)
				{
					vertices[i] = new Vertex
					{
						Position = new Vec3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
						UV = new Vec2(br.ReadSingle(), br.ReadSingle())
					};
				}

				byte indexCount = br.ReadByte();
				var indices = new ushort[indexCount];

				for (int i = 0; i < indexCount; i++)
				{
					indices[i] = br.ReadUInt16();
				}

				var mesh = new Mesh(vertices, indices);

				ushort texWidth = br.ReadUInt16();
				ushort texHeight = br.ReadUInt16();

				var pixels = new uint[texWidth * texHeight];
				for (int i = 0; i < pixels.Length; i++)
				{
					pixels[i] = br.ReadUInt32();
				}

				var texture = new Texture(texWidth, texHeight, pixels);

				tile.AddPrimitive(new RenderPrimitive(mesh, texture)
				{
					TextureSourcePath = ReadFixedString(br, 128),
					MeshSourcePath = ReadFixedString(br, 128)
				});
			}

			return tile;
		}

		private static TileDefinition ImportLesserVersion(BinaryReader br, ushort version)
		{
			ushort id = br.ReadUInt16();
			string name = ReadFixedString(br, 64);
			CollisionType collision = (CollisionType)br.ReadByte();

			uint vertexCount = br.ReadByte();

			TileDefinition tile = vertexCount switch
			{
				0 => new TileDefinition(),
				_ => new TileGeneric()
			};

			tile.Id = id;
			tile.Name = name;
			tile.Collision = collision;

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

				uint indexCount = br.ReadByte();
				var indices = new ushort[indexCount];
				for (int i = 0; i < indexCount; i++)
				{
					indices[i] = br.ReadUInt16();
				}

				var mesh = new Mesh(vertices, indices);

				ushort texWidth = br.ReadUInt16();
				ushort texHeight = br.ReadUInt16();

				var pixels = new uint[texWidth * texHeight];
				for (int i = 0; i < pixels.Length; i++)
				{
					pixels[i] = br.ReadUInt32();
				}

				var texture = new Texture(texWidth, texHeight, pixels);
				var primitive = new RenderPrimitive(mesh, texture);

				if (version == 2)
				{
					primitive.TextureSourcePath = ReadFixedString(br, 128);
					primitive.MeshSourcePath = ReadFixedString(br, 128);
				}

				tile.AddPrimitive(primitive);
			}
			else
			{
				br.ReadByte();    // Skip index_count
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