using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace SpotifyOverlay
{
    public class TrackChangedEventArgs : EventArgs
    {
        public string Title { get; set; } = string.Empty;
        public string Artist { get; set; } = string.Empty;
        public string Album { get; set; } = string.Empty;
        public BitmapImage? AlbumArt { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan Position { get; set; }
        public bool IsPlaying { get; set; }
        public string SourceApp { get; set; } = string.Empty;
    }

    public class SpotifyMediaService
    {
        private GlobalSystemMediaTransportControlsSessionManager? _sessionManager;
        private GlobalSystemMediaTransportControlsSession? _currentSession;
        
        public event EventHandler<TrackChangedEventArgs>? TrackChanged;
        public event EventHandler<bool>? PlaybackStateChanged;
        public event EventHandler<TimeSpan>? PositionChanged;

        private TrackChangedEventArgs _currentTrack = new();
        private DateTimeOffset _lastTimelineUpdate = DateTimeOffset.UtcNow;
        private TimeSpan _lastReportedPosition = TimeSpan.Zero;

        public TrackChangedEventArgs CurrentTrack => _currentTrack;

        public async Task InitializeAsync()
        {
            try
            {
                _sessionManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (_sessionManager != null)
                {
                    _sessionManager.SessionsChanged += SessionManager_SessionsChanged;
                    _sessionManager.CurrentSessionChanged += SessionManager_CurrentSessionChanged;
                    UpdateCurrentSession();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SpotifyMediaService] Init error: {ex.Message}");
            }
        }

        private void SessionManager_SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        {
            UpdateCurrentSession();
        }

        private void SessionManager_CurrentSessionChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        {
            UpdateCurrentSession();
        }

        private void UpdateCurrentSession()
        {
            if (_sessionManager == null) return;

            try
            {
                var sessions = _sessionManager.GetSessions();
                
                // Prioritize Spotify if available, otherwise take the active/current session
                var spotifySession = sessions.FirstOrDefault(s => s.SourceAppUserModelId.ToLowerInvariant().Contains("spotify"));
                var targetSession = spotifySession ?? _sessionManager.GetCurrentSession() ?? sessions.FirstOrDefault();

                if (_currentSession != targetSession)
                {
                    if (_currentSession != null)
                    {
                        _currentSession.MediaPropertiesChanged -= CurrentSession_MediaPropertiesChanged;
                        _currentSession.PlaybackInfoChanged -= CurrentSession_PlaybackInfoChanged;
                        _currentSession.TimelinePropertiesChanged -= CurrentSession_TimelinePropertiesChanged;
                    }

                    _currentSession = targetSession;

                    if (_currentSession != null)
                    {
                        _currentSession.MediaPropertiesChanged += CurrentSession_MediaPropertiesChanged;
                        _currentSession.PlaybackInfoChanged += CurrentSession_PlaybackInfoChanged;
                        _currentSession.TimelinePropertiesChanged += CurrentSession_TimelinePropertiesChanged;
                    }
                }

                _ = RefreshAllAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SpotifyMediaService] Session update error: {ex.Message}");
            }
        }

        private void CurrentSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            _ = RefreshMediaPropertiesAsync();
        }

        private void CurrentSession_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            RefreshPlaybackInfo();
        }

        private void CurrentSession_TimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args)
        {
            RefreshTimeline();
        }

        public async Task RefreshAllAsync()
        {
            await RefreshMediaPropertiesAsync();
            RefreshPlaybackInfo();
            RefreshTimeline();
        }

        public async Task RefreshMediaPropertiesAsync()
        {
            if (_currentSession == null)
            {
                _currentTrack = new TrackChangedEventArgs
                {
                    Title = "Spotify не запущен",
                    Artist = "Ожидание воспроизведения...",
                    IsPlaying = false
                };
                TrackChanged?.Invoke(this, _currentTrack);
                return;
            }

            try
            {
                var props = await _currentSession.TryGetMediaPropertiesAsync();
                if (props != null)
                {
                    BitmapImage? bitmap = null;
                    if (props.Thumbnail != null)
                    {
                        bitmap = await LoadImageAsync(props.Thumbnail);
                    }

                    var playbackInfo = _currentSession.GetPlaybackInfo();
                    bool isPlaying = playbackInfo?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;

                    var timeline = _currentSession.GetTimelineProperties();

                    _currentTrack = new TrackChangedEventArgs
                    {
                        Title = string.IsNullOrWhiteSpace(props.Title) ? "Неизвестный трек" : props.Title,
                        Artist = string.IsNullOrWhiteSpace(props.Artist) ? "Неизвестный исполнитель" : props.Artist,
                        Album = props.AlbumTitle ?? string.Empty,
                        AlbumArt = bitmap,
                        Duration = timeline?.EndTime ?? TimeSpan.Zero,
                        Position = timeline?.Position ?? TimeSpan.Zero,
                        IsPlaying = isPlaying,
                        SourceApp = _currentSession.SourceAppUserModelId
                    };

                    _lastReportedPosition = _currentTrack.Position;
                    _lastTimelineUpdate = DateTimeOffset.UtcNow;

                    TrackChanged?.Invoke(this, _currentTrack);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SpotifyMediaService] Error reading props: {ex.Message}");
            }
        }

        private void RefreshPlaybackInfo()
        {
            if (_currentSession == null) return;
            try
            {
                var playbackInfo = _currentSession.GetPlaybackInfo();
                if (playbackInfo != null)
                {
                    bool isPlaying = playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    _currentTrack.IsPlaying = isPlaying;
                    PlaybackStateChanged?.Invoke(this, isPlaying);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SpotifyMediaService] Error playback info: {ex.Message}");
            }
        }

        private void RefreshTimeline()
        {
            if (_currentSession == null) return;
            try
            {
                var timeline = _currentSession.GetTimelineProperties();
                if (timeline != null)
                {
                    _currentTrack.Duration = timeline.EndTime;
                    _currentTrack.Position = timeline.Position;
                    _lastReportedPosition = timeline.Position;
                    _lastTimelineUpdate = DateTimeOffset.UtcNow;
                    PositionChanged?.Invoke(this, timeline.Position);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SpotifyMediaService] Error timeline: {ex.Message}");
            }
        }

        public TimeSpan GetCurrentEstimatedPosition()
        {
            if (!_currentTrack.IsPlaying)
            {
                return _lastReportedPosition;
            }

            var elapsed = DateTimeOffset.UtcNow - _lastTimelineUpdate;
            var estimated = _lastReportedPosition + elapsed;
            if (_currentTrack.Duration > TimeSpan.Zero && estimated > _currentTrack.Duration)
            {
                estimated = _currentTrack.Duration;
            }
            return estimated;
        }

        public async Task TogglePlayPauseAsync()
        {
            if (_currentSession != null)
            {
                try
                {
                    await _currentSession.TryTogglePlayPauseAsync();
                }
                catch { }
            }
        }

        public async Task NextTrackAsync()
        {
            if (_currentSession != null)
            {
                try
                {
                    await _currentSession.TrySkipNextAsync();
                }
                catch { }
            }
        }

        public async Task PreviousTrackAsync()
        {
            if (_currentSession != null)
            {
                try
                {
                    await _currentSession.TrySkipPreviousAsync();
                }
                catch { }
            }
        }

        private async Task<BitmapImage?> LoadImageAsync(IRandomAccessStreamReference streamRef)
        {
            try
            {
                using var stream = await streamRef.OpenReadAsync();
                using var netStream = stream.AsStreamForRead();
                
                var memoryStream = new MemoryStream();
                await netStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = memoryStream;
                bitmap.EndInit();
                bitmap.Freeze(); // Make cross-thread accessible and fast
                return bitmap;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SpotifyMediaService] LoadImage error: {ex.Message}");
                return null;
            }
        }
    }
}
