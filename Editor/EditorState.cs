using Glyphborn.Mapper.Controls;

namespace Glyphborn.Mapper.Editor
{
	public enum Tool
	{
		Paint,
		Erase,
		Fill,
		Eyedrop
	}

	public sealed class EditorState
	{
		public int CurrentLayer { get; set; } = 0;
		public TileSelection? SelectedTile { get; set; }
		public bool ShowGrid { get; set; } = true;
		public AreaDocument? Area;
		public int ActiveMapX;
		public int ActiveMapY;
	}
}
