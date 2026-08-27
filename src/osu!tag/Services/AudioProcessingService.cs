using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Osutag.Services
{
    /// <summary>
    /// Audio processing service using FFmpeg for rate/pitch changes.
    /// </summary>
    public static class AudioProcessingService
    {
        /// <summary>
        /// Processes audio with rate and pitch adjustments using FFmpeg.
        /// </summary>
        /// <param name="inputPath">Path to input audio file</param>
        /// <param name="outputPath">Path for output audio file</param>
        /// <param name="rate">Playback rate multiplier (e.g., 1.5 for 150% speed)</param>
        /// <param name="pitchSemitones">Additional pitch shift in semitones (not currently used)</param>
        /// <param name="maintainPitch">Preserves pitch at all rates. If false, pitch follows the playback rate.</param>
        /// <param name="cutStartSeconds">Optional source position at which to start the output.</param>
        /// <param name="cutEndSeconds">Optional source position at which to end the output.</param>
        public static void ProcessAudio(string inputPath, string outputPath, float rate, float pitchSemitones, bool maintainPitch,
            float? cutStartSeconds = null, float? cutEndSeconds = null)
        {
            if (cutStartSeconds is < 0 || cutEndSeconds is < 0 ||
                (cutEndSeconds.HasValue && cutStartSeconds.HasValue && cutEndSeconds.Value <= cutStartSeconds.Value) ||
                (cutEndSeconds.HasValue && !cutStartSeconds.HasValue && cutEndSeconds.Value <= 0))
            {
                throw new ArgumentException("The MP3 trim end must be greater than its start.");
            }

            // Get FFmpeg path
            if (FFmpegHelper.IsDownloading)
            {
                throw new Exception("FFmpeg is currently setting up. Audio processing unavailable.");
            }
            var ffmpegPath = FFmpegHelper.GetFFmpegPathAsync(null).GetAwaiter().GetResult();

            // Build the filter chain based on settings. Pitch is preserved by
            // default, including at 0.25x. The natural playback-rate path is
            // only used when the user explicitly enables pitch shifting.
            var audioFilter = BuildRateFilter(rate, maintainPitch);

            if (cutStartSeconds.HasValue || cutEndSeconds.HasValue)
            {
                var trimStart = cutStartSeconds ?? 0;
                var trimFilter = cutEndSeconds.HasValue
                    ? $"atrim=start={trimStart.ToString("0.###", CultureInfo.InvariantCulture)}:end={cutEndSeconds.Value.ToString("0.###", CultureInfo.InvariantCulture)}"
                    : $"atrim=start={trimStart.ToString("0.###", CultureInfo.InvariantCulture)}";
                audioFilter = $"{trimFilter},asetpts=PTS-STARTPTS,{audioFilter}";
            }

            // Build FFmpeg arguments
            var args = new StringBuilder();
            args.Append($"-y "); // Overwrite output
            args.Append($"-i \"{inputPath}\" ");
            args.Append($"-vn -map_metadata 0 ");
            args.Append($"-filter:a \"{audioFilter}\" ");
            args.Append($"-acodec libmp3lame ");
            // Use the highest broadly compatible MP3 bitrate and preserve the
            // source sample rate unless a pitch filter intentionally changes it.
            args.Append($"-b:a 320k -write_xing 1 ");
            args.Append($"\"{outputPath}\"");

            var psi = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args.ToString(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = psi };
            var stderr = new StringBuilder();
            
            process.ErrorDataReceived += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    stderr.AppendLine(e.Data);
            };

            process.Start();
            process.BeginErrorReadLine();

            // Wait with timeout (60 seconds should be enough for most audio files)
            if (!process.WaitForExit(60000))
            {
                try { process.Kill(); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Process kill failed: {ex.Message}"); }
                throw new Exception("FFmpeg process timed out after 60 seconds.");
            }

            if (process.ExitCode != 0)
            {
                Debug.WriteLine($"FFmpeg error output: {stderr}");
                throw new Exception($"FFmpeg failed with exit code {process.ExitCode}. Check if the input file is valid.");
            }

            // Verify output was created
            if (!File.Exists(outputPath))
            {
                throw new Exception("FFmpeg did not create output file.");
            }
        }

        /// <summary>
        /// Builds the rate filter used by both export and preview.
        ///
        /// Normal and extreme pitch-preserving rates use chained atempo filters
        /// (atempo supports 0.5x-2x per stage). The natural playback-rate path
        /// is reserved for the explicit pitch toggle.
        /// </summary>
        private static string BuildRateFilter(float rate, bool maintainPitch)
        {
            if (!maintainPitch)
                return BuildNaturalRateFilter(rate);

            var filters = new StringBuilder();
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
    }
}

