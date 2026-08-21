using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace SpotifyOverlay
{
    public enum RingColorOption
    {
        Silver,       // Silver / Metallic
        Adaptive,     // Adaptive (Cover Color)
        White,        // White
        SpotifyGreen, // Spotify Green
        Black,        // Black
        Gold,         // Gold
        Cyan,         // Neon Cyan
        None          // No Ring
    }

    public enum CoverShapeOption
    {
        Circle, // Spinning Circle
        Square  // Static Rounded Square
    }

    public static class RingColorHelper
    {
        public static Brush GetRingBrush(RingColorOption option, Color? currentAccentColor = null)
        {
            if (option == RingColorOption.None)
            {
                return Brushes.Transparent;
            }

            switch (option)
            {
                case RingColorOption.Silver:
                    var silverBrush = new LinearGradientBrush
                    {
                        StartPoint = new System.Windows.Point(0, 0),
                        EndPoint = new System.Windows.Point(1, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromRgb(240, 244, 252), 0.0),
                            new GradientStop(Color.FromRgb(165, 175, 190), 0.35),
                            new GradientStop(Color.FromRgb(250, 252, 255), 0.7),
                            new GradientStop(Color.FromRgb(140, 150, 168), 1.0)
                        }
                    };
                    silverBrush.Freeze();
                    return silverBrush;

                case RingColorOption.Adaptive:
                    Color accent = currentAccentColor ?? Color.FromRgb(29, 185, 84);
                    var adaptiveBrush = new SolidColorBrush(Color.FromArgb(235, accent.R, accent.G, accent.B));
                    adaptiveBrush.Freeze();
                    return adaptiveBrush;

                case RingColorOption.White:
                    var whiteBrush = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255));
                    whiteBrush.Freeze();
                    return whiteBrush;

                case RingColorOption.SpotifyGreen:
                    var greenBrush = new SolidColorBrush(Color.FromRgb(29, 185, 84));
                    greenBrush.Freeze();
                    return greenBrush;

                case RingColorOption.Black:
                    var blackBrush = new SolidColorBrush(Color.FromArgb(180, 20, 20, 26));
                    blackBrush.Freeze();
                    return blackBrush;

                case RingColorOption.Gold:
                    var goldBrush = new LinearGradientBrush
                    {
                        StartPoint = new System.Windows.Point(0, 0),
                        EndPoint = new System.Windows.Point(1, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromRgb(255, 235, 150), 0.0),
                            new GradientStop(Color.FromRgb(200, 155, 50), 0.5),
                            new GradientStop(Color.FromRgb(255, 220, 120), 1.0)
                        }
                    };
                    goldBrush.Freeze();
                    return goldBrush;

                case RingColorOption.Cyan:
                    var cyanBrush = new SolidColorBrush(Color.FromRgb(0, 229, 255));
                    cyanBrush.Freeze();
                    return cyanBrush;

                default:
                    return Brushes.Transparent;
            }
        }

        public static void ApplyRingColor(Ellipse? ringEllipse, Border? ringBorder, RingColorOption option, Color? currentAccentColor = null, bool isFullscreen = false)
        {
            double strokeThickness = isFullscreen ? 2.0 : 1.2;
            Brush brush = GetRingBrush(option, currentAccentColor);

            if (ringEllipse != null)
            {
                if (option == RingColorOption.None)
                {
                    ringEllipse.Stroke = Brushes.Transparent;
                    ringEllipse.StrokeThickness = 0;
                }
                else
                {
                    ringEllipse.Stroke = brush;
                    ringEllipse.StrokeThickness = strokeThickness;
                }
            }

            if (ringBorder != null)
            {
                if (option == RingColorOption.None)
                {
                    ringBorder.BorderBrush = Brushes.Transparent;
                    ringBorder.BorderThickness = new Thickness(0);
                }
                else
                {
                    ringBorder.BorderBrush = brush;
                    ringBorder.BorderThickness = new Thickness(strokeThickness);
                }
            }
        }
    }
}
