using System;
using System.Numerics;

using Glyphborn.Mapper.Maths;

namespace Glyphborn.Mapper.Tiles
{
	public sealed class TileDefinition
	{
		// Identity
		public ushort Id;
		public string Name = "";
		public CollisionType Collision = CollisionType.None;

		// Rendering
		public RenderPrimitive? Primitive;

		// Editor-only metadata
		public string? MeshSourcePath;
		public string? TextureSourcePath;
	}

	public sealed class Mesh
	{
		public readonly Vertex[] Vertices;
		public readonly ushort[] Indices;

		public Mesh(Vertex[] vertices, ushort[] indices)
		{
			Vertices = vertices;
			Indices = indices;
		}
	}

	public struct Vertex
	{
		public Vec3 Position;
		public Vec2 UV;
	}

	public sealed class Texture
	{
		public readonly int Width;
		public readonly int Height;
		public readonly uint[] Pixels;

		public Texture(int width, int height, uint[] pixels)
		{
			Width = width;
			Height = height;
			Pixels = pixels;
		}

		public uint Sample(float u, float v)
		{
			int x = (int)(u * (Width - 1));
			int y = (int)(v * (Height - 1));

			x = Math.Clamp(x, 0, Width - 1);
			y = Math.Clamp(y, 0, Height - 1);

			return Pixels[y * Width + x];
		}
	}

	public sealed class RenderPrimitive
	{
		public readonly Mesh Mesh;
		public readonly Texture Texture;

		public RenderPrimitive(Mesh mesh, Texture texture)
		{
			Mesh = mesh;
			Texture = texture;
		}
	}
}
