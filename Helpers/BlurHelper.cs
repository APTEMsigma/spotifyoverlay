using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SpotifyOverlay
{
    public static class BlurHelper
    {
        /// <summary>
        /// Creates a moderately blurred, high-fidelity background texture (160x160).
        /// Preserves album art composition, shapes and silhouettes while smoothly softening pixels.
        /// </summary>
        public static BitmapSource? CreatePreBlurredBackground(BitmapSource? source)
        {
            if (source == null) return null;

            try
            {
                // Moderate resolution (160x160) preserves artwork silhouettes & details
                var targetWidth = 160;
                var targetHeight = 160;

                var smallBmp = new TransformedBitmap(source, new ScaleTransform(
                    (double)targetWidth / Math.Max(1, source.PixelWidth),
                    (double)targetHeight / Math.Max(1, source.PixelHeight)));

                var converted = new FormatConvertedBitmap(smallBmp, PixelFormats.Bgra32, null, 0);
                int stride = targetWidth * 4;
                byte[] pixels = new byte[targetHeight * stride];
                converted.CopyPixels(pixels, stride, 0);

                // Gentle blur pass: keeps shapes recognizable
                BoxBlur(pixels, targetWidth, targetHeight, 2);

                var result = BitmapSource.Create(
                    targetWidth, targetHeight,
                    96, 96,
                    PixelFormats.Bgra32,
                    null,
                    pixels,
                    stride);

                result.Freeze();
                return result;
            }
            catch
            {
                return source;
            }
        }

        private static void BoxBlur(byte[] pixels, int width, int height, int radius)
        {
            int length = width * height;
            byte[] temp = new byte[pixels.Length];
            Array.Copy(pixels, temp, pixels.Length);

            // Horizontal pass
            for (int y = 0; y < height; y++)
            {
                int rowOffset = y * width * 4;
                for (int x = 0; x < width; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0, count = 0;
                    for (int kx = -radius; kx <= radius; kx++)
                    {
                        int px = Math.Clamp(x + kx, 0, width - 1);
                        int idx = rowOffset + px * 4;
                        b += temp[idx];
                        g += temp[idx + 1];
                        r += temp[idx + 2];
                        a += temp[idx + 3];
                        count++;
                    }
                    int outIdx = rowOffset + x * 4;
                    pixels[outIdx] = (byte)(b / count);
                    pixels[outIdx + 1] = (byte)(g / count);
                    pixels[outIdx + 2] = (byte)(r / count);
                    pixels[outIdx + 3] = (byte)(a / count);
                }
            }

            Array.Copy(pixels, temp, pixels.Length);

            // Vertical pass
            for (int x = 0; x < width; x++)
            {
                int colOffset = x * 4;
                for (int y = 0; y < height; y++)
                {
                    int r = 0, g = 0, b = 0, a = 0, count = 0;
                    for (int ky = -radius; ky <= radius; ky++)
                    {
                        int py = Math.Clamp(y + ky, 0, height - 1);
                        int idx = (py * width * 4) + colOffset;
                        b += temp[idx];
                        g += temp[idx + 1];
                        r += temp[idx + 2];
                        a += temp[idx + 3];
                        count++;
                    }
                    int outIdx = (y * width * 4) + colOffset;
                    pixels[outIdx] = (byte)(b / count);
                    pixels[outIdx + 1] = (byte)(g / count);
                    pixels[outIdx + 2] = (byte)(r / count);
                    pixels[outIdx + 3] = (byte)(a / count);
                }
            }
        }
    }
}
