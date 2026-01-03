using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using OsuTag.ViewModels;

namespace OsuTag
{
    public partial class AudioFilesWindow : Window
    {
        private System.Windows.Media.MediaPlayer? _mediaPlayer;
        private MapItemGroup? _mapGroup;

        public AudioFilesWindow(MapItemGroup mapGroup)
        {
            InitializeComponent();
            DataContext = mapGroup;
            _mapGroup = mapGroup;
            
            _mediaPlayer = new System.Windows.Media.MediaPlayer();
            _mediaPlayer.Volume = Properties.Settings.Default.PreviewVolume / 100.0;
            
            Closing += (s, e) => _mediaPlayer?.Stop();
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

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _mediaPlayer?.Stop();
            Close();
        }

        private void AudioFile_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is AudioFileItem audioFile)
            {
                audioFile.IsSelected = !audioFile.IsSelected;
                e.Handled = true;
            }
        }

        private void PlayPreview_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is AudioFileItem audioFile)
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

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            if (_mapGroup != null)
            {
                foreach (var audioFile in _mapGroup.UniqueAudioFiles)
                {
                    audioFile.IsSelected = true;
                }
            }
        }

        private void DeselectAll_Click(object sender, RoutedEventArgs e)
        {
            if (_mapGroup != null)
            {
                foreach (var audioFile in _mapGroup.UniqueAudioFiles)
                {
                    audioFile.IsSelected = false;
                }
            }
        }
    }
}
