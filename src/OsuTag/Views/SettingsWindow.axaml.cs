using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OsuTag.Services;

namespace OsuTag.Views
{
    public partial class SettingsWindow : Window
    {
        public static readonly StyledProperty<bool> ProcessCoversProperty = AvaloniaProperty.Register<SettingsWindow, bool>(nameof(ProcessCovers));
        public bool ProcessCovers
        {
            get => GetValue(ProcessCoversProperty);
            set => SetValue(ProcessCoversProperty, value);
        }

        public static readonly StyledProperty<bool> CreateBackupsProperty = AvaloniaProperty.Register<SettingsWindow, bool>(nameof(CreateBackups));
        public bool CreateBackups
        {
            get => GetValue(CreateBackupsProperty);
            set => SetValue(CreateBackupsProperty, value);
        }

        public static readonly StyledProperty<bool> RememberSongsPathProperty = AvaloniaProperty.Register<SettingsWindow, bool>(nameof(RememberSongsPath));
        public bool RememberSongsPath
        {
            get => GetValue(RememberSongsPathProperty);
            set => SetValue(RememberSongsPathProperty, value);
        }

        public static readonly StyledProperty<bool> SmartScanProperty = AvaloniaProperty.Register<SettingsWindow, bool>(nameof(SmartScan));
        public bool SmartScan
        {
            get => GetValue(SmartScanProperty);
            set => SetValue(SmartScanProperty, value);
        }

        public static readonly StyledProperty<bool> SortByMostPlayedProperty = AvaloniaProperty.Register<SettingsWindow, bool>(nameof(SortByMostPlayed));
        public bool SortByMostPlayed
        {
            get => GetValue(SortByMostPlayedProperty);
            set => SetValue(SortByMostPlayedProperty, value);
        }

        public static readonly StyledProperty<bool> TelemetryEnabledProperty = AvaloniaProperty.Register<SettingsWindow, bool>(nameof(TelemetryEnabled));
        public bool TelemetryEnabled
        {
            get => GetValue(TelemetryEnabledProperty);
            set => SetValue(TelemetryEnabledProperty, value);
        }

        public static readonly StyledProperty<bool> DiscordRpcEnabledProperty = AvaloniaProperty.Register<SettingsWindow, bool>(nameof(DiscordRpcEnabled));
        public bool DiscordRpcEnabled
        {
            get => GetValue(DiscordRpcEnabledProperty);
            set => SetValue(DiscordRpcEnabledProperty, value);
        }

        public static readonly StyledProperty<bool> CheckForUpdatesProperty = AvaloniaProperty.Register<SettingsWindow, bool>(nameof(CheckForUpdates));
        public bool CheckForUpdates
        {
            get => GetValue(CheckForUpdatesProperty);
            set => SetValue(CheckForUpdatesProperty, value);
        }

        public static readonly StyledProperty<double> PreviewVolumeProperty = AvaloniaProperty.Register<SettingsWindow, double>(nameof(PreviewVolume));
        public double PreviewVolume
        {
            get => GetValue(PreviewVolumeProperty);
            set => SetValue(PreviewVolumeProperty, value);
        }

        public static readonly StyledProperty<string> CompanellaStatusProperty = AvaloniaProperty.Register<SettingsWindow, string>(nameof(CompanellaStatus), "Scanning...");
        public string CompanellaStatus
        {
            get => GetValue(CompanellaStatusProperty);
            set => SetValue(CompanellaStatusProperty, value);
        }

        public bool IsCompanellaSupported => PlatformService.IsWindows;

        public static readonly StyledProperty<string> SelectedThemeProperty = AvaloniaProperty.Register<SettingsWindow, string>(nameof(SelectedTheme), "#5B9FED");
        public string SelectedTheme
        {
            get => GetValue(SelectedThemeProperty);
            set => SetValue(SelectedThemeProperty, value);
        }

        static SettingsWindow()
        {
            PreviewVolumeProperty.Changed.AddClassHandler<SettingsWindow>((x, e) => x.OnPreviewVolumeChanged(e));
        }

