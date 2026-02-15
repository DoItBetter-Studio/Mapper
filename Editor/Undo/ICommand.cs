namespace Glyphborn.Mapper.Editor.Undo
{
	internal interface ICommand
	{
		void Undo(MapDocument doc);
		void Redo(MapDocument doc);
	}
}
