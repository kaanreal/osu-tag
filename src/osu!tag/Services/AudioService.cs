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
        private CancellationTokenSource? _durationCts;
        private CancellationTokenSource? _debounceCts;
        private readonly object _lock = new();

        // State for live updates
        private string? _lastPath;
        private int _lastStartTimeMs;
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
        /// <param name="maintainPitch">If true, pitch is preserved (Double Time). If false, pitch changes with speed (Nightcore).</param>
        public void PlayPreview(string path, int startTimeMs, int? durationMs = null, float rate = 1.0f, bool maintainPitch = true) 
        {
            PlayPreviewInternal(path, startTimeMs, durationMs, rate, maintainPitch, true);
        }

        private void PlayPreviewInternal(string path, int startTimeMs, int? durationMs = null, float rate = 1.0f, bool maintainPitch = true, bool resetState = true)
        {
            Stop();

            if (!File.Exists(path)) return;

            if (resetState)
            {
                _lastPath = path;
                _lastStartTimeMs = startTimeMs;
                _lastRate = rate;
                _lastMaintainPitch = maintainPitch;
            }

            IsLoading = true;

            Task.Run(async () =>
            {
                try
                {
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

                    // Build audio filter
                    string audioFilter;
                    if (maintainPitch)
                    {
                        // Tempo change without pitch (atempo)
                        audioFilter = BuildAtempoFilter(rate);
                    }
                    else
                    {
                        // Speed change with pitch (Nightcore)
                        var newRate = (int)(44100 * rate);
                        audioFilter = $"asetrate={newRate},aresample=44100";
                    }

                    // FFplay volume: 0-100 -> 0.0-1.0
                    var volumeNorm = (_volume / 100.0).ToString("0.00", CultureInfo.InvariantCulture);
                    audioFilter += $",volume={volumeNorm}";

                    // Build arguments
                    var startSec = startTimeMs / 1000.0;
                    var args = $"-nodisp -autoexit -loglevel quiet -ss {startSec.ToString("0.000", CultureInfo.InvariantCulture)} -i \"{path}\" -af \"{audioFilter}\"";

                    if (durationMs.HasValue)
                    {
                        var durationSec = durationMs.Value / 1000.0;
                        args = $"-nodisp -autoexit -loglevel quiet -ss {startSec.ToString("0.000", CultureInfo.InvariantCulture)} -t {durationSec.ToString("0.000", CultureInfo.InvariantCulture)} -i \"{path}\" -af \"{audioFilter}\"";
                    }

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
                        _ffplayProcess = new Process { StartInfo = psi };
                        _ffplayProcess.Start();
                        _playStopwatch.Restart();
                    }

                    // Handle auto-stop after duration
                    if (durationMs.HasValue)
                    {
                        _durationCts = new CancellationTokenSource();
                        try
                        {
                            await Task.Delay(durationMs.Value, _durationCts.Token).ConfigureAwait(false);
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

                    PlayPreviewInternal(_lastPath, currentPos, null, rate, maintainPitch, false);
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
            lock (_lock)
            {
                _durationCts?.Cancel();
                _durationCts = null;
                _debounceCts?.Cancel();
                _debounceCts = null;

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
        }


        private static string BuildAtempoFilter(float rate)
        {
            if (rate >= 0.5f && rate <= 2.0f)
                return $"atempo={rate.ToString("0.000", CultureInfo.InvariantCulture)}";

            var filters = new System.Text.StringBuilder();
            var currentRate = rate;

            while (currentRate > 2.0f || currentRate < 0.5f)
            {
                if (currentRate > 2.0f)
                {
                    if (filters.Length > 0) filters.Append(',');
                    filters.Append("atempo=2.0");
                    currentRate /= 2.0f;
                }
                else if (currentRate < 0.5f)
                {
                    if (filters.Length > 0) filters.Append(',');
                    filters.Append("atempo=0.5");
                    currentRate /= 0.5f;
                }
            }

            if (filters.Length > 0) filters.Append(',');
            filters.Append($"atempo={currentRate.ToString("0.000", CultureInfo.InvariantCulture)}");

            return filters.ToString();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
