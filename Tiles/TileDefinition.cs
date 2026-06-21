using Glyphborn.Mapper.Maths;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Glyphborn.Mapper.Tiles
{
	public enum TileType
	{
		None,
		TileGeneric,
		TileAnimated,

		TileEntityDoor,
		TileEntityCrop
	}

	public class TileDefinition
	{
		// Identity
		public ushort Id;
		public string Name = "";
		public CollisionType Collision = CollisionType.None;
		public virtual TileType TileType { get; } = TileType.None;

		/// <summary>
		/// Editor-only polymorphic hook to grab a primary visual representation 
		/// without exposing storage implementation details.
		/// </summary>
		public virtual IEnumerable<RenderPrimitive> GetPrimitives() => Enumerable.Empty<RenderPrimitive>();
		public virtual void AddPrimitive(RenderPrimitive primitive) { }
		public virtual void ClearPrimitives() { }
	}

	public sealed class TileGeneric : TileDefinition
	{
		public RenderPrimitive? Primitive;
		public override TileType TileType => TileType.TileGeneric;

		public override IEnumerable<RenderPrimitive> GetPrimitives() =>
		Primitive != null ? new[] { Primitive } : Enumerable.Empty<RenderPrimitive>();

		public override void AddPrimitive(RenderPrimitive primitive) => Primitive = primitive;
		public override void ClearPrimitives() => Primitive = null;
	}

	public sealed class TileAnimated : TileDefinition
	{
		public RenderPrimitive? Primitive;
		public byte FrameRate;
		public override TileType TileType => TileType.TileAnimated;

		public override IEnumerable<RenderPrimitive> GetPrimitives() =>
		Primitive != null ? new[] { Primitive } : Enumerable.Empty<RenderPrimitive>();

		public override void AddPrimitive(RenderPrimitive primitive) => Primitive = primitive;
		public override void ClearPrimitives() => Primitive = null;
	}

	public abstract class TileEntity : TileDefinition
	{
		public List<RenderPrimitive> Primitives = new();

		public override IEnumerable<RenderPrimitive> GetPrimitives() => Primitives;
		public override void AddPrimitive(RenderPrimitive primitive) => Primitives.Add(primitive);
		public override void ClearPrimitives() => Primitives.Clear();
	}

	public abstract class AnimatedTileEntity : TileEntity
	{
		public byte FrameRate;
	}

	public sealed class TileEntityDoor : AnimatedTileEntity
	{
		// Has zero real use in the editor, other than to see what it would look like in the game
		public bool OpenState = false;
		public override TileType TileType => TileType.TileEntityDoor;
	}

	public sealed class TileEntityCrop : TileEntity
	{
		// Growth rate is the game ticks between steps
		public ushort GrowthRate;
		public override TileType TileType => TileType.TileEntityCrop;
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

		public uint Sample(float u, float v) => SampleFrame(u, v, 0, Height);

		public uint SampleFrame(float u, float v, int frameIndex, int frameHeight)
		{
			int x = (int)(u * (Width - 1));
			x = Math.Clamp(x, 0, Width - 1);

			int y = (int)(v * (frameHeight - 1));
			y = Math.Clamp(y, 0, frameHeight - 1);
			y += frameIndex * frameHeight;
			y = Math.Clamp(y, 0, Height - 1);

			return Pixels[y * Width + x];
		}
	}

	public sealed class RenderPrimitive
	{
		public readonly Mesh Mesh;
		public readonly Texture Texture;

		// Editor-only metadata
		public string? MeshSourcePath;
		public string? TextureSourcePath;

		public RenderPrimitive(Mesh mesh, Texture texture)
		{
			Mesh = mesh;
			Texture = texture;
		}
	}
}
