using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OsuTag.Services;

namespace OsuTag
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
            Loaded += (s, e) => PlayOpenAnimation();
        }
        
        private void PlayOpenAnimation()
        {
            // Apply transform to root border, not window
            RootBorder.RenderTransform = new ScaleTransform(0.95, 0.95);
            RootBorder.Opacity = 0;
            
            var storyboard = new Storyboard();
            
            // Fade in
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeIn, RootBorder);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(fadeIn);
            
            // Scale in
            var scaleX = new DoubleAnimation
            {
                From = 0.95,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleX, RootBorder);
            Storyboard.SetTargetProperty(scaleX, new PropertyPath("RenderTransform.ScaleX"));
            storyboard.Children.Add(scaleX);
            
            var scaleY = new DoubleAnimation
            {
                From = 0.95,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(scaleY, RootBorder);
            Storyboard.SetTargetProperty(scaleY, new PropertyPath("RenderTransform.ScaleY"));
            storyboard.Children.Add(scaleY);
            
            storyboard.Begin();
        }

        private void LoadSettings()
        {
            // Load current settings
            ProcessCoversCheckBox.IsChecked = Properties.Settings.Default.ProcessCovers;
            CreateBackupsCheckBox.IsChecked = Properties.Settings.Default.CreateBackups;
            PreviewVolumeSlider.Value = Properties.Settings.Default.PreviewVolume;
            VolumeValueText.Text = $"{(int)Properties.Settings.Default.PreviewVolume}%";
            RememberPathCheckBox.IsChecked = Properties.Settings.Default.RememberSongsPath;
            SmartScanCheckBox.IsChecked = Properties.Settings.Default.SmartScan;
            
            // Load Companella settings
            SortByMostPlayedCheckBox.IsChecked = Properties.Settings.Default.SortByMostPlayed;
            var companellaPath = Properties.Settings.Default.CompanellaPath;
            
            // Auto-detect Companella path if not set
            if (string.IsNullOrEmpty(companellaPath))
            {
                companellaPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Companella");
            }
            
            CompanellaPathTextBox.Text = companellaPath;
            UpdateCompanellaStatus();
            
            // Load update settings
            CheckForUpdatesCheckBox.IsChecked = Properties.Settings.Default.CheckForUpdates;
            
            // Load privacy settings
            TelemetryEnabledCheckBox.IsChecked = Properties.Settings.Default.TelemetryEnabled;
            DiscordRpcEnabledCheckBox.IsChecked = Properties.Settings.Default.DiscordRpcEnabled;
            
            // Set version text
            VersionText.Text = AppVersion.Display;
        }
        
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VolumeValueText != null)
            {
                VolumeValueText.Text = $"{(int)e.NewValue}%";
            }
        }

        private void UpdateCompanellaStatus()
        {
            var path = CompanellaPathTextBox.Text;
            var sessionsDbPath = Path.Combine(path, "sessions.db");
            var mapsDbPath = Path.Combine(path, "maps.db");
            
            if (Directory.Exists(path) && File.Exists(sessionsDbPath))
            {
                var status = File.Exists(mapsDbPath) 
                    ? "✓ Companella databases found (sessions.db + maps.db)" 
                    : "✓ sessions.db found (maps.db not found, using fallback)";
                CompanellaStatusText.Text = status;
                CompanellaStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#5B9FED"));
            }
            else if (Directory.Exists(path))
            {
                CompanellaStatusText.Text = "⚠ sessions.db not found in this folder";
                CompanellaStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5A623"));
            }
            else
            {
                CompanellaStatusText.Text = "⚠ Folder does not exist";
                CompanellaStatusText.Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F5A623"));
            }
        }

        private void BrowseCompanellaPath_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select Companella! data folder",
                UseDescriptionForTitle = true,
                SelectedPath = CompanellaPathTextBox.Text
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                CompanellaPathTextBox.Text = dialog.SelectedPath;
                UpdateCompanellaStatus();
            }
        }

        private void DownloadCompanella_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/Leinadix/companella",
                UseShellExecute = true
            });
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Save all settings
            Properties.Settings.Default.ProcessCovers = ProcessCoversCheckBox.IsChecked ?? false;
            Properties.Settings.Default.CreateBackups = CreateBackupsCheckBox.IsChecked ?? false;
            Properties.Settings.Default.PreviewVolume = PreviewVolumeSlider.Value;
            Properties.Settings.Default.RememberSongsPath = RememberPathCheckBox.IsChecked ?? false;
            Properties.Settings.Default.SmartScan = SmartScanCheckBox.IsChecked ?? true;
            
            // Save Companella settings
            Properties.Settings.Default.SortByMostPlayed = SortByMostPlayedCheckBox.IsChecked ?? false;
            Properties.Settings.Default.CompanellaPath = CompanellaPathTextBox.Text;
            
            // Save update settings
            Properties.Settings.Default.CheckForUpdates = CheckForUpdatesCheckBox.IsChecked ?? true;
            
            // Save privacy settings
            Properties.Settings.Default.TelemetryEnabled = TelemetryEnabledCheckBox.IsChecked ?? true;
            Properties.Settings.Default.DiscordRpcEnabled = DiscordRpcEnabledCheckBox.IsChecked ?? true;
            
            Properties.Settings.Default.Save();
            
            // Handle settings changes for services
            DiscordRpcService.HandleSettingsChanged();
            
            // Track settings change (only if telemetry is enabled)
            _ = TelemetryService.TrackSettingsChanged(new Dictionary<string, object>
            {
                ["telemetry_enabled"] = Properties.Settings.Default.TelemetryEnabled,
                ["discord_rpc_enabled"] = Properties.Settings.Default.DiscordRpcEnabled
            });
            
            DialogResult = true;
            Close();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ScannedFolders = "";
            Properties.Settings.Default.Save();
            System.Windows.MessageBox.Show("Scan cache cleared. Next scan will reload all beatmaps.", "Cache Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
        {
            UpdateStatusText.Text = "Checking for updates...";
            UpdateStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9B9DA3"));
            
            try
            {
                var updateInfo = await UpdateService.CheckForUpdatesAsync();
                
                if (updateInfo == null)
                {
                    UpdateStatusText.Text = "⚠ Could not check for updates. Please try again later.";
                    UpdateStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5A623"));
                }
                else if (updateInfo.IsNewer)
                {
                    UpdateStatusText.Text = $"✓ New version available: {updateInfo.Version}";
                    UpdateStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4CAF50"));
                    
                    // Close settings and show update modal on main window
                    if (Owner is MainWindow mainWindow)
                    {
                        this.Close();
                        mainWindow.ShowUpdateModalFromSettings(updateInfo);
                    }
                }
                else
                {
                    UpdateStatusText.Text = $"✓ You're running the latest version ({AppVersion.Display})";
                    UpdateStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5B9FED"));
                }
            }
            catch (Exception ex)
            {
                UpdateStatusText.Text = $"⚠ Error checking for updates: {ex.Message}";
                UpdateStatusText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5A623"));
            }
        }
    }
}
