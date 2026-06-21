using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Glyphborn.Mapper.Tiles;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Glyphborn.Mapper.Editor
{
	public static class TilePreviewer
	{
		private static readonly Dictionary<int, WriteableBitmap> _previewCache = new();
		private static readonly Dictionary<int, WriteableBitmap> _thumbnailCache = new();
		private static readonly object _lock = new();
		private static readonly Vector _defaultDpi = new Vector(96, 96);

		public static WriteableBitmap GetPreview(Texture tex)
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

				bmp = CreateThumbnail(tex, thumbW, thumbH);
				_thumbnailCache[key] = bmp;
				return bmp;
			}
		}

		public static void ClearCache()
		{
			lock (_lock)
			{
				_previewCache.Clear();
				_thumbnailCache.Clear();
			}
		}

		// Replaces the old per-pixel SetPixel loop with a row-by-row block copy.
		// Texture packs each pixel as 0xAARRGGBB in a uint; on the little-endian
		// hardware .NET targets, that lays out in memory as [B, G, R, A] per pixel -
		// exactly what PixelFormat.Bgra8888 expects. So the source array can be
		// copied straight across without touching individual channels.
		private static unsafe WriteableBitmap TextureToBitmap(Texture tex)
		{
			var bmp = new WriteableBitmap(
				new PixelSize(tex.Width, tex.Height),
				_defaultDpi,
				PixelFormat.Bgra8888,
				AlphaFormat.Unpremul);

			using (var fb = bmp.Lock())
			{
				ReadOnlySpan<byte> src = MemoryMarshal.AsBytes(tex.Pixels.AsSpan());
				int rowBytes = tex.Width * 4;

				for (int y = 0; y < tex.Height; y++)
				{
					var srcRow = src.Slice(y * rowBytes, rowBytes);
					byte* dstPtr = (byte*)(fb.Address + y * fb.RowBytes).ToPointer();
					srcRow.CopyTo(new Span<byte>(dstPtr, rowBytes));
				}
			}

			return bmp;
		}

		// Nearest-neighbor downscale, sampled directly from the source texture
		// instead of rendering the full preview and scaling that down. Avalonia's
		// Bitmap.CreateScaledBitmap takes a BitmapInterpolationMode, but that enum
		// has no true "nearest neighbor" entry, and LowQuality has a history of not
		// reliably behaving like one (AvaloniaUI/Avalonia#8621). Sampling by hand
		// guarantees the same crisp, blocky look InterpolationMode.NearestNeighbor
		// gave you under GDI+.
		private static unsafe WriteableBitmap CreateThumbnail(Texture tex, int thumbW, int thumbH)
		{
			var bmp = new WriteableBitmap(
				new PixelSize(thumbW, thumbH),
				_defaultDpi,
				PixelFormat.Bgra8888,
				AlphaFormat.Unpremul);

			using (var fb = bmp.Lock())
			{
				for (int y = 0; y <  thumbH; y++)
				{
					int srcY = y * tex.Height / thumbH;
					byte* dstRow = (byte*)(fb.Address + y * fb.RowBytes).ToPointer();

					for (int x = 0; x < thumbW; x++)
					{
						int srcX = x * tex.Width / thumbW;
						uint pixel = tex.Pixels[srcY * tex.Width + srcX];

						// Same AARRGGBB -> BGRA little-endian layout as above, so one
						// 4-byte write reproduces what Color.FromArgb(a, r, g, b) did.
						*(uint*)(dstRow + x * 4) = pixel;
					}
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
					hash = (hash * 31) ^ (int)pixels[i];
				hash = (hash * 31) ^ pixels.Length;
				return hash;
			}
		}
	}
}
