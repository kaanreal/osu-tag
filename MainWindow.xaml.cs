using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Linq;
using System.ComponentModel;
using OsuTag.ViewModels;
using OsuTag.Services;

namespace OsuTag
{
    public partial class MainWindow : Window
    {
        private MediaPlayer? _mediaPlayer;
        private int _previousSelectedCount = 0;
        
        public MainWindow()
        {
            InitializeComponent();
            var viewModel = new MainViewModel();
            DataContext = viewModel;
            
            // Subscribe to property changes for animation
            viewModel.PropertyChanged += ViewModel_PropertyChanged;
            
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.Volume = Properties.Settings.Default.PreviewVolume / 100.0;
            
            // Initialize Discord RPC
            DiscordRpcService.Initialize();

            Closing += (s, e) => {
                StopPreview();
                DiscordRpcService.Shutdown();
            };
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedCount))
            {
                if (DataContext is MainViewModel vm)
                {
                    // Animate when going from 0 to non-zero
                    if (_previousSelectedCount == 0 && vm.SelectedCount > 0)
                    {
                        AnimateSelectionPanelIn();
                    }
                    _previousSelectedCount = vm.SelectedCount;

                    // Update Discord RPC with selection count
                    if (vm.SelectedCount > 0)
                    {
                        DiscordRpcService.UpdateStatus("selected", vm.SelectedCount);
                    }
                    else
                    {
                        DiscordRpcService.UpdateStatus("idle", 0);
                    }
                }
            }
        }

        private void AnimateSelectionPanelIn()
        {
            if (SelectionPanel == null) return;
            
            // Create slide-up and fade-in animation
            var slideAnim = new DoubleAnimation
            {
                From = 30,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            var fadeAnim = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            // Apply to the selection panel
            if (SelectionPanel.RenderTransform is TranslateTransform transform)
            {
                transform.BeginAnimation(TranslateTransform.YProperty, slideAnim);
            }
            SelectionPanel.BeginAnimation(OpacityProperty, fadeAnim);
        }

        /// <summary>
        /// Animates a visual copy of the card shrinking and sliding to the selection panel badge.
        /// </summary>
        private void AnimateCardToSelectionPanel(Border cardBorder)
        {
            // Don't animate multi-audio cards (they expand instead)
            if (cardBorder.DataContext is MapItemGroup mapGroup && mapGroup.HasMultipleDifferentAudios)
                return;
            
            try
            {
                // Get card position relative to the window
                var cardPos = cardBorder.TransformToAncestor(this).Transform(new Point(0, 0));
                
                // Create a visual copy of the card with glow effect
                var cardVisual = new VisualBrush(cardBorder);
                var ghost = new Border
                {
                    Width = cardBorder.ActualWidth,
                    Height = cardBorder.ActualHeight,
                    Background = cardVisual,
                    CornerRadius = new CornerRadius(6),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    IsHitTestVisible = false,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(cardPos.X, cardPos.Y, 0, 0),
                    Effect = new DropShadowEffect
                    {
                        Color = Color.FromRgb(91, 159, 237),
                        BlurRadius = 20,
                        ShadowDepth = 0,
                        Opacity = 0.8
                    }
                };
                
                // Enable bitmap caching for smoother animation
                ghost.CacheMode = new BitmapCache { EnableClearType = false, SnapsToDevicePixels = true };
                
                // Set high z-index so it's above everything
                Panel.SetZIndex(ghost, 9999);
                
                // If in a grid, span all rows/columns
                if (Content is Grid mainGrid)
                {
                    Grid.SetRowSpan(ghost, 100);
                    Grid.SetColumnSpan(ghost, 100);
                    mainGrid.Children.Add(ghost);
                    
                    // Get the exact position of the selection panel
                    double targetX, targetY;
                    if (SelectionPanel != null && SelectionPanel.IsVisible)
                    {
                        var panelPos = SelectionPanel.TransformToAncestor(this).Transform(new Point(0, 0));
                        targetX = panelPos.X + 60; // Center of the badge
                        targetY = panelPos.Y + 15;
                    }
                    else
                    {
                        // Fallback to bottom left area
                        targetX = 100;
                        targetY = this.ActualHeight - 80;
                    }
                    
                    // Calculate for card center to land at target
                    targetX -= cardBorder.ActualWidth * 0.075; // Account for scale (0.15/2)
                    targetY -= cardBorder.ActualHeight * 0.075;
                    
                    // Animate margin for position with smooth curve
                    var animMargin = new ThicknessAnimation
                    {
                        From = new Thickness(cardPos.X, cardPos.Y, 0, 0),
                        To = new Thickness(targetX, targetY, 0, 0),
                        Duration = TimeSpan.FromMilliseconds(450),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                    };
                    
                    // Scale down smoothly
                    var scaleTransform = new ScaleTransform(1, 1);
                    ghost.RenderTransform = scaleTransform;
                    
                    var scaleAnim = new DoubleAnimation
                    {
                        From = 1,
                        To = 0.12,
                        Duration = TimeSpan.FromMilliseconds(450),
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
                    };
                    
                    // Slight rotation for more dynamic feel
                    var rotateTransform = new RotateTransform(0);
                    var transformGroup = new TransformGroup();
                    transformGroup.Children.Add(scaleTransform);
                    transformGroup.Children.Add(rotateTransform);
                    ghost.RenderTransform = transformGroup;
                    
                    var rotateAnim = new DoubleAnimation
                    {
                        From = 0,
                        To = -5,
                        Duration = TimeSpan.FromMilliseconds(450),
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    };
                    
                    // Fade out at the end
                    var fadeAnim = new DoubleAnimation
                    {
                        From = 1,
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(120),
                        BeginTime = TimeSpan.FromMilliseconds(380)
                    };
                    
                    // Animate the glow getting brighter then fading
                    var glowAnim = new DoubleAnimation
                    {
                        From = 0.8,
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(450),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                    };
                    
                    fadeAnim.Completed += (s, e) =>
                    {
                        mainGrid.Children.Remove(ghost);
                    };
                    
                    ghost.BeginAnimation(MarginProperty, animMargin);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                    rotateTransform.BeginAnimation(RotateTransform.AngleProperty, rotateAnim);
                    ghost.BeginAnimation(OpacityProperty, fadeAnim);
                    if (ghost.Effect is DropShadowEffect glow)
                    {
                        glow.BeginAnimation(DropShadowEffect.OpacityProperty, glowAnim);
                    }
                }
            }
            catch { /* Ignore animation errors */ }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Update version display
            if (VersionText != null)
            {
                VersionText.Text = $"osu!tag {AppVersion.Display}";
            }
            
            // Check for updates on startup if enabled
            if (Properties.Settings.Default.CheckForUpdates)
            {
                await CheckForUpdatesAsync();
            }
        }
        
        private UpdateInfo? _latestUpdateInfo;
        private bool _isDownloading = false;
        
        private async Task CheckForUpdatesAsync()
        {
            try
            {
                var updateInfo = await UpdateService.CheckForUpdatesAsync();
                if (updateInfo != null && updateInfo.IsNewer)
                {
                    // Check if user chose to skip this version
                    var skippedVersion = Properties.Settings.Default.SkipUpdateVersion;
                    if (skippedVersion == updateInfo.Version)
                    {
                        // User skipped this version, just show in footer
                        _latestUpdateInfo = updateInfo;
                        UpdateVersionDisplay(true, updateInfo.Version);
                        return;
                    }
                    
                    _latestUpdateInfo = updateInfo;
                    UpdateVersionDisplay(true, updateInfo.Version);
                    
                    // Show update modal after a short delay
                    await Task.Delay(1000);
                    ShowUpdateModal(updateInfo);
                }
            }
            catch
            {
                // Silently ignore update check failures
            }
        }
        
        private void UpdateVersionDisplay(bool updateAvailable, string newVersion = "")
        {
            if (VersionText != null)
            {
                VersionText.Text = $"osu!tag {AppVersion.Display}";
            }
            if (UpdateAvailableText != null)
            {
                UpdateAvailableText.Visibility = updateAvailable ? Visibility.Visible : Visibility.Collapsed;
                if (updateAvailable)
                {
                    UpdateAvailableText.Text = $"Update available: {newVersion}";
                }
            }
        }
        
        private void ShowUpdateModal(UpdateInfo updateInfo)
        {
            if (UpdateOverlay == null) return;
            
            // Populate modal content
            UpdateModalCurrentVersion.Text = AppVersion.Display;
            UpdateModalNewVersion.Text = updateInfo.Version;
            
            // Format release notes
            var notes = updateInfo.ReleaseNotes;
            if (string.IsNullOrEmpty(notes))
            {
                notes = "• Bug fixes and improvements";
            }
            else
            {
                if (notes.Length > 300)
                    notes = notes.Substring(0, 300) + "...";
            }
            UpdateModalNotes.Text = notes;
            
            // Reset UI state
            DownloadProgressPanel.Visibility = Visibility.Collapsed;
            UpdateButtonsPanel.Visibility = Visibility.Visible;
            UpdateNowButton.IsEnabled = true;
            UpdateNowButton.Content = "Download & Install";
            _isDownloading = false;
            
            // Show overlay with animation
            UpdateOverlay.Visibility = Visibility.Visible;
            AnimateUpdateModalIn();
        }
        
        private void AnimateUpdateModalIn()
        {
            var storyboard = new Storyboard();
            
            // Fade in the overlay
            UpdateOverlay.Opacity = 0;
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeIn, UpdateOverlay);
            Storyboard.SetTargetProperty(fadeIn, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(fadeIn);
            
            storyboard.Begin();
        }
        
        public void ShowUpdateModalFromSettings(UpdateInfo updateInfo)
        {
            _latestUpdateInfo = updateInfo;
            ShowUpdateModal(updateInfo);
        }
        
        private void HideUpdateModal()
        {
            if (UpdateOverlay == null || _isDownloading) return;
            UpdateOverlay.Visibility = Visibility.Collapsed;
        }
        
        private void UpdateOverlay_Click(object sender, MouseButtonEventArgs e)
        {
            // Close when clicking outside the modal (but not during download)
            if (!_isDownloading)
            {
                HideUpdateModal();
            }
        }
        
        private async void UpdateNow_Click(object sender, RoutedEventArgs e)
        {
            if (_latestUpdateInfo == null || _isDownloading) return;
            
            _isDownloading = true;
            UpdateNowButton.IsEnabled = false;
            UpdateNowButton.Content = "Downloading...";
            
            // Show progress panel
            DownloadProgressPanel.Visibility = Visibility.Visible;
            DownloadProgressBar.Value = 0;
            DownloadProgressText.Text = "0%";
            DownloadStatusText.Text = "Downloading update...";
            
            // Create progress reporter
            var progress = new Progress<(long downloaded, long total)>(p =>
            {
                var percent = p.total > 0 ? (int)((double)p.downloaded / p.total * 100) : 0;
                DownloadProgressBar.Value = percent;
                DownloadProgressText.Text = $"{percent}%";
                
                var downloadedMB = p.downloaded / 1024.0 / 1024.0;
                var totalMB = p.total / 1024.0 / 1024.0;
                DownloadStatusText.Text = $"Downloading... {downloadedMB:F1} MB / {totalMB:F1} MB";
            });
            
            // Download the update
            var downloadPath = await UpdateService.DownloadUpdateAsync(_latestUpdateInfo, progress);
            
            if (downloadPath != null)
            {
                DownloadStatusText.Text = "Installing update...";
                DownloadProgressText.Text = "100%";
                
                await Task.Delay(500);
                
                // Apply the update (this will close the app)
                if (UpdateService.ApplyUpdate(downloadPath))
                {
                    DownloadStatusText.Text = "Restarting...";
                    await Task.Delay(300);
                    Application.Current.Shutdown();
                }
                else
                {
                    // Fallback: open download page
                    DownloadStatusText.Text = "Auto-update failed. Opening download page...";
                    await Task.Delay(1000);
                    UpdateService.OpenDownloadPage(_latestUpdateInfo.DownloadUrl);
                    _isDownloading = false;
                    HideUpdateModal();
                }
            }
            else
            {
                DownloadStatusText.Text = "Download failed. Please try again.";
                _isDownloading = false;
                UpdateNowButton.IsEnabled = true;
                UpdateNowButton.Content = "Retry";
                
                await Task.Delay(2000);
                DownloadProgressPanel.Visibility = Visibility.Collapsed;
            }
        }
        
        private void UpdateLater_Click(object sender, RoutedEventArgs e)
        {
            HideUpdateModal();
        }
        
        private void SkipVersion_Click(object sender, RoutedEventArgs e)
        {
            if (_latestUpdateInfo != null)
            {
                // Save the skipped version
                Properties.Settings.Default.SkipUpdateVersion = _latestUpdateInfo.Version;
                Properties.Settings.Default.Save();
            }
            HideUpdateModal();
        }
        
        private void UpdateAvailableText_Click(object sender, MouseButtonEventArgs e)
        {
            if (_latestUpdateInfo != null)
            {
                ShowUpdateModal(_latestUpdateInfo);
            }
        }

        private void MapItem_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border && border.DataContext is MapItemGroup mapGroup)
            {
                // Float upward animation
                try
                {
                    var translateAnim = new DoubleAnimation
                    {
                        To = -6,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    
                    if (border.RenderTransform is TransformGroup transformGroup)
                    {
                        var translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
                        if (translateTransform != null)
                        {
                            translateTransform.BeginAnimation(TranslateTransform.YProperty, translateAnim);
                        }
                    }
                    
                    // Increase shadow
                    if (border.Effect is DropShadowEffect shadow)
                    {
                        var shadowAnim = new DoubleAnimation
                        {
                            To = 0.28,
                            Duration = TimeSpan.FromMilliseconds(200)
                        };
                        shadow.BeginAnimation(DropShadowEffect.OpacityProperty, shadowAnim);
                        
                        var depthAnim = new DoubleAnimation
                        {
                            To = 8,
                            Duration = TimeSpan.FromMilliseconds(200)
                        };
                        shadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, depthAnim);
                    }
                }
                catch { /* Ignore animation errors */ }
                
                // Only play preview if card is not expanded
                if (!mapGroup.IsExpanded && !string.IsNullOrEmpty(mapGroup.PreviewMp3Path) && System.IO.File.Exists(mapGroup.PreviewMp3Path))
                {
                    try
                    {
                        _mediaPlayer?.Open(new Uri(mapGroup.PreviewMp3Path));
                        
                        // Seek to preview time once media is loaded
                        if (mapGroup.PreviewTime > 0 && _mediaPlayer != null)
                        {
                            _mediaPlayer.MediaOpened += (s, args) =>
                            {
                                _mediaPlayer.Position = TimeSpan.FromMilliseconds(mapGroup.PreviewTime);
                            };
                        }
                        
                        _mediaPlayer?.Play();
                    }
                    catch { }
                }
            }
        }

        private void MapItem_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Border border)
            {
                // Return to normal position and reset tilt
                try
                {
                    var translateAnim = new DoubleAnimation
                    {
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(200),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    
                    if (border.RenderTransform is TransformGroup transformGroup)
                    {
                        var translateTransform = transformGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
                        if (translateTransform != null)
                        {
                            translateTransform.BeginAnimation(TranslateTransform.YProperty, translateAnim);
                        }
                        
                        // Reset rotations
                        var rotateTransforms = transformGroup.Children.OfType<RotateTransform>().ToList();
                        if (rotateTransforms.Count >= 2)
                        {
                            var resetAnim = new DoubleAnimation
                            {
                                To = 0,
                                Duration = TimeSpan.FromMilliseconds(200),
                                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                            };
                            rotateTransforms[0].BeginAnimation(RotateTransform.AngleProperty, resetAnim);
                            rotateTransforms[1].BeginAnimation(RotateTransform.AngleProperty, resetAnim);
                        }
                    }
                    
                    // Reset shadow
                    if (border.Effect is DropShadowEffect shadow)
                    {
                        var shadowAnim = new DoubleAnimation
                        {
                            To = 0.15,
                            Duration = TimeSpan.FromMilliseconds(200)
                        };
                        shadow.BeginAnimation(DropShadowEffect.OpacityProperty, shadowAnim);
                        
                        var depthAnim = new DoubleAnimation
                        {
                            To = 3,
                            Duration = TimeSpan.FromMilliseconds(200)
                        };
                        shadow.BeginAnimation(DropShadowEffect.ShadowDepthProperty, depthAnim);
                    }
                }
                catch { /* Ignore animation errors */ }
            }
            
            StopPreview();
        }

        private void MapItem_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is Border border && border.DataContext is MapItemGroup mapGroup)
            {
                // Steam trading card 3D tilt effect
                if (mapGroup.IsExpanded) return; // No tilt when expanded
                
                try
                {
                    var position = e.GetPosition(border);
                    var centerX = border.ActualWidth / 2;
                    var centerY = border.ActualHeight / 2;
                    
                    // Calculate relative position (-1 to 1)
                    var relativeX = (position.X - centerX) / centerX;
                    var relativeY = (position.Y - centerY) / centerY;
                    
                    // Calculate subtle tilt angles (max 3 degrees)
                    var maxTilt = 3.0;
                    var tiltY = relativeX * maxTilt; // Horizontal tilt
                    var tiltX = -relativeY * maxTilt; // Vertical tilt (inverted)
                    
                    if (border.RenderTransform is TransformGroup transformGroup)
                    {
                        var rotateTransforms = transformGroup.Children.OfType<RotateTransform>().ToList();
                        if (rotateTransforms.Count >= 2)
                        {
                            // Stop any ongoing animations first
                            rotateTransforms[0].BeginAnimation(RotateTransform.AngleProperty, null);
                            rotateTransforms[1].BeginAnimation(RotateTransform.AngleProperty, null);
                            
                            // Apply smooth tilt - direct set for smooth tracking
                            rotateTransforms[0].Angle = tiltX;
                            rotateTransforms[1].Angle = tiltY;
                        }
                    }
                }
                catch { /* Ignore tilt errors */ }
            }
        }

        private void MapItem_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is MapItemGroup mapGroup)
            {
                // If the card is expanded and we clicked inside the expanded area, ignore
                if (mapGroup.IsExpanded)
                {
                    // Check if click originated from inside the selection area (audio files or difficulties)
                    var clickedElement = e.OriginalSource as DependencyObject;
                    if (clickedElement != null)
                    {
                        // Walk up the tree to see if we clicked inside an ItemsControl (the selection list)
                        var parent = clickedElement;
                        while (parent != null)
                        {
                            if (parent is ItemsControl)
                            {
                                // Click was inside the audio/difficulty selection - don't handle here
                                return;
                            }
                            if (parent == border)
                            {
                                // We've reached the card border without finding ItemsControl
                                break;
                            }
                            parent = VisualTreeHelper.GetParent(parent);
                        }
                    }
                }
                
                StopPreview();
                
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.SelectedMapGroup = mapGroup;
                }
                
                if (mapGroup.HasMultipleDifferentRates || mapGroup.HasMultipleDifferentAudios)
                {
                    // Toggle expanded state
                    mapGroup.IsExpanded = !mapGroup.IsExpanded;
                }
                else
                {
                    // Toggle selection for simple maps
                    bool wasSelected = mapGroup.IsSelected;
                    mapGroup.IsSelected = !mapGroup.IsSelected;
                    
                    // Animate card flying to selection panel when selecting
                    if (!wasSelected && mapGroup.IsSelected)
                    {
                        AnimateCardToSelectionPanel(border);
                    }
                    
                    // Refresh selection panel
                    if (DataContext is MainViewModel vm)
                    {
                        vm.RefreshSelectedItems();
                    }
                }
            }
        }

        private void AudioFile_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is Border border && border.DataContext is AudioFileItem audioFile)
            {
                if (!string.IsNullOrEmpty(audioFile.Mp3Path) && System.IO.File.Exists(audioFile.Mp3Path))
                {
                    try
                    {
                        _mediaPlayer?.Open(new Uri(audioFile.Mp3Path));
                        
                        if (audioFile.PreviewTime > 0 && _mediaPlayer != null)
                        {
                            _mediaPlayer.MediaOpened += (s, args) =>
                            {
                                _mediaPlayer.Position = TimeSpan.FromMilliseconds(audioFile.PreviewTime);
                            };
                        }
                        
                        _mediaPlayer?.Play();
                    }
                    catch { }
                }
            }
        }

        private void AudioFile_MouseLeave(object sender, MouseEventArgs e)
        {
            StopPreview();
        }

        private void AudioFile_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is AudioFileItem audioFile)
            {
                var itemsControl = FindParent<ItemsControl>(border);
                
                if (itemsControl?.DataContext is MapItemGroup mapGroup)
                {
                    bool wasSelected = audioFile.IsSelected;
                    
                    if (audioFile.IsSelected)
                    {
                        // Clicking on already selected file deselects it and the card
                        audioFile.IsSelected = false;
                        mapGroup.IsSelected = false;
                    }
                    else
                    {
                        // Deselect all other audio files first
                        foreach (var file in mapGroup.UniqueAudioFiles)
                        {
                            file.IsSelected = false;
                        }
                        
                        // Select the clicked file and the card
                        audioFile.IsSelected = true;
                        mapGroup.IsSelected = true;
                        
                        // Animate the card flying to selection panel
                        var cardBorder = FindParent<Border>(itemsControl);
                        if (cardBorder != null && cardBorder.DataContext is MapItemGroup)
                        {
                            AnimateCardToSelectionPanel(cardBorder);
                        }
                    }
                    
                    // Refresh selection panel
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.RefreshSelectedItems();
                    }
                }
                
                e.Handled = true;
            }
        }

        private void AudioPreview_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is AudioFileItem audioFile)
            {
                if (!string.IsNullOrEmpty(audioFile.Mp3Path) && System.IO.File.Exists(audioFile.Mp3Path))
                {
                    try
                    {
                        _mediaPlayer?.Stop();
                        _mediaPlayer?.Open(new Uri(audioFile.Mp3Path));
                        
                        if (audioFile.PreviewTime > 0 && _mediaPlayer != null)
                        {
                            _mediaPlayer.MediaOpened += OnMediaOpened;
                            void OnMediaOpened(object? s, EventArgs args)
                            {
                                _mediaPlayer.Position = TimeSpan.FromMilliseconds(audioFile.PreviewTime);
                                _mediaPlayer.MediaOpened -= OnMediaOpened;
                            }
                        }
                        
                        _mediaPlayer?.Play();
                    }
                    catch { }
                }
                e.Handled = true;
            }
        }
        
        private void Difficulty_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is DifficultyItem difficulty)
            {
                // Find the parent MapItemGroup
                var itemsControl = FindParent<ItemsControl>(border);
                
                if (itemsControl?.DataContext is MapItemGroup mapGroup)
                {
                    // If clicking on already selected difficulty, deselect it and the card
                    if (difficulty.IsSelected)
                    {
                        difficulty.IsSelected = false;
                        mapGroup.IsSelected = false;
                    }
                    else
                    {
                        // Deselect all other difficulties
                        foreach (var diff in mapGroup.Difficulties)
                        {
                            diff.IsSelected = false;
                        }
                        
                        // Select only this one
                        difficulty.IsSelected = true;
                        
                        // Also select the card
                        mapGroup.IsSelected = true;
                    }
                    
                    // Refresh selection panel
                    if (DataContext is MainViewModel viewModel)
                    {
                        viewModel.RefreshSelectedItems();
                    }
                }
                
                e.Handled = true;
            }
        }
        
        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            
            if (parent == null)
                return null;
                
            if (parent is T typedParent)
                return typedParent;
                
            return FindParent<T>(parent);
        }

        private void StopPreview()
        {
            try
            {
                _mediaPlayer?.Stop();
            }
            catch { }
        }

        private void ToggleExpand(object sender, RoutedEventArgs e)
        {
            // This method is not used - expand/collapse is handled by binding
        }

        private async void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow()
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            
            if (settingsWindow.ShowDialog() == true)
            {
                // Update media player volume
                if (_mediaPlayer != null)
                {
                    _mediaPlayer.Volume = Properties.Settings.Default.PreviewVolume / 100.0;
                }
                
                // Reload settings from Properties.Settings
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.ProcessCovers = Properties.Settings.Default.ProcessCovers;
                    viewModel.CreateBackups = Properties.Settings.Default.CreateBackups;
                    
                    // Refresh Companella sorting if maps are already loaded
                    if (viewModel.MapsLoaded)
                    {
                        await viewModel.RefreshCompanellaSorting();
                    }
                }
            }
        }

        private void BrowseOutputPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select Output Folder",
                ShowNewFolderButton = true
            };

            if (DataContext is MainViewModel viewModel && !string.IsNullOrEmpty(viewModel.OutputPath))
            {
                dialog.SelectedPath = viewModel.OutputPath;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.OutputPath = dialog.SelectedPath;
                    Properties.Settings.Default.LastUsedPath = dialog.SelectedPath;
                    Properties.Settings.Default.Save();
                }
            }
        }
        
        private void BuyMeCoffee_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://buymeacoffee.com/kaanreal",
                    UseShellExecute = true
                });
            }
            catch { }
        }
        
        private void CardsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Disabled auto-loading - user clicks Load More button instead
        }
        
        private void SelectionPanel_HeaderClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.IsSelectionPanelExpanded = !viewModel.IsSelectionPanelExpanded;
            }
        }
        
        private void SelectionPanel_ClearClick(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.ClearSelection();
            }
        }
        
        private void SelectionPanel_RemoveItem(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is SelectedItemInfo item)
            {
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.RemoveFromSelection(item);
                }
            }
        }
    }
}
