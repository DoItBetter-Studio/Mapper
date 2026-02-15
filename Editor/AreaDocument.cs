using System;
using System.Collections.Generic;

using Glyphborn.Mapper.Tiles;

namespace Glyphborn.Mapper.Editor
{
	enum MapEdge
	{
		Inside,
		North, South, East, West,
		NorthEast, NorthWest,
		SouthEast, SouthWest
	}

	public sealed class AreaDocument
	{
		// --- Shared tilesets (same structure MapDocument already uses) ---
		public List<Tileset> Tilesets { get; } = new List<Tileset>();

		// --- Map Matrix ---
		public int Width { get; private set; }
		public int Height { get; private set; }

		public MapDocument?[,] Maps { get; private set; }

		// Optional editor metadata
		public string Name { get; set; } = "New Area";

		public event Action? Changed;

		public AreaDocument(int width, int height)
		{
			Width = width;
			Height = height;
			Maps = new MapDocument?[width, height];
		}

		public MapDocument? GetMap(int x, int y)
		{
			if (x < 0 || y < 0 || x >= Width || y >= Height)
				return null;

			return Maps[x, y];
		}

		public bool HasMap(int x, int y)
		{
			return x>= 0 && y >= 0 &&
				x < Width && y < Height &&
				Maps[x, y] != null;
		}

		public void SetMap(int x, int y, MapDocument? map)
		{
			if (x < 0 || y < 0 || x >= Width || y >= Height)
				return;

			Maps[x, y] = map;
			Changed?.Invoke();
		}

		public MapDocument GetOrCreateMap(int x, int y)
		{
			if (x < 0)
			{
				ExpandWest();
				x = 0;
			}
			else if (x >= Width)
			{
				ExpandEast();
			}

			if (y < 0)
			{
				ExpandNorth();
				y = 0;
			}
			else if (y >= Height)
			{
				ExpandSouth();
			}

			if (Maps[x, y] == null)
			{
				Maps[x, y] = new MapDocument();
				Changed?.Invoke();
			}

			return Maps[x, y]!;
		}

		private void ExpandNorth()
		{
			var newMaps = new MapDocument?[Width, Height + 1];

			for (int y = 0; y < Height; y++)
			for (int x = 0; x < Width; x++)
				newMaps[x, y + 1] = Maps[x, y];

			Maps = newMaps;
			Height++;
			Changed?.Invoke();
		}

		private void ExpandSouth()
		{
			var newMaps = new MapDocument?[Width, Height + 1];

			for (int y = 0; y < Height; y++)
			for (int x = 0; x < Width; x++)
				newMaps[x, y] = Maps[x, y];

			Maps = newMaps;
			Height++;
			Changed?.Invoke();
		}

		private void ExpandWest()
		{
			var newMaps = new MapDocument?[Width + 1, Height ];

			for (int y = 0; y < Height; y++)
			for (int x = 0; x < Width; x++)
				newMaps[x + 1, y] = Maps[x, y];

			Maps = newMaps;
			Width++;
			Changed?.Invoke();
		}

		private void ExpandEast()
		{
			var newMaps = new MapDocument?[Width + 1, Height];

			for (int y = 0; y < Height; y++)
			for (int x = 0; x < Width; x++)
				newMaps[x, y] = Maps[x, y];

			Maps = newMaps;
			Width++;
			Changed?.Invoke();
		}
	}
}
