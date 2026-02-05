using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Osutag.Services;
using Osutag.ViewModels;
using Avalonia.Platform.Storage;

namespace Osutag.Views
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

        public static readonly StyledProperty<bool> DynamicBackgroundEnabledProperty = AvaloniaProperty.Register<SettingsWindow, bool>(nameof(DynamicBackgroundEnabled));
        public bool DynamicBackgroundEnabled
        {
            get => GetValue(DynamicBackgroundEnabledProperty);
            set => SetValue(DynamicBackgroundEnabledProperty, value);
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

        public static readonly StyledProperty<string> SessionsDbStatusProperty = AvaloniaProperty.Register<SettingsWindow, string>(nameof(SessionsDbStatus), "Not Found");
        public string SessionsDbStatus
        {
            get => GetValue(SessionsDbStatusProperty);
            set => SetValue(SessionsDbStatusProperty, value);
        }

        public static readonly StyledProperty<Avalonia.Media.IBrush> SessionsDbColorProperty = AvaloniaProperty.Register<SettingsWindow, Avalonia.Media.IBrush>(nameof(SessionsDbColor), Avalonia.Media.Brushes.Red);
        public Avalonia.Media.IBrush SessionsDbColor
        {
            get => GetValue(SessionsDbColorProperty);
            set => SetValue(SessionsDbColorProperty, value);
        }

        public static readonly StyledProperty<string> MapsDbStatusProperty = AvaloniaProperty.Register<SettingsWindow, string>(nameof(MapsDbStatus), "Not Found");
        public string MapsDbStatus
        {
            get => GetValue(MapsDbStatusProperty);
            set => SetValue(MapsDbStatusProperty, value);
        }

        public static readonly StyledProperty<Avalonia.Media.IBrush> MapsDbColorProperty = AvaloniaProperty.Register<SettingsWindow, Avalonia.Media.IBrush>(nameof(MapsDbColor), Avalonia.Media.Brushes.Red);
        public Avalonia.Media.IBrush MapsDbColor
        {
            get => GetValue(MapsDbColorProperty);
            set => SetValue(MapsDbColorProperty, value);
        }

        public string AppVersion => "v" + Osutag.Services.AppVersion.Current;

        public static readonly StyledProperty<string> OsuPathProperty = AvaloniaProperty.Register<SettingsWindow, string>(nameof(OsuPath), "");
        public string OsuPath
        {
            get => GetValue(OsuPathProperty);
            set => SetValue(OsuPathProperty, value);
        }

        static SettingsWindow()
        {
            PreviewVolumeProperty.Changed.AddClassHandler<SettingsWindow>((x, e) => x.OnPreviewVolumeChanged(e));
            SelectedThemeProperty.Changed.AddClassHandler<SettingsWindow>((x, e) => x.OnSelectedThemeChanged(e));
            SortByMostPlayedProperty.Changed.AddClassHandler<SettingsWindow>((x, e) => x.OnSortByMostPlayedChanged(e));
            DynamicBackgroundEnabledProperty.Changed.AddClassHandler<SettingsWindow>((x, e) => x.OnDynamicBackgroundEnabledChanged(e));
        }

        private void OnPreviewVolumeChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is double volume)
            {
                AudioService.Instance.Volume = (int)volume;
                SettingsService.Settings.PreviewVolume = volume;
            }
        }

        private void OnSelectedThemeChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is string themeTag)
            {
                if (themeTag == "Dynamic")
                {
                    DynamicBackgroundEnabled = true;
                }
                else
                {
                    DynamicBackgroundEnabled = false;
                    App.ApplyTheme(themeTag);
                }
                SettingsService.Settings.ThemeColor = themeTag;
                SettingsService.Save();
            }
        }

        private void OnSortByMostPlayedChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool enabled)
            {
                SettingsService.Settings.SortByMostPlayed = enabled;
                SettingsService.Save();
            }
        }

        private void OnDynamicBackgroundEnabledChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool enabled)
            {
                SettingsService.Settings.DynamicBackgroundEnabled = enabled;
                SettingsService.Save();

                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow?.DataContext is MainViewModel mainVm)
                {
                    mainVm.DynamicBackgroundEnabled = enabled;
                }
            }
        }

        public SettingsWindow()
        {
            try
            {
                InitializeComponent();
                DataContext = this;
                LoadSettings();
                
                if (IsCompanellaSupported)
                {
                    AutoDiscoverCompanella();
                }
                
                // Set initial theme selection
                try
                {
                    SetThemeComboBoxSelection(SettingsService.Settings.ThemeColor);
                }
                catch (Exception)
                {
                }
            }
            catch (Exception)
            {
                throw;
            }
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
            if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                SelectedTheme = tag;
            }
        }

        private void AutoDiscoverCompanella()
        {
            string? companellaPath = SettingsService.Settings.CompanellaPath;

            if (string.IsNullOrEmpty(companellaPath))
            {
                companellaPath = PlatformService.GetDefaultCompanellaPath();
            }
            
            UpdateCompanellaStatus(companellaPath);
        }

        private void UpdateCompanellaStatus(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                {
                    SessionsDbStatus = "Path not set";
                    SessionsDbColor = Avalonia.Media.Brushes.Gray;
                    MapsDbStatus = "Path not set";
                    MapsDbColor = Avalonia.Media.Brushes.Gray;
                    CompanellaStatus = "Companella location unknown.";
                    return;
                }

                bool sessionsExists = File.Exists(Path.Combine(path, "sessions.db"));
                bool mapsExists = File.Exists(Path.Combine(path, "maps.db"));

                SessionsDbStatus = sessionsExists ? "Detected" : "Not Found";
                SessionsDbColor = sessionsExists ? Avalonia.Media.Brushes.LimeGreen : Avalonia.Media.Brushes.Red;

                MapsDbStatus = mapsExists ? "Detected" : "Not Found";
                MapsDbColor = mapsExists ? Avalonia.Media.Brushes.LimeGreen : Avalonia.Media.Brushes.Red;

                if (sessionsExists && mapsExists)
                {
                    CompanellaStatus = "Companella detected.";
                }
                else if (sessionsExists || mapsExists)
                {
                    CompanellaStatus = "Companella partially detected.";
                }
                else
                {
                    CompanellaStatus = "Companella not detected.";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateCompanellaStatus failed: {ex.Message}");
                CompanellaStatus = "Error checking Companella status.";
            }
        }

        public async void BrowseCompanella_Click(object? sender, RoutedEventArgs e)
        {
            var storage = this.StorageProvider;
            IStorageFolder? startFolder = null;
            if (!string.IsNullOrEmpty(SettingsService.Settings.CompanellaPath))
            {
                try { startFolder = await storage.TryGetFolderFromPathAsync(SettingsService.Settings.CompanellaPath); }
                catch { /* Ignore if path is invalid */ }
            }

            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Companella Installation Folder",
                SuggestedStartLocation = startFolder,
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                var result = folders[0].Path.LocalPath;
                SettingsService.Settings.CompanellaPath = result;
                UpdateCompanellaStatus(result);
            }
        }

        public async void BrowseOsuPath_Click(object? sender, RoutedEventArgs e)
        {
            var storage = this.StorageProvider;
            IStorageFolder? startFolder = null;
            if (!string.IsNullOrEmpty(SettingsService.Settings.OsuPath))
            {
                try { startFolder = await storage.TryGetFolderFromPathAsync(SettingsService.Settings.OsuPath); }
                catch { /* Ignore if path is invalid */ }
            }

            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select osu! Installation Folder",
                SuggestedStartLocation = startFolder,
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                var result = folders[0].Path.LocalPath;
                OsuPath = result;
                SettingsService.Settings.OsuPath = result;
                SettingsService.Save();
                
                // Notify MainViewModel
                if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                    desktop.MainWindow?.DataContext is MainViewModel mainVm)
                {
                    mainVm.OsuPath = result;
                }
            }
        }

        public void DownloadCompanella_Click(object? sender, PointerPressedEventArgs e)
        {
            PlatformService.OpenUrl("https://github.com/Leinadix/companella");
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ClearCache failed: {ex.Message}");
            }
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
            DynamicBackgroundEnabled = SettingsService.Settings.DynamicBackgroundEnabled;
            OsuPath = SettingsService.Settings.OsuPath;
            SpotifyClientId = SettingsService.Settings.SpotifyClientId;
            SpotifyClientSecret = SettingsService.Settings.SpotifyClientSecret;
        }

        private void SaveSettings()
        {
            try
            {
                Console.WriteLine("[SettingsWindow] SaveSettings called");
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
                SettingsService.Settings.DynamicBackgroundEnabled = (SelectedTheme == "Dynamic");
                SettingsService.Settings.OsuPath = OsuPath;
                SettingsService.Settings.SpotifyClientId = SpotifyClientId;
                SettingsService.Settings.SpotifyClientSecret = SpotifyClientSecret;
                SettingsService.Save();
                
                // Handle Discord RPC changes - Temporarily disabled for debugging
                /*
                try
                {
                    DiscordRpcService.HandleSettingsChanged();
                }
                catch (Exception)
                {
                }
                */
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void Save_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettings();
                Close();
            }
            catch (Exception)
            {
            }
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void CheckForUpdates_Click(object? sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn != null) btn.IsEnabled = false;

            try 
            {
                // Force a fresh check
                var updateInfo = await UpdateService.Instance.CheckForUpdatesAsync();
                
                if (updateInfo != null && updateInfo.IsNewer)
                {
                    // Update found - open window
                    var updateWindow = new UpdateWindow(updateInfo);
                    await updateWindow.ShowDialog(this);
                }
                else
                {
                    // No update found - show feedback
                    var msgWin = new MessageWindow("Check for Updates", "You are on the newest version.");
                    await msgWin.ShowDialog(this);
                }
            }
            catch (Exception ex)
            {
                 var msgWin = new MessageWindow("Error", $"Update check failed: {ex.Message}");
                 await msgWin.ShowDialog(this);
            }
            finally
            {
                if (btn != null) btn.IsEnabled = true;
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                BeginMoveDrag(e);
            }
        }

        private void SpotifyHelp_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            PlatformService.OpenUrl("https://developer.spotify.com/dashboard");
        }

        public static readonly StyledProperty<string> SpotifyClientIdProperty = AvaloniaProperty.Register<SettingsWindow, string>(nameof(SpotifyClientId));
        public string SpotifyClientId
        {
            get => GetValue(SpotifyClientIdProperty);
            set => SetValue(SpotifyClientIdProperty, value);
        }

        public static readonly StyledProperty<string> SpotifyClientSecretProperty = AvaloniaProperty.Register<SettingsWindow, string>(nameof(SpotifyClientSecret));
        public string SpotifyClientSecret
        {
            get => GetValue(SpotifyClientSecretProperty);
            set => SetValue(SpotifyClientSecretProperty, value);
        }
    }
}
