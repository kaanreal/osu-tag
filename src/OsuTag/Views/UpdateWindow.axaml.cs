using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using OsuTag.Services;
using System.Threading.Tasks;

namespace OsuTag.Views
{
    public partial class UpdateWindow : Window
    {
        private UpdateInfo? _updateInfo;
        
        public UpdateWindow()
        {
            InitializeComponent();
        }

        public UpdateWindow(UpdateInfo info) : this()
        {
            _updateInfo = info;
            
            // Populate UI
            this.DataContext = info.Changelog; // Bind markdown content directly or via viewmodel
            
            var versionText = this.FindControl<TextBlock>("VersionText");
            if (versionText != null) versionText.Text = info.Version;
            
            // MarkdownScrollViewer binds to DataContext (string) automatically via {Binding} in XAML
            // but we can also set it explicitly if needed, though Binding matching {Binding} works best with DataContext being the string.
            // However, to keep other bindings potential, maybe set DataContext as info?
            // Re-check XAML: Markdown="{Binding}" implies DataContext is the string.
            // Let's set DataContext = info.Changelog.
            this.DataContext = info.Changelog;
        }

        private void Header_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            if (_updateInfo != null)
            {
                UpdateService.Instance.IgnoreUpdate(_updateInfo.Version);
            }
            Close();
        }

        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            var buttonsGrid = this.FindControl<Grid>("ButtonsGrid");
            var progressPanel = this.FindControl<StackPanel>("ProgressPanel");
            var progressBar = this.FindControl<ProgressBar>("DownloadProgressBar");
            var progressText = this.FindControl<TextBlock>("ProgressText");
            var updateBtn = this.FindControl<Button>("UpdateBtn");

            if (buttonsGrid == null || progressPanel == null || progressBar == null || updateBtn == null) return;

            // Switch to progress view
            buttonsGrid.IsVisible = false;
            progressPanel.IsVisible = true;

            try
            {
                var progress = new Progress<double>(p =>
                {
                    progressBar.Value = p;
                    if (progressText != null) progressText.Text = $"{(int)p}%";
                });

                await UpdateService.Instance.DownloadUpdateAsync(_updateInfo.DownloadUrl, progress);

                // Download complete
                if (progressText != null) progressText.Text = "Installing...";
                await Task.Delay(500); // Visual pause

                UpdateService.Instance.ApplyUpdate();
            }
            catch (Exception ex)
            {
                // Show error state (simplified)
                if (progressText != null) 
                {
                    progressText.Text = "Error!";
                    progressText.Foreground = Avalonia.Media.Brushes.Red;
                }
                
                // Re-enable close
                await Task.Delay(2000);
                buttonsGrid.IsVisible = true;
                progressPanel.IsVisible = false;
            }
        }
    }
}
