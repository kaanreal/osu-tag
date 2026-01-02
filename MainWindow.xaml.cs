using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Linq;
using OsuTag.ViewModels;

namespace OsuTag
{
    public partial class MainWindow : Window
    {
        private MediaPlayer? _mediaPlayer;
        
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
            
            _mediaPlayer = new MediaPlayer();
            _mediaPlayer.Volume = 0.3;
            
            Closing += (s, e) => StopPreview();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
                StopPreview();
                
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.SelectedMapGroup = mapGroup;
                }
                
                if (mapGroup.HasMultipleDifferentRates || mapGroup.HasMultipleDifferentAudios)
                {
                    // Expand the card to show selection menu
                    mapGroup.IsExpanded = !mapGroup.IsExpanded;
                    // Don't toggle selection when expanding, only when collapsing and nothing is selected
                    if (!mapGroup.IsExpanded)
                    {
                        // Check if any item is selected
                        bool anySelected = mapGroup.HasMultipleDifferentAudios 
                            ? mapGroup.UniqueAudioFiles.Any(f => f.IsSelected)
                            : mapGroup.Difficulties.Any(d => d.IsSelected);
                        if (!anySelected)
                        {
                            mapGroup.IsSelected = !mapGroup.IsSelected;
                        }
                    }
                }
                else
                {
                    // Toggle selection for simple maps
                    mapGroup.IsSelected = !mapGroup.IsSelected;
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
                    }
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

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow()
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            
            if (settingsWindow.ShowDialog() == true)
            {
                // Reload settings from Properties.Settings
                if (DataContext is MainViewModel viewModel)
                {
                    viewModel.ProcessCovers = Properties.Settings.Default.ProcessCovers;
                    viewModel.CreateBackups = Properties.Settings.Default.CreateBackups;
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
        
        private bool _isLoadingMore = false;
        
        private async void CardsScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Prevent multiple simultaneous loads
            if (_isLoadingMore) return;
            
            // Load more items when scrolling near the bottom
            if (sender is ScrollViewer scrollViewer && DataContext is MainViewModel viewModel)
            {
                // Check if we're within 300 pixels of the bottom
                double distanceFromBottom = scrollViewer.ScrollableHeight - scrollViewer.VerticalOffset;
                
                if (distanceFromBottom < 300 && viewModel.CanLoadMore)
                {
                    _isLoadingMore = true;
                    
                    // Small delay to batch rapid scroll events
                    await Task.Delay(16); // ~1 frame at 60fps
                    
                    viewModel.LoadMoreItems();
                    _isLoadingMore = false;
                }
            }
        }
    }
}
