using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Osutag.ViewModels;

namespace Osutag.Views
{
    public partial class AudioFilesWindow : Window
    {
        public ObservableCollection<AudioFileItem> AudioFiles { get; set; } = new();
        public AudioFileItem? SelectedAudioFile { get; private set; }

        public AudioFilesWindow()
        {
            InitializeComponent();
        }

        public AudioFilesWindow(ObservableCollection<AudioFileItem> audioFiles) : this()
        {
            AudioFiles = audioFiles;
            AudioFilesList.ItemsSource = AudioFiles;
        }

        private void AudioItem_Click(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.DataContext is AudioFileItem item)
            {
                // Deselect all others
                foreach (var audioFile in AudioFiles)
                {
                    audioFile.IsSelected = false;
                }
                item.IsSelected = true;
                SelectedAudioFile = item;
            }
        }

        private void OK_Click(object? sender, RoutedEventArgs e)
        {
            Services.AudioService.Instance.Stop();
            Close(SelectedAudioFile);
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Services.AudioService.Instance.Stop();
            Close(null);
        }

        private void AudioItem_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is Control control && control.DataContext is AudioFileItem item)
            {
                if (!string.IsNullOrEmpty(item.Mp3Path))
                {
                    Services.AudioService.Instance.PlayPreview(item.Mp3Path, item.PreviewTime);
                }
            }
        }

        private void AudioItem_PointerExited(object? sender, PointerEventArgs e)
        {
            Services.AudioService.Instance.Stop();
        }
    }
}
