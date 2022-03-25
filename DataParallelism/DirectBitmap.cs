using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;

namespace DataParallelism
{
	public class DirectBitmap : IDisposable
	{
		public unsafe DirectBitmap(Bitmap bmp)
		{
			this.bmp = bmp;
			if(bmp.PixelFormat != PixelFormat.Format32bppArgb)
				throw new Exception($"Invalid image format '{bmp.PixelFormat}'");
			data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);
			ptr = (int*)data.Scan0;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe Color FastGetPixel(int x, int y)
		{
			if(x >= data.Width || y >= data.Height)
				throw new ArgumentOutOfRangeException();
			return Color.FromArgb(ptr[y * data.Width + x]);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe void FastSetPixel(int x, int y, Color color)
		{
			if(x >= data.Width || y >= data.Height)
				throw new ArgumentOutOfRangeException();
			ptr[y * data.Width + x] = color.ToArgb();
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe void FastSet20X20Pixel(int index, Color color)
		{
			if(index >= data.Width * data.Height)
				throw new ArgumentOutOfRangeException();

			var bigLine = index / (data.Width / 20);
			var bigColumn = index % (data.Width / 20);
			var realLine = bigLine * 20;
			var realColumn = bigColumn * 20;
			
			for (int j = 0; j < 20; j++)
			{
				for (var i = 0; i < 20; i++)
				{
					ptr[realLine * data.Width + realColumn + j + i * data.Width] = color.ToArgb();
				}
			}
		}

		public void Dispose()
		{
			bmp.UnlockBits(data);
			bmp.Dispose();
		}

		public int Width => data.Width;
		public int Height => data.Height;

		private readonly Bitmap bmp;
		private readonly BitmapData data;
		private readonly unsafe int* ptr;
	}

	public static class ColorHelper
	{
		public static Color GrayScale(this Color color)
		{
			var gray = (byte)(0.3 * color.R + 0.59 * color.G + 0.11 * color.B);
			return Color.FromArgb(color.A, gray, gray, gray);
		}
	}
}