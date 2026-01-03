using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DiscordRPC;

namespace OsuTag.Services
{
    public static class DiscordRpcService
    {
        private static DiscordRpcClient? _client;
        private static int _convertedCount = 0;
        private static string _logoKey = "osutaglogo"; // Set this to your Discord application's logo asset name

        public static void Initialize()
        {
            if (_client != null) return;

            // Check if Discord RPC is enabled
            if (!Properties.Settings.Default.DiscordRpcEnabled) return;

            _client = new DiscordRpcClient("1456772447578886387"); // Replace with your Discord application's client ID
            _client.Initialize();
            UpdateStatus("idle", 0);
        }

        public static void UpdateStatus(string status, int count)
        {
            // Check if Discord RPC is enabled
            if (!Properties.Settings.Default.DiscordRpcEnabled)
            {
                // If disabled, shut down the client if it exists
                if (_client != null)
                {
                    Shutdown();
                }
                return;
            }

            _convertedCount = count;
            if (_client == null) return;

            string details, stateText;

            switch (status.ToLower())
            {
                case "idle":
                    details = "Converting osu! beatmaps to MP3";
                    stateText = "osu!tag - Beatmap Converter";
                    break;
                case "selected":
                    details = "Selecting beatmaps to convert";
                    stateText = $"{count} beatmap{(count != 1 ? "s" : "")} selected";
                    break;
                case "converting":
                    details = "Converting beatmaps to MP3";
                    stateText = $"Processing... ({count})";
                    break;
                case "completed":
                    details = "Conversion completed!";
                    stateText = $"Finished {count} beatmap{(count != 1 ? "s" : "")}";
                    break;
                default:
                    details = "Using osu!tag";
                    stateText = status;
                    break;
            }

            _client.SetPresence(new RichPresence
            {
                Details = details,
                State = stateText,
                Assets = new Assets
                {
                    LargeImageKey = _logoKey,
                    LargeImageText = "osu!tag - Convert beatmaps to MP3 with metadata"
                }
            });
        }

        public static void IncrementConvertedCount()
        {
            UpdateStatus("Converting something", _convertedCount + 1);
        }

        public static void Shutdown()
        {
            if (_client != null)
            {
                _client.Dispose();
                _client = null;
            }
        }

        public static void HandleSettingsChanged()
        {
            if (Properties.Settings.Default.DiscordRpcEnabled)
            {
                // If enabled and not initialized, initialize
                if (_client == null)
                {
                    Initialize();
                }
            }
            else
            {
                // If disabled, shut down
                if (_client != null)
                {
                    Shutdown();
                }
            }
        }
    }
}