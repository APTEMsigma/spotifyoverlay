using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Color = System.Windows.Media.Color;

namespace SpotifyOverlay
{
    public partial class FullscreenOverlayWindow : Window
    {
        private readonly SpotifyMediaService _mediaService;
        private readonly DispatcherTimer _progressTimer = new();
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private double _lastRenderTime = 0;
        private double _discAngle = 0;
        private const double DiscRotationSpeed = 30.0;
        private int _targetFps = 0;
        private RingColorOption _ringColorOption = RingColorOption.Silver;
        private CoverShapeOption _coverShape = CoverShapeOption.Circle;
        private Color? _currentAccentColor;

        public event EventHandler? ExitRequested;

        public FullscreenOverlayWindow(SpotifyMediaService mediaService, int targetFps = 0, RingColorOption ringColor = RingColorOption.Silver, CoverShapeOption coverShape = CoverShapeOption.Circle)
        {
            InitializeComponent();
            _mediaService = mediaService;
            _targetFps = targetFps;
            _ringColorOption = ringColor;
            _coverShape = coverShape;

            _progressTimer.Interval = TimeSpan.FromMilliseconds(300);
            _progressTimer.Tick += ProgressTimer_Tick;
            _progressTimer.Start();

            _mediaService.TrackChanged += MediaService_TrackChanged;
            _mediaService.PlaybackStateChanged += MediaService_PlaybackStateChanged;
            _mediaService.PositionChanged += MediaService_PositionChanged;

            CompositionTarget.Rendering += CompositionTarget_Rendering;

            Loaded += FullscreenOverlayWindow_Loaded;
            Closing += FullscreenOverlayWindow_Closing;
        }

        public void SetTargetScreen(Forms.Screen screen)
        {
            var bounds = screen.Bounds;
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
        }

        public void SetRingColor(RingColorOption option)
        {
            _ringColorOption = option;
            RingColorHelper.ApplyRingColor(DiscOuterRing, SquareOuterRing, option, _currentAccentColor, true);
        }

        public void SetCoverShape(CoverShapeOption shape)
        {
            _coverShape = shape;
            if (shape == CoverShapeOption.Square)
            {
                CoverCircleContainer.Visibility = Visibility.Collapsed;
                CoverSquareContainer.Visibility = Visibility.Visible;
                CircleShadowLayer.Visibility = Visibility.Collapsed;
                SquareShadowLayer.Visibility = Visibility.Visible;
                DiscRotateTransform.Angle = 0;
            }
            else
            {
                CoverCircleContainer.Visibility = Visibility.Visible;
                CoverSquareContainer.Visibility = Visibility.Collapsed;
                CircleShadowLayer.Visibility = Visibility.Visible;
                SquareShadowLayer.Visibility = Visibility.Collapsed;
            }
            SetRingColor(_ringColorOption);
        }

        public void SetTargetFps(int fps)
        {
            _targetFps = fps;
        }

        private void FullscreenOverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetCoverShape(_coverShape);
            SetRingColor(_ringColorOption);
            UpdateTrackUI(_mediaService.CurrentTrack);
        }

        private void FullscreenOverlayWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (Visibility != Visibility.Visible) return;
            if (_coverShape == CoverShapeOption.Square || !_mediaService.CurrentTrack.IsPlaying) return;

            double currentTime = _stopwatch.Elapsed.TotalSeconds;
            double dt = currentTime - _lastRenderTime;

            if (_targetFps > 0)
            {
                double targetFrameTime = 1.0 / _targetFps;
                if (dt < targetFrameTime) return;
            }

            _lastRenderTime = currentTime;
            if (dt > 0.1) dt = 0.016;

            _discAngle = (_discAngle + DiscRotationSpeed * dt) % 360.0;
            DiscRotateTransform.Angle = _discAngle;
        }

        private void MediaService_TrackChanged(object? sender, TrackChangedEventArgs e)
        {
            Dispatcher.Invoke(() => UpdateTrackUI(e));
        }

        private void UpdateTrackUI(TrackChangedEventArgs e)
        {
            TrackTitleText.Text = string.IsNullOrWhiteSpace(e.Title) ? "Spotify is not running" : e.Title;
            ArtistText.Text = string.IsNullOrEmpty(e.Album) 
                ? e.Artist 
                : $"{e.Artist} • {e.Album}";

            if (e.AlbumArt != null)
            {
                AlbumCoverImageCircle.Source = e.AlbumArt;
                AlbumCoverImageSquare.Source = e.AlbumArt;
                
                // Fast one-time memory blur for 0% continuous GPU overhead
                BlurredBackgroundImage.Source = BlurHelper.CreatePreBlurredBackground(e.AlbumArt);

                AlbumCoverImageCircle.Visibility = Visibility.Visible;
                AlbumCoverImageSquare.Visibility = Visibility.Visible;
                DefaultArtBorderCircle.Visibility = Visibility.Collapsed;
                DefaultArtBorderSquare.Visibility = Visibility.Collapsed;

                var theme = ColorExtractor.ExtractTheme(e.AlbumArt);
                _currentAccentColor = theme.AccentColor;
                var accentBrush = new SolidColorBrush(theme.AccentColor);
                accentBrush.Freeze();
                ActiveProgressBar.Background = accentBrush;

                if (_ringColorOption == RingColorOption.Adaptive)
                {
                    RingColorHelper.ApplyRingColor(DiscOuterRing, SquareOuterRing, _ringColorOption, _currentAccentColor, true);
                }
            }
            else
            {
                AlbumCoverImageCircle.Source = null;
                AlbumCoverImageSquare.Source = null;
                BlurredBackgroundImage.Source = null;
                AlbumCoverImageCircle.Visibility = Visibility.Collapsed;
                AlbumCoverImageSquare.Visibility = Visibility.Collapsed;
                DefaultArtBorderCircle.Visibility = Visibility.Visible;
                DefaultArtBorderSquare.Visibility = Visibility.Visible;
            }

            UpdateTimelineUI(e.Position, e.Duration);
        }

        private void MediaService_PlaybackStateChanged(object? sender, bool isPlaying)
        {
            // State is read directly in CompositionTarget_Rendering
        }

        private void MediaService_PositionChanged(object? sender, TimeSpan position)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateTimelineUI(position, _mediaService.CurrentTrack.Duration);
            });
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (_mediaService.CurrentTrack.IsPlaying && _mediaService.CurrentTrack.Duration > TimeSpan.Zero)
            {
                var pos = _mediaService.GetCurrentEstimatedPosition();
                UpdateTimelineUI(pos, _mediaService.CurrentTrack.Duration);
            }
        }

        private void UpdateTimelineUI(TimeSpan position, TimeSpan duration)
        {
            CurrentTimeText.Text = FormatTime(position);
            TotalTimeText.Text = FormatTime(duration);

            double totalSeconds = duration.TotalSeconds;
            double currentSeconds = position.TotalSeconds;

            double progressRatio = 0;
            if (totalSeconds > 0)
            {
                progressRatio = Math.Clamp(currentSeconds / totalSeconds, 0, 1.0);
            }

            double barContainerWidth = 460;
            double activeWidth = barContainerWidth * progressRatio;

            ActiveProgressBar.Width = activeWidth;
            ProgressThumb.Margin = new Thickness(Math.Max(0, activeWidth - 6), 0, 0, 0);
        }

        private static string FormatTime(TimeSpan time)
        {
            if (time < TimeSpan.Zero) time = TimeSpan.Zero;
            return time.TotalHours >= 1
                ? time.ToString(@"h\:mm\:ss")
                : time.ToString(@"m\:ss");
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ExitFullscreen();
            }
            else if (e.Key == Key.Space)
            {
                _ = _mediaService.TogglePlayPauseAsync();
            }
            else if (e.Key == Key.Right)
            {
                _ = _mediaService.NextTrackAsync();
            }
            else if (e.Key == Key.Left)
            {
                _ = _mediaService.PreviousTrackAsync();
            }
        }

        private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            _ = _mediaService.TogglePlayPauseAsync();
        }

        private void ProgressBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            _ = _mediaService.TogglePlayPauseAsync();
        }

        private void BtnExitFullscreen_Click(object sender, RoutedEventArgs e)
        {
            ExitFullscreen();
        }

        public void ExitFullscreen()
        {
            Hide();
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
