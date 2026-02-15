namespace Glyphborn.Mapper.Editor.Undo
{
	internal sealed class BatchCommand : ICommand
	{
		private readonly TileCommand[] _commands;

		public BatchCommand(TileCommand[] commands)
		{
			_commands = commands;
		}

		public void Undo(MapDocument doc)
		{
			foreach (var cmd in _commands)
				cmd.Undo(doc);
		}

		public void Redo(MapDocument doc)
		{
			foreach (var cmd in _commands)
				cmd.Redo(doc);
		}
	}
}
