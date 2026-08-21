using System.Windows.Media;
using System.Windows.Shapes;
using Color = System.Windows.Media.Color;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace SpotifyOverlay
{
    public enum RingColorOption
    {
        Silver,       // Металлик (CD)
        Adaptive,     // В цвет обложки (Адаптивный)
        White,        // Белый
        SpotifyGreen, // Spotify Зеленый
        Black,        // Черный
        Gold,         // Золотой
        Cyan,         // Неоновый Голубой
        None          // Без кольца
    }

    public static class RingColorHelper
    {
        public static void ApplyRingColor(Ellipse ringEllipse, RingColorOption option, Color? currentAccentColor = null, bool isFullscreen = false)
        {
            double strokeThickness = isFullscreen ? 2.0 : 1.2;

            if (option == RingColorOption.None)
            {
                ringEllipse.Stroke = Brushes.Transparent;
                ringEllipse.StrokeThickness = 0;
                return;
            }

            ringEllipse.StrokeThickness = strokeThickness;

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
                    ringEllipse.Stroke = silverBrush;
                    break;

                case RingColorOption.Adaptive:
                    Color accent = currentAccentColor ?? Color.FromRgb(29, 185, 84);
                    var adaptiveBrush = new SolidColorBrush(Color.FromArgb(235, accent.R, accent.G, accent.B));
                    adaptiveBrush.Freeze();
                    ringEllipse.Stroke = adaptiveBrush;
                    break;

                case RingColorOption.White:
                    var whiteBrush = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255));
                    whiteBrush.Freeze();
                    ringEllipse.Stroke = whiteBrush;
                    break;

                case RingColorOption.SpotifyGreen:
                    var greenBrush = new SolidColorBrush(Color.FromRgb(29, 185, 84));
                    greenBrush.Freeze();
                    ringEllipse.Stroke = greenBrush;
                    break;

                case RingColorOption.Black:
                    var blackBrush = new SolidColorBrush(Color.FromArgb(180, 20, 20, 26));
                    blackBrush.Freeze();
                    ringEllipse.Stroke = blackBrush;
                    break;

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
                    ringEllipse.Stroke = goldBrush;
                    break;

                case RingColorOption.Cyan:
                    var cyanBrush = new SolidColorBrush(Color.FromRgb(0, 229, 255));
                    cyanBrush.Freeze();
                    ringEllipse.Stroke = cyanBrush;
                    break;
            }
        }
    }
}
