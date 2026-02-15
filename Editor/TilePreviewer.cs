using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

using Glyphborn.Mapper.Tiles;

namespace Glyphborn.Mapper.Editor
{
	public static class TilePreviewer
	{
		private static readonly Dictionary<int, Bitmap> _previewCache = new();
		private static readonly Dictionary<int, Bitmap> _thumbnailCache = new();
		private static readonly object _lock = new();

		public static Bitmap GetPreview(Texture tex)
		{
			if (tex == null) throw new ArgumentNullException(nameof(tex));
			int key = ComputeTextureHash(tex);

			lock (_lock)
			{
				if (_previewCache.TryGetValue(key, out var bmp) && bmp != null)
					return bmp;

				bmp = TextureToBitmap(tex);
				_previewCache[key] = bmp;
				return bmp;
			}
		}

		public static Bitmap GetThumbnail(Texture tex, int thumbW = 32, int thumbH = 32)
		{
			if (tex == null) throw new ArgumentNullException(nameof(tex));
			int key = (ComputeTextureHash(tex) * 397) ^ thumbW ^ thumbH;

			lock (_lock)
			{
				if (_thumbnailCache.TryGetValue(key, out var bmp) && bmp != null)
					return bmp;

				var source = GetPreview(tex);
				var thumb = new Bitmap(thumbW, thumbH);
				using (var g = Graphics.FromImage(thumb))
				{
					g.InterpolationMode = InterpolationMode.NearestNeighbor;
					g.DrawImage(source, 0, 0, thumbW, thumbH);
				}

				_thumbnailCache[key] = thumb;
				return thumb;
			}
		}

		public static void ClearCache()
		{
			lock (_lock)
			{
				foreach (var b in _previewCache.Values) b.Dispose();
				foreach (var b in _thumbnailCache.Values) b.Dispose();
				_previewCache.Clear();
				_thumbnailCache.Clear();
			}
		}

		private static Bitmap TextureToBitmap(Texture tex)
		{
			var bmp = new Bitmap(tex.Width, tex.Height);

			for (int y = 0; y < tex.Height; y++)
			{
				for (int x = 0; x < tex.Width; x++)
				{
					uint pixel = tex.Pixels[y * tex.Width + x];

					byte a = (byte) ((pixel >> 24) & 0xFF);
					byte r = (byte) ((pixel >> 16) & 0xFF);
					byte g = (byte) ((pixel >> 8) & 0xFF);
					byte b = (byte) (pixel & 0xFF);

					bmp.SetPixel(x, y, Color.FromArgb(a, r, g, b));
				}
			}

			return bmp;
		}

		private static int ComputeTextureHash(Texture tex)
		{
			unchecked
			{
				int hash = tex.Width * 397 ^ tex.Height;
				var pixels = tex.Pixels;
				// sample pixels to avoid iterating a huge array every time; still good collision resistance
				int step = Math.Max(1, pixels.Length / 64);
				for (int i = 0; i < pixels.Length; i++)
					hash = (hash * 31) ^ (int) pixels[i];
				hash = (hash * 31) ^ pixels.Length;
				return hash;
			}
		}
	}
}