        private void OnPreviewVolumeChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is double volume)
            {
                AudioService.Instance.Volume = (int)volume;
                SettingsService.Settings.PreviewVolume = volume;
                SettingsService.Save();
            }
        }

        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = this;
            LoadSettings();
            
            if (IsCompanellaSupported)
            {
                AutoDiscoverCompanella();
            }
            
            // Set initial theme selection
            SetThemeComboBoxSelection(SettingsService.Settings.ThemeColor);
        }

        private void SetThemeComboBoxSelection(string hexColor)
        {
            for (int i = 0; i < ThemeComboBox.Items.Count; i++)
            {
                if (ThemeComboBox.Items[i] is ComboBoxItem item && item.Tag?.ToString() == hexColor)
                {
                    ThemeComboBox.SelectedIndex = i;
                    break;
                }
            }
        }

        private void ThemeComboBox_SelectionChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
        {
            if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string hexColor)
            {
                SelectedTheme = hexColor;
            }
        }

        private void AutoDiscoverCompanella()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string companellaPath = Path.Combine(localAppData, "Companella");
                
                if (Directory.Exists(companellaPath))
                {
                    CompanellaStatus = $"Found Companella data under settings.";
                }
                else
                {
                    CompanellaStatus = "Companella not found.";
                }
            }
            catch
            {
                CompanellaStatus = "Error scanning for Companella.";
            }
        }

        public void ClearCache_Click(object? sender, RoutedEventArgs e)
        {
            // Logic to clear cache... we can call MainViewModel or do it directly
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var cacheFile = Path.Combine(appData, "osu!tag", "mapcache.json");
                if (File.Exists(cacheFile)) File.Delete(cacheFile);
                
                SettingsService.Settings.ScannedFolders = "";
                SettingsService.Save();
            }
            catch { }
        }

        private void LoadSettings()
        {
            ProcessCovers = SettingsService.Settings.ProcessCovers;
            CreateBackups = SettingsService.Settings.CreateBackups;
            RememberSongsPath = SettingsService.Settings.RememberSongsPath;
            SmartScan = SettingsService.Settings.SmartScan;
            SortByMostPlayed = SettingsService.Settings.SortByMostPlayed;
            TelemetryEnabled = SettingsService.Settings.TelemetryEnabled;
            DiscordRpcEnabled = SettingsService.Settings.DiscordRpcEnabled;
            CheckForUpdates = SettingsService.Settings.CheckForUpdates;
            PreviewVolume = SettingsService.Settings.PreviewVolume;
            SelectedTheme = SettingsService.Settings.ThemeColor;
        }

        private void SaveSettings()
        {
            SettingsService.Settings.ProcessCovers = ProcessCovers;
            SettingsService.Settings.CreateBackups = CreateBackups;
            SettingsService.Settings.RememberSongsPath = RememberSongsPath;
            SettingsService.Settings.SmartScan = SmartScan;
            SettingsService.Settings.SortByMostPlayed = SortByMostPlayed;
            SettingsService.Settings.TelemetryEnabled = TelemetryEnabled;
            SettingsService.Settings.DiscordRpcEnabled = DiscordRpcEnabled;
            SettingsService.Settings.CheckForUpdates = CheckForUpdates;
            SettingsService.Settings.PreviewVolume = PreviewVolume;
            SettingsService.Settings.ThemeColor = SelectedTheme;
            SettingsService.Save();
            
            // Handle Discord RPC changes
            DiscordRpcService.HandleSettingsChanged();
        }

        private void Save_Click(object? sender, RoutedEventArgs e)
        {
            SaveSettings();
            App.ApplyTheme(SelectedTheme);
            Close(true);
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }

        private async void CheckForUpdates_Click(object? sender, RoutedEventArgs e)
        {
            var updateInfo = await UpdateService.CheckForUpdatesAsync();
            if (updateInfo != null && updateInfo.IsNewer)
            {
                // Show update available
                UpdateService.OpenDownloadPage(updateInfo.DownloadUrl);
            }
        }
    }
}
