using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Color = System.Windows.Media.Color;

namespace SpotifyOverlay
{
    public class AppSettings
    {
        public double WindowLeft { get; set; } = 100;
        public double WindowTop { get; set; } = 100;
        public bool IsLocked { get; set; } = false;
        public bool IsTopmost { get; set; } = true;
        public int TargetFps { get; set; } = 0; // 0 = Auto / Monitor VSync native
        public bool IsMarqueeEnabled { get; set; } = true;
        public RingColorOption RingColor { get; set; } = RingColorOption.Silver;
    }

    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        private readonly SpotifyMediaService _mediaService = new();
        private readonly DispatcherTimer _progressTimer = new();
        private Forms.NotifyIcon? _notifyIcon;
        private FullscreenOverlayWindow? _fullscreenWindow;
        private bool _isLocked = false;
        private int _targetFps = 0;
        private bool _isMarqueeEnabled = true;
        private RingColorOption _currentRingColor = RingColorOption.Silver;
        private Color? _currentAccentColor;
        private string _lastTrackTitle = string.Empty;
        private readonly string _settingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "SpotifyOverlay", "settings.json");

        // High-precision subpixel animation state
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private double _lastRenderTime = 0;
        private double _discAngle = 0;
        private const double DiscRotationSpeed = 38.0; // degrees per second
        private double _marqueeX = 0;
        private double _loopDistance = 0;
        private const double MarqueeSpeed = 26.0; // pixels per second
        private bool _isMarqueeActive = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
            InitializeTrayIcon();

            _progressTimer.Interval = TimeSpan.FromMilliseconds(300);
            _progressTimer.Tick += ProgressTimer_Tick;
            _progressTimer.Start();

            _mediaService.TrackChanged += MediaService_TrackChanged;
            _mediaService.PlaybackStateChanged += MediaService_PlaybackStateChanged;
            _mediaService.PositionChanged += MediaService_PositionChanged;

            CompositionTarget.Rendering += CompositionTarget_Rendering;

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void InitializeTrayIcon()
        {
            try
            {
                _notifyIcon = new Forms.NotifyIcon
                {
                    Text = "Spotify Overlay",
                    Icon = CreateTrayIcon(),
                    Visible = true
                };

                _notifyIcon.MouseClick += NotifyIcon_MouseClick;
                _notifyIcon.DoubleClick += NotifyIcon_DoubleClick;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TrayIcon] Error: {ex.Message}");
            }
        }

        private static System.Drawing.Icon CreateTrayIcon()
        {
            using var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(System.Drawing.Color.Transparent);

                // Spotify green base circle
                using var brush = new SolidBrush(System.Drawing.Color.FromArgb(29, 185, 84));
                g.FillEllipse(brush, 2, 2, 28, 28);

                // Thin silver/white ring
                using var ringPen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(220, 255, 255, 255), 1.5f);
                g.DrawEllipse(ringPen, 5, 5, 22, 22);

                // Inner musical note / symbol
                using var noteBrush = new SolidBrush(System.Drawing.Color.FromArgb(20, 20, 26));
                g.FillEllipse(noteBrush, 12, 12, 8, 8);
            }

            IntPtr hIcon = bmp.GetHicon();
            return System.Drawing.Icon.FromHandle(hIcon);
        }

        private void NotifyIcon_MouseClick(object? sender, Forms.MouseEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateVisibilityMenuHeader();
                PopulateFullscreenMenu();
                UpdateRingColorMenu();
                MainContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
                MainContextMenu.IsOpen = true;

                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                if (helper.Handle != IntPtr.Zero)
                {
                    SetForegroundWindow(helper.Handle);
                }
            });
        }

        private void NotifyIcon_DoubleClick(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(ToggleOverlayVisibility);
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            UpdateVisibilityMenuHeader();
            PopulateFullscreenMenu();
            UpdateRingColorMenu();
            MenuAutoStartupItem.IsChecked = AutoStartupHelper.IsAutoStartupEnabled();
        }

        private void UpdateRingColorMenu()
        {
            foreach (var item in MenuRingColorSubmenu.Items)
            {
                if (item is System.Windows.Controls.MenuItem mi && mi.Tag is string tagStr)
                {
                    if (Enum.TryParse<RingColorOption>(tagStr, out var opt))
                    {
                        mi.IsCheckable = true;
                        mi.IsChecked = _currentRingColor == opt;
                    }
                }
            }
        }

        public void SetRingColor(RingColorOption option)
        {
            _currentRingColor = option;
            RingColorHelper.ApplyRingColor(DiscOuterRing, option, _currentAccentColor, false);
            _fullscreenWindow?.SetRingColor(option);
            UpdateRingColorMenu();
            SaveSettings();
        }

        private void MenuRingColor_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is string tagStr)
            {
                if (Enum.TryParse<RingColorOption>(tagStr, out var opt))
                {
                    SetRingColor(opt);
                }
            }
        }

        private void PopulateFullscreenMenu()
        {
            MenuFullscreenSubmenu.Items.Clear();

            bool isFullscreenActive = _fullscreenWindow != null && _fullscreenWindow.IsVisible;

            // Compact mode option
            var compactItem = new System.Windows.Controls.MenuItem
            {
                Header = "Compact Widget",
                IsCheckable = true,
                IsChecked = !isFullscreenActive
            };
            compactItem.Click += (s, e) =>
            {
                if (_fullscreenWindow != null && _fullscreenWindow.IsVisible)
                {
                    _fullscreenWindow.ExitFullscreen();
                }
                Visibility = Visibility.Visible;
                Activate();
            };
            MenuFullscreenSubmenu.Items.Add(compactItem);

            MenuFullscreenSubmenu.Items.Add(new Separator { Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)) });

            // Screens list
            var screens = Forms.Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];
                string screenLabel = $"Monitor {i + 1} ({screen.Bounds.Width}x{screen.Bounds.Height})";
                if (screen.Primary) screenLabel += " [Primary]";

                var monitorItem = new System.Windows.Controls.MenuItem
                {
                    Header = screenLabel,
                    Tag = screen,
                    IsCheckable = true,
                    IsChecked = isFullscreenActive && _fullscreenWindow != null && _fullscreenWindow.Left == screen.Bounds.Left && _fullscreenWindow.Top == screen.Bounds.Top
                };

                monitorItem.Click += (s, e) =>
                {
                    if (s is System.Windows.Controls.MenuItem mi && mi.Tag is Forms.Screen targetScreen)
                    {
                        SwitchToFullscreen(targetScreen);
                    }
                };

                MenuFullscreenSubmenu.Items.Add(monitorItem);
            }
        }

        private void SwitchToFullscreen(Forms.Screen screen)
        {
            if (_fullscreenWindow == null)
            {
                _fullscreenWindow = new FullscreenOverlayWindow(_mediaService, _targetFps, _currentRingColor);
                _fullscreenWindow.ExitRequested += (s, e) =>
                {
                    Visibility = Visibility.Visible;
                    Activate();
                };
            }

            _fullscreenWindow.SetTargetScreen(screen);
            _fullscreenWindow.SetRingColor(_currentRingColor);
            _fullscreenWindow.Show();
            _fullscreenWindow.Activate();
            Visibility = Visibility.Collapsed;
        }

        private void ToggleOverlayVisibility()
        {
            if (_fullscreenWindow != null && _fullscreenWindow.IsVisible)
            {
                _fullscreenWindow.ExitFullscreen();
                Visibility = Visibility.Visible;
                Activate();
                return;
            }

            if (Visibility == Visibility.Visible)
            {
                Visibility = Visibility.Collapsed;
            }
            else
            {
                Visibility = Visibility.Visible;
                Activate();
            }
            UpdateVisibilityMenuHeader();
        }

        private void UpdateVisibilityMenuHeader()
        {
            MenuVisibilityItem.Header = (Visibility == Visibility.Visible || (_fullscreenWindow != null && _fullscreenWindow.IsVisible))
                ? "Hide Overlay to Tray" 
                : "Show Overlay";
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetRingColor(_currentRingColor);
            await _mediaService.InitializeAsync();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            CompositionTarget.Rendering -= CompositionTarget_Rendering;
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            if (_fullscreenWindow != null)
            {
                _fullscreenWindow.Close();
            }
            SaveSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFilePath))
                {
                    string json = File.ReadAllText(_settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        double screenWidth = SystemParameters.PrimaryScreenWidth;
                        double screenHeight = SystemParameters.PrimaryScreenHeight;

                        Left = Math.Max(0, Math.Min(settings.WindowLeft, screenWidth - Width));
                        Top = Math.Max(0, Math.Min(settings.WindowTop, screenHeight - Height));
                        _isLocked = settings.IsLocked;
                        Topmost = settings.IsTopmost;
                        _targetFps = settings.TargetFps;
                        _isMarqueeEnabled = settings.IsMarqueeEnabled;
                        _currentRingColor = settings.RingColor;

                        MenuTopmostItem.IsChecked = Topmost;
                        MenuLockItem.IsChecked = _isLocked;
                        MenuMarqueeItem.IsChecked = _isMarqueeEnabled;
                        UpdateRingColorMenu();
                        return;
                    }
                }
            }
            catch { }

            Left = SystemParameters.WorkArea.Width - Width - 40;
            Top = 60;
        }

        private void SaveSettings()
        {
            try
            {
                var dir = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var settings = new AppSettings
                {
                    WindowLeft = Left,
                    WindowTop = Top,
                    IsLocked = _isLocked,
                    IsTopmost = Topmost,
                    TargetFps = _targetFps,
                    IsMarqueeEnabled = _isMarqueeEnabled,
                    RingColor = _currentRingColor
                };

                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_settingsFilePath, json);
            }
            catch { }
        }

        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (Visibility != Visibility.Visible) return;

            double currentTime = _stopwatch.Elapsed.TotalSeconds;
            double dt = currentTime - _lastRenderTime;

            if (_targetFps > 0)
            {
                double targetFrameTime = 1.0 / _targetFps;
                if (dt < targetFrameTime)
                {
                    return;
                }
            }

            _lastRenderTime = currentTime;

            if (dt > 0.1) dt = 0.016;

            // Smooth hardware accelerated circular rotation
            if (_mediaService.CurrentTrack.IsPlaying)
            {
                _discAngle = (_discAngle + DiscRotationSpeed * dt) % 360.0;
                DiscRotateTransform.Angle = _discAngle;
            }

            // Smooth infinite marquee
            if (_isMarqueeEnabled && _isMarqueeActive && _loopDistance > 0)
            {
                _marqueeX -= MarqueeSpeed * dt;
                if (_marqueeX <= -_loopDistance)
                {
                    _marqueeX += _loopDistance;
                }
                MarqueeTransform.X = _marqueeX;
            }
        }

        private void MediaService_TrackChanged(object? sender, TrackChangedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _lastTrackTitle = e.Title;
                UpdateMarqueeAnimation();

                if (_notifyIcon != null)
                {
                    string tip = $"{e.Title} - {e.Artist}";
                    if (tip.Length > 63) tip = tip.Substring(0, 60) + "...";
                    _notifyIcon.Text = tip;
                }

                ArtistText.Text = string.IsNullOrEmpty(e.Album) 
                    ? e.Artist 
                    : $"{e.Artist} • {e.Album}";

                if (e.AlbumArt != null)
                {
                    AlbumCoverImage.Source = e.AlbumArt;
                    AlbumCoverImage.Visibility = Visibility.Visible;
                    DefaultArtBorder.Visibility = Visibility.Collapsed;

                    var theme = ColorExtractor.ExtractTheme(e.AlbumArt);
                    _currentAccentColor = theme.AccentColor;
                    ApplyTheme(theme);

                    if (_currentRingColor == RingColorOption.Adaptive)
                    {
                        RingColorHelper.ApplyRingColor(DiscOuterRing, _currentRingColor, _currentAccentColor, false);
                    }
                }
                else
                {
                    AlbumCoverImage.Source = null;
                    AlbumCoverImage.Visibility = Visibility.Collapsed;
                    DefaultArtBorder.Visibility = Visibility.Visible;
                    ApplyTheme(ColorExtractor.GetDefaultTheme());
                }

                UpdateTimelineUI(e.Position, e.Duration);
            });
        }

        private void TitleContainer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateMarqueeAnimation();
        }

        private void UpdateMarqueeAnimation()
        {
            string title = string.IsNullOrWhiteSpace(_lastTrackTitle) ? "Spotify is not running" : _lastTrackTitle;
            TrackTitleEllipsisText.Text = title;

            if (!_isMarqueeEnabled)
            {
                TrackTitleEllipsisText.Visibility = Visibility.Visible;
                MarqueeTrack.Visibility = Visibility.Collapsed;
                _isMarqueeActive = false;
                return;
            }

            TrackTitleEllipsisText.Visibility = Visibility.Collapsed;
            MarqueeTrack.Visibility = Visibility.Visible;

            _marqueeX = 0;
            MarqueeTransform.X = 0;

            var typeface = new Typeface(TrackTitleText1.FontFamily, TrackTitleText1.FontStyle, TrackTitleText1.FontWeight, TrackTitleText1.FontStretch);
            var formattedTitle = new FormattedText(
                title,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                typeface,
                TrackTitleText1.FontSize,
                System.Windows.Media.Brushes.White,
                VisualTreeHelper.GetDpi(this).PixelsPerDip);

            double textWidth = formattedTitle.WidthIncludingTrailingWhitespace;
            double containerWidth = TitleContainer.ActualWidth > 0 ? TitleContainer.ActualWidth : 180;

            if (textWidth > containerWidth + 2)
            {
                string separator = "       •       ";
                var formattedSep = new FormattedText(
                    separator,
                    System.Globalization.CultureInfo.CurrentCulture,
                    System.Windows.FlowDirection.LeftToRight,
                    typeface,
                    TrackTitleText1.FontSize,
                    System.Windows.Media.Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);

                double sepWidth = formattedSep.WidthIncludingTrailingWhitespace;
                _loopDistance = textWidth + sepWidth;

                TrackTitleText1.Text = title;
                TrackTitleText2.Text = separator + title;
                TrackTitleText2.Visibility = Visibility.Visible;

                Canvas.SetLeft(TrackTitleText1, 0);
                Canvas.SetLeft(TrackTitleText2, textWidth);

                _isMarqueeActive = true;
            }
            else
            {
                TrackTitleText1.Text = title;
                TrackTitleText2.Visibility = Visibility.Collapsed;
                Canvas.SetLeft(TrackTitleText1, 0);
                _isMarqueeActive = false;
            }
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

            // Total available width for progress track: 238 - (44+10) - (26+26+8) = 124px
            double barContainerWidth = 124;
            double activeWidth = barContainerWidth * progressRatio;

            ActiveProgressBar.Width = activeWidth;
            ProgressThumb.Margin = new Thickness(Math.Max(0, activeWidth - 3), 0, 0, 0);
        }

        private static string FormatTime(TimeSpan time)
        {
            if (time < TimeSpan.Zero) time = TimeSpan.Zero;
            return time.TotalHours >= 1
                ? time.ToString(@"h\:mm\:ss")
                : time.ToString(@"m\:ss");
        }

        private void ApplyTheme(ExtractedTheme theme)
        {
            CardCoreBorder.Background = theme.BackgroundBrush;
            SmudgeBlurLayer.Background = theme.BlurBrush;

            var accentBrush = new SolidColorBrush(theme.AccentColor);
            accentBrush.Freeze();
            ActiveProgressBar.Background = accentBrush;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!_isLocked && e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
                SaveSettings();
            }
        }

        private async void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            await _mediaService.TogglePlayPauseAsync();
        }

        private async void ProgressBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            await _mediaService.TogglePlayPauseAsync();
        }

        private void MenuToggleVisibility_Click(object sender, RoutedEventArgs e)
        {
            ToggleOverlayVisibility();
        }

        private void MenuMarquee_Click(object sender, RoutedEventArgs e)
        {
            _isMarqueeEnabled = MenuMarqueeItem.IsChecked;
            UpdateMarqueeAnimation();
            SaveSettings();
        }

        private void MenuFps_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new FpsDialog(_targetFps) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _targetFps = dialog.SelectedFps;
                SaveSettings();
            }
        }

        private async void MenuPlayPause_Click(object sender, RoutedEventArgs e)
        {
            await _mediaService.TogglePlayPauseAsync();
        }

        private async void MenuNext_Click(object sender, RoutedEventArgs e)
        {
            await _mediaService.NextTrackAsync();
        }

        private async void MenuPrev_Click(object sender, RoutedEventArgs e)
        {
            await _mediaService.PreviousTrackAsync();
        }

        private void MenuTopmostItem_Click(object sender, RoutedEventArgs e)
        {
            Topmost = MenuTopmostItem.IsChecked;
            SaveSettings();
        }

        private void MenuTopmost_Click(object sender, RoutedEventArgs e)
        {
            Topmost = MenuTopmostItem.IsChecked;
            SaveSettings();
        }

        private void MenuLock_Click(object sender, RoutedEventArgs e)
        {
            _isLocked = MenuLockItem.IsChecked;
            SaveSettings();
        }

        private void MenuAutoStartup_Click(object sender, RoutedEventArgs e)
        {
            AutoStartupHelper.SetAutoStartup(MenuAutoStartupItem.IsChecked);
            MenuAutoStartupItem.IsChecked = AutoStartupHelper.IsAutoStartupEnabled();
        }

        private void MenuExit_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            System.Windows.Application.Current.Shutdown();
        }
    }
}