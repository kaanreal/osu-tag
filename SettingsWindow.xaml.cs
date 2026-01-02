using System;
using System.Windows;

namespace OsuTag
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Load current settings
            ProcessCoversCheckBox.IsChecked = Properties.Settings.Default.ProcessCovers;
            CreateBackupsCheckBox.IsChecked = Properties.Settings.Default.CreateBackups;
            FileFormatTextBox.Text = Properties.Settings.Default.FileNameFormat;
            PreviewVolumeSlider.Value = Properties.Settings.Default.PreviewVolume;
            RememberPathCheckBox.IsChecked = Properties.Settings.Default.RememberSongsPath;
            SmartScanCheckBox.IsChecked = Properties.Settings.Default.SmartScan;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // Save all settings
            Properties.Settings.Default.ProcessCovers = ProcessCoversCheckBox.IsChecked ?? false;
            Properties.Settings.Default.CreateBackups = CreateBackupsCheckBox.IsChecked ?? false;
            Properties.Settings.Default.FileNameFormat = FileFormatTextBox.Text;
            Properties.Settings.Default.PreviewVolume = PreviewVolumeSlider.Value;
            Properties.Settings.Default.RememberSongsPath = RememberPathCheckBox.IsChecked ?? false;
            Properties.Settings.Default.SmartScan = SmartScanCheckBox.IsChecked ?? true;
            Properties.Settings.Default.Save();
            
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
            MessageBox.Show("Scan cache cleared. Next scan will reload all beatmaps.", "Cache Cleared", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
