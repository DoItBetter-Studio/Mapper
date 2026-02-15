using System;
using System.IO;

namespace Glyphborn.Mapper.Editor
{
	static class EditorPaths
	{
		private static string Root => Path.Combine(AppContext.BaseDirectory, "../..", "assets");

		public static string Tilesets => Path.Combine(Root, "tilesets");
		public static string Regional => Path.Combine(Tilesets, "regional");
		public static string Local => Path.Combine(Tilesets, "local");
		public static string Interior => Path.Combine(Tilesets, "interior");

		public static string Maps => Path.Combine(Root, "maps");
	}
}
