using System;
using System.Collections.Generic;
using System.Drawing;

using Glyphborn.Mapper.Editor.Undo;

namespace Glyphborn.Mapper.Editor
{
	public sealed class MapDocument
	{
		public const int WIDTH = 32;
		public const int HEIGHT = 32;
		public const int LAYERS = 32;

		public TileRef[][][] Tiles;

		public Bitmap? MiniPreview;

		public event Action? Update;

		public bool IsDirty { get; set; }
		public bool MiniPreviewDirty { get; set; }
		public bool IsPreview { get; set; } = false;

		private Stack<ICommand> _undoStack = new();
		private Stack<ICommand> _redoStack = new();
		private List<TileCommand>? _currentBatch;

		public MapDocument()
		{
			Tiles = new TileRef[LAYERS][][];

			for (int l = 0; l < LAYERS; l++)
			{
				Tiles[l] = new TileRef[HEIGHT][];
				for (int y = 0; y < HEIGHT; y++)
					Tiles[l][y] = new TileRef[WIDTH];
			}
		}

		public void BeginBatch()
		{
			_currentBatch = new List<TileCommand>();
		}

		public void EndBatch()
		{
			if (_currentBatch != null && _currentBatch.Count > 0)
			{
				_undoStack.Push(new BatchCommand(_currentBatch.ToArray()));
				_currentBatch = null;
				_redoStack.Clear();
			}
		}

		public void SetTile(int layer, int x, int y, TileRef tile)
		{
			ref var current = ref Tiles[layer][y][x];

			if (current.Tileset == tile.Tileset &&
				current.TileId == tile.TileId)
				return;

			var cmd = new TileCommand
			{
				Layer = layer,
				X = x,
				Y = y,
				OldTile = current,
				NewTile = tile,
			};

			if (_currentBatch != null)
			{
				_currentBatch.Add(cmd);
			}
			else
			{
				_undoStack.Push(cmd);
				_redoStack.Clear();
			}

			current = tile;
			IsDirty = true;
			MiniPreviewDirty = true;
			Update?.Invoke();
		}

		public bool Undo()
		{
			if (_undoStack.Count == 0) return false;

			var cmd = _undoStack.Pop();
			_redoStack.Push(cmd);

			cmd.Undo(this);
			return true;
		}

		public bool Redo()
		{
			if (_redoStack.Count == 0) return false;

			var cmd = _redoStack.Pop();
			_undoStack.Push(cmd);

			cmd.Redo(this);
			return true;
		}

		public void FloodFill(int layer, int startX, int startY, TileRef fillTile)
		{
			if (startX < 0 || startY < 0 || startX >= WIDTH || startY >= HEIGHT)
				return;

			var targetTile = Tiles[layer][startY][startX];

			// Don't fill if already the target tile
			if (targetTile.Tileset == fillTile.Tileset && targetTile.TileId == fillTile.TileId)
				return;

			var stack = new Stack<(int x, int y)>();
			var visited = new HashSet<(int, int)>();

			stack.Push((startX, startY));

			while (stack.Count > 0)
			{
				var (x, y) = stack.Pop();

				if (x < 0 || y < 0 || x >= WIDTH || y >= HEIGHT)
					continue;

				if (visited.Contains((x, y)))
					continue;

				var currentTile = Tiles[layer][y][x];

				if (currentTile.Tileset != targetTile.Tileset || currentTile.TileId != targetTile.TileId)
					continue;

				visited.Add((x, y));
				SetTile(layer, x, y, fillTile);

				stack.Push((x - 1, y));
				stack.Push((x + 1, y));
				stack.Push((x, y - 1));
				stack.Push((x, y + 1));
			}
		}
	}
}
