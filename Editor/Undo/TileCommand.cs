namespace Glyphborn.Mapper.Editor.Undo
{
	internal struct TileCommand : ICommand
	{
		public int Layer;
		public int X, Y;
		public TileRef OldTile;
		public TileRef NewTile;

		public void Undo(MapDocument doc)
		{
			doc.Tiles[Layer][Y][X] = OldTile;
		}

		public void Redo(MapDocument doc)
		{
			doc.Tiles[Layer][Y][X] = NewTile;
		}
	}
}
