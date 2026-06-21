using Glyphborn.Mapper.Tiles;

namespace Glyphborn.Mapper.Editor
{
	public enum Tool
	{
		MapBuilder = 0,
		RoomBuilder = 1,
	}

	public sealed class EditorState
	{
		public Tool Tool { get; set; }
		public int CurrentLayer { get; set; } = 0;
		public TileSelection? SelectedTile { get; set; }
		public bool ShowGrid { get; set; } = true;
		public AreaDocument? Area;
		public int ActiveMapX;
		public int ActiveMapY;

		public uint CurrentRoomID { get; set; } = 1;
	}

	public readonly struct TileSelection
	{
		public readonly byte TilesetIndex;
		public readonly ushort TileIndex;
		public readonly TileDefinition Tile;

		public TileSelection(byte tilesetIndex, ushort tileIndex, TileDefinition tile)
		{
			TilesetIndex = tilesetIndex;
			TileIndex = tileIndex;
			Tile = tile;
		}
	}
}
