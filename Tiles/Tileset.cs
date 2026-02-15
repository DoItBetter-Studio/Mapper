using System.Collections.Generic;

using Glyphborn.Mapper.Editor;

namespace Glyphborn.Mapper.Tiles
{
	public sealed class Tileset
	{
		public string Name { get; set; } = "Unnamed";
		public TilesetType Type { get; set; }
		public List<TileDefinition> Tiles { get; } = new();
	}
}
