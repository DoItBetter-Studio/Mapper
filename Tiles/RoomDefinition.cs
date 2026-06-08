using System;
using System.Drawing;

namespace Glyphborn.Mapper.Tiles
{
	public sealed class RoomDefinition
	{
		private static readonly Random _random = new Random();

		public uint Id;
		public string Name = "";
		public Color Color = RandomColor();

		private static Color RandomColor()
		{
			return Color.FromArgb(_random.Next(0, 256), _random.Next(0, 256), _random.Next(0, 256));
		}
	}
}
