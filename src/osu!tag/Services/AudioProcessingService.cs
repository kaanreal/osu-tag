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
        /// <param name="maintainPitch">If true, changes tempo without pitch. If false, changes both (Nightcore)</param>
        public static void ProcessAudio(string inputPath, string outputPath, float rate, float pitchSemitones, bool maintainPitch)
        {
            // Get FFmpeg path (sync wrapper for async method)
            var ffmpegPath = FFmpegHelper.GetFFmpegPathAsync().GetAwaiter().GetResult();

            // Build the filter chain based on settings
            string audioFilter;
            
            if (maintainPitch)
            {
                // Tempo change without pitch change (Double Time / Half Time)
                // atempo only supports 0.5 to 2.0, so chain multiple filters for extreme values
                audioFilter = BuildAtempoFilter(rate);
            }
            else
            {
                // Speed change WITH pitch change (Nightcore effect)
                // asetrate changes sample rate (which changes pitch+speed), then aresample restores to 44100
                var newSampleRate = (int)(44100 * rate);
                audioFilter = $"asetrate={newSampleRate},aresample=44100";
            }

            // Build FFmpeg arguments
            var args = new StringBuilder();
            args.Append($"-y "); // Overwrite output
            args.Append($"-i \"{inputPath}\" ");
            args.Append($"-filter:a \"{audioFilter}\" ");
            args.Append($"-acodec libmp3lame ");
            args.Append($"-b:a 192k ");
            args.Append($"-ar 44100 "); // Ensure consistent sample rate
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
                try { process.Kill(); } catch { }
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
        /// Builds an atempo filter chain for the given rate.
        /// atempo only supports values between 0.5 and 2.0, so we chain multiple filters.
        /// </summary>
        private static string BuildAtempoFilter(float rate)
        {
            // For rates within normal range, single filter is fine
            if (rate >= 0.5f && rate <= 2.0f)
            {
                return $"atempo={rate.ToString("0.000", CultureInfo.InvariantCulture)}";
            }

            // For extreme rates, chain multiple atempo filters
            var filters = new StringBuilder();
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

            // Add the final adjustment
            if (filters.Length > 0) filters.Append(',');
            filters.Append($"atempo={currentRate.ToString("0.000", CultureInfo.InvariantCulture)}");

            return filters.ToString();
        }
    }
}

