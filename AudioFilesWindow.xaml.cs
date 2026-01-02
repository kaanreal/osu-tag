using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OsuTag.ViewModels;

namespace OsuTag
{
    public partial class AudioFilesWindow : Window
    {
        private System.Windows.Media.MediaPlayer? _mediaPlayer;

        public AudioFilesWindow(MapItemGroup mapGroup)
        {
            InitializeComponent();
            DataContext = mapGroup;
            
            _mediaPlayer = new System.Windows.Media.MediaPlayer();
            _mediaPlayer.Volume = 0.3;
            
            Closing += (s, e) => _mediaPlayer?.Stop();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
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
            _mediaPlayer?.Stop();
        }

        private void AudioFile_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.DataContext is AudioFileItem audioFile)
            {
                audioFile.IsSelected = !audioFile.IsSelected;
                e.Handled = true;
            }
        }
    }
}
