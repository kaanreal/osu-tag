using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Osutag.Services
{
    /// <summary>
    /// Audio playback service using FFplay (part of FFmpeg suite).
    /// </summary>
    public class AudioService : IDisposable
    {
        private static AudioService? _instance;
        public static AudioService Instance => _instance ??= new AudioService();

        private Process? _ffplayProcess;
        private CancellationTokenSource? _debounceCts;
        private CancellationTokenSource? _previewCts;
        private readonly object _lock = new();

        // State for live updates
        private string? _lastPath;
        private int _lastStartTimeMs;
        private int? _lastDurationMs;
        private float _lastRate = 1.0f;
        private bool _lastMaintainPitch = true;
        private readonly Stopwatch _playStopwatch = new();

        private int _volume = (int)SettingsService.Settings.PreviewVolume;
        public int Volume
        {
            get => _volume;
            set
            {
                _volume = Math.Clamp(value, 0, 100);
                SettingsService.Settings.PreviewVolume = _volume;
            }
        }

        public event EventHandler<bool>? IsLoadingChanged;
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    IsLoadingChanged?.Invoke(this, value);
                }
            }
        }

        private AudioService() { }

        /// <summary>
        /// Plays an audio preview using FFplay.
        /// </summary>
        /// <param name="path">Path to MP3</param>
        /// <param name="startTimeMs">Start offset in milliseconds</param>
        /// <param name="durationMs">Duration to play (optional)</param>
        /// <param name="rate">Playback speed multiplier (e.g. 1.5)</param>
        /// <param name="maintainPitch">Preserves pitch at all rates. If false, pitch follows the playback rate.</param>
        public void PlayPreview(string path, int startTimeMs, int? durationMs = null, float rate = 1.0f, bool maintainPitch = true) 
        {
            PlayPreviewInternal(path, startTimeMs, durationMs, rate, maintainPitch, true);
        }

        private void PlayPreviewInternal(string path, int startTimeMs, int? durationMs = null, float rate = 1.0f, bool maintainPitch = true, bool resetState = true)
        {
            Stop();

            if (!File.Exists(path)) return;

            CancellationTokenSource previewCts;
            lock (_lock)
            {
                _previewCts = new CancellationTokenSource();
                previewCts = _previewCts;
            }

            if (resetState)
            {
                _lastPath = path;
                _lastStartTimeMs = startTimeMs;
                _lastDurationMs = durationMs;
                _lastRate = rate;
                _lastMaintainPitch = maintainPitch;
            }

            IsLoading = true;

            Task.Run(async () =>
            {
                var token = previewCts.Token;
                try
                {
                    token.ThrowIfCancellationRequested();

                    // Locate FFplay (downloads if missing)
                    if (FFmpegHelper.IsDownloading)
                    {
                        Debug.WriteLine("FFmpeg is currently downloading. Sound unavailable.");
                        return;
                    }
                    
                    string ffplayPath;
                    try
                    {
                        ffplayPath = await FFmpegHelper.GetFFplayPathAsync(null).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to locate ffplay: {ex.Message}");
                        return;
                    }

                    token.ThrowIfCancellationRequested();

                    // Keep preview processing identical to the export path.
                    // Pitch remains unchanged at 0.25x unless the pitch toggle
                    // is enabled explicitly.
                    // FFplay volume: 0-100 -> 0.0-1.0
                    var volumeNorm = (_volume / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
                    // Keep the requested source range inside the filter graph. Putting
                    // -ss after -i would seek the already-processed output and can
                    // discard most (or all) of a slowed-down preview.
                    var startSec = Math.Max(0, startTimeMs) / 1000.0;
                    var trimFilter = durationMs.HasValue
                        ? $"atrim=start={startSec.ToString("0.###", CultureInfo.InvariantCulture)}:duration={(durationMs.Value / 1000.0).ToString("0.###", CultureInfo.InvariantCulture)},asetpts=PTS-STARTPTS"
                        : $"atrim=start={startSec.ToString("0.###", CultureInfo.InvariantCulture)},asetpts=PTS-STARTPTS";
                    var audioFilter = $"{trimFilter},{BuildRateFilter(rate, maintainPitch)},volume={volumeNorm}";

                    var args = $"-nodisp -autoexit -loglevel quiet -vn -i \"{path}\" -af \"{audioFilter}\"";

                    var psi = new ProcessStartInfo
                    {
                        FileName = ffplayPath,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    lock (_lock)
                    {
                        if (token.IsCancellationRequested)
                            return;

                        _ffplayProcess = new Process { StartInfo = psi };
                        _ffplayProcess.Start();
                        _playStopwatch.Restart();
                    }

                    // Handle auto-stop after duration
                    if (durationMs.HasValue)
                    {
                        try
                        {
                            var playbackDurationMs = (int)Math.Clamp(durationMs.Value / Math.Max(rate, 0.01f), 1, int.MaxValue);
                            await Task.Delay(playbackDurationMs, token).ConfigureAwait(false);
                            Stop();
                        }
                        catch (TaskCanceledException) { }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"FFplay playback error: {ex.Message}");
                }
                finally
                {
                    var isCurrentPreview = false;
                    lock (_lock)
                    {
                        if (ReferenceEquals(_previewCts, previewCts))
                        {
                            _previewCts = null;
                            previewCts.Dispose();
                            isCurrentPreview = true;
                        }
                    }

                    if (isCurrentPreview)
                        IsLoading = false;
                }
            });
        }

        /// <summary>
        /// Updates playback state live by restarting FFplay at the current calculated position.
        /// </summary>
        public void UpdatePlaybackState(float rate, bool maintainPitch)
        {
            if (_lastPath == null || (_lastRate == rate && _lastMaintainPitch == maintainPitch)) return;

            // Debounce to avoid process spam while sliding
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(150, token).ConfigureAwait(false);
                    if (token.IsCancellationRequested) return;

                    int currentPos;
                    lock (_lock)
                    {
                        // Calculate current position in the audio
                        // Audio Time = Initial Offset + (Wall Time * Playback Rate)
                        var elapsedMs = (int)_playStopwatch.ElapsedMilliseconds;
                        currentPos = _lastStartTimeMs + (int)(elapsedMs * _lastRate);
                        
                        _lastRate = rate;
                        _lastMaintainPitch = maintainPitch;
                        _lastStartTimeMs = currentPos;
                        _playStopwatch.Reset(); // Wait for actual process start to restart
                    }

                    PlayPreviewInternal(_lastPath, currentPos, _lastDurationMs, rate, maintainPitch, false);
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Live update error: {ex.Message}");
                }
            });
        }

        public void Stop()
        {
            CancellationTokenSource? canceledPreview;
            lock (_lock)
            {
                _debounceCts?.Cancel();
                _debounceCts = null;
                canceledPreview = _previewCts;
                _previewCts?.Cancel();
                _previewCts = null;

                _playStopwatch.Stop();
                _playStopwatch.Reset();

                if (_ffplayProcess != null && !_ffplayProcess.HasExited)
                {
                    try
                    {
                        _ffplayProcess.Kill();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"FFplay kill failed: {ex.Message}");
                    }
                }
                _ffplayProcess?.Dispose();
                _ffplayProcess = null;
            }

            canceledPreview?.Dispose();
            IsLoading = false;
        }


        private static string BuildRateFilter(float rate, bool maintainPitch)
        {
            if (!maintainPitch)
                return BuildNaturalRateFilter(rate);

            var filters = new System.Text.StringBuilder();
            var currentRate = rate;

            while (currentRate > 2.0f)
            {
                if (filters.Length > 0) filters.Append(',');
                filters.Append("atempo=2.0");
                currentRate /= 2.0f;
            }

            while (currentRate < 0.5f)
            {
                if (filters.Length > 0) filters.Append(',');
                filters.Append("atempo=0.5");
                currentRate /= 0.5f;
            }

            if (filters.Length > 0) filters.Append(',');
            filters.Append($"atempo={currentRate.ToString("0.000", CultureInfo.InvariantCulture)}");
            return filters.ToString();
        }

        private static string BuildNaturalRateFilter(float rate)
        {
            var sampleRate = Math.Max(1, (int)Math.Round(44100f * rate));
            return $"asetrate={sampleRate},aresample=44100:resampler=soxr:precision=28";
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
