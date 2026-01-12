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
            SettingsService.Save();
            
            // Handle Discord RPC changes
            DiscordRpcService.HandleSettingsChanged();
        }

        private void Save_Click(object? sender, RoutedEventArgs e)
        {
            SaveSettings();
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
