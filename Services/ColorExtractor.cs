using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;

namespace SpotifyOverlay
{
    public class ExtractedTheme
    {
        public Color DominantColor { get; set; } = Color.FromRgb(20, 20, 25);
        public Color SecondaryColor { get; set; } = Color.FromRgb(10, 10, 15);
        public Color AccentColor { get; set; } = Color.FromRgb(29, 185, 84);
        public Brush BackgroundBrush { get; set; } = new SolidColorBrush(Color.FromArgb(135, 18, 18, 24));
        public Brush BlurBrush { get; set; } = new SolidColorBrush(Color.FromArgb(95, 18, 18, 24));
    }

    public static class ColorExtractor
    {
        public static ExtractedTheme ExtractTheme(BitmapSource? bitmap)
        {
            if (bitmap == null)
            {
                return GetDefaultTheme();
            }

            try
            {
                var smallBmp = new TransformedBitmap(bitmap, new ScaleTransform(
                    32.0 / Math.Max(1, bitmap.PixelWidth),
                    32.0 / Math.Max(1, bitmap.PixelHeight)));

                var formatConvertedBmp = new FormatConvertedBitmap();
                formatConvertedBmp.BeginInit();
                formatConvertedBmp.Source = smallBmp;
                formatConvertedBmp.DestinationFormat = PixelFormats.Bgra32;
                formatConvertedBmp.EndInit();

                int width = formatConvertedBmp.PixelWidth;
                int height = formatConvertedBmp.PixelHeight;
                int stride = width * 4;
                byte[] pixels = new byte[height * stride];
                formatConvertedBmp.CopyPixels(pixels, stride, 0);

                var colorCandidates = new List<(Color color, double score)>();
                int totalR = 0, totalG = 0, totalB = 0;
                int count = 0;

                for (int i = 0; i < pixels.Length; i += 4)
                {
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];
                    byte a = pixels[i + 3];

                    if (a < 128) continue;

                    totalB += b;
                    totalG += g;
                    totalR += r;
                    count++;

                    RgbToHsv(r, g, b, out double h, out double s, out double v);

                    double saturationFactor = Math.Pow(s, 1.2);
                    double valueFactor = (v >= 0.15 && v <= 0.85) ? 1.0 : 0.3;
                    double score = saturationFactor * valueFactor + 0.1;

                    colorCandidates.Add((Color.FromRgb(r, g, b), score));
                }

                if (colorCandidates.Count == 0)
                {
                    return GetDefaultTheme();
                }

                var bestVibrant = colorCandidates.OrderByDescending(c => c.score).First().color;

                Color dominant = count > 0 
                    ? Color.FromRgb((byte)(totalR / count), (byte)(totalG / count), (byte)(totalB / count))
                    : bestVibrant;

                // Transparent, atmospheric tones matching album color (~50-55% alpha)
                byte darkR = (byte)Math.Clamp((int)(dominant.R * 0.5), 15, 80);
                byte darkG = (byte)Math.Clamp((int)(dominant.G * 0.5), 15, 80);
                byte darkB = (byte)Math.Clamp((int)(dominant.B * 0.5), 15, 80);
                Color darkTone = Color.FromArgb(135, darkR, darkG, darkB);

                byte secR = (byte)Math.Clamp((int)(bestVibrant.R * 0.3), 10, 50);
                byte secG = (byte)Math.Clamp((int)(bestVibrant.G * 0.3), 10, 50);
                byte secB = (byte)Math.Clamp((int)(bestVibrant.B * 0.3), 10, 50);
                Color darkerTone = Color.FromArgb(150, secR, secG, secB);

                var gradient = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint = new System.Windows.Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(darkTone, 0.0),
                        new GradientStop(darkerTone, 1.0)
                    }
                };
                gradient.Freeze();

                var blurGradient = new LinearGradientBrush
                {
                    StartPoint = new System.Windows.Point(0, 0),
                    EndPoint = new System.Windows.Point(1, 1),
                    GradientStops = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(95, darkR, darkG, darkB), 0.0),
                        new GradientStop(Color.FromArgb(110, secR, secG, secB), 1.0)
                    }
                };
                blurGradient.Freeze();

                return new ExtractedTheme
                {
                    DominantColor = darkTone,
                    SecondaryColor = darkerTone,
                    AccentColor = bestVibrant,
                    BackgroundBrush = gradient,
                    BlurBrush = blurGradient
                };
            }
            catch
            {
                return GetDefaultTheme();
            }
        }

        public static ExtractedTheme GetDefaultTheme()
        {
            var grad = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(135, 18, 18, 24), 0.0),
                    new GradientStop(Color.FromArgb(155, 10, 10, 14), 1.0)
                }
            };
            grad.Freeze();

            var blurGrad = new LinearGradientBrush
            {
                StartPoint = new System.Windows.Point(0, 0),
                EndPoint = new System.Windows.Point(1, 1),
                GradientStops = new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb(95, 18, 18, 24), 0.0),
                    new GradientStop(Color.FromArgb(110, 10, 10, 14), 1.0)
                }
            };
            blurGrad.Freeze();

            return new ExtractedTheme
            {
                DominantColor = Color.FromRgb(18, 18, 24),
                SecondaryColor = Color.FromRgb(10, 10, 14),
                AccentColor = Color.FromRgb(29, 185, 84),
                BackgroundBrush = grad,
                BlurBrush = blurGrad
            };
        }

        private static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
        {
            double rd = r / 255.0;
            double gd = g / 255.0;
            double bd = b / 255.0;

            double max = Math.Max(rd, Math.Max(gd, bd));
            double min = Math.Min(rd, Math.Min(gd, bd));
            double delta = max - min;

            v = max;
            s = max == 0 ? 0 : delta / max;

            if (delta == 0)
            {
                h = 0;
            }
            else if (max == rd)
            {
                h = 60 * (((gd - bd) / delta) % 6);
            }
            else if (max == gd)
            {
                h = 60 * (((bd - rd) / delta) + 2);
            }
            else
            {
                h = 60 * (((rd - gd) / delta) + 4);
            }

            if (h < 0) h += 360;
        }
    }
}
