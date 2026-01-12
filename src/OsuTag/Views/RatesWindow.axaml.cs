using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OsuTag.ViewModels;

namespace OsuTag.Views
{
    public partial class RatesWindow : Window
    {
        public ObservableCollection<DifficultyItem> Difficulties { get; set; } = new();
        public DifficultyItem? SelectedDifficulty { get; private set; }

        public RatesWindow()
        {
            InitializeComponent();
        }

        public RatesWindow(ObservableCollection<DifficultyItem> difficulties) : this()
        {
            Difficulties = difficulties;
            RatesList.ItemsSource = Difficulties;
        }

        private void RateItem_Click(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.DataContext is DifficultyItem item)
            {
                // Deselect all others
                foreach (var diff in Difficulties)
                {
                    diff.IsSelected = false;
                }
                item.IsSelected = true;
                SelectedDifficulty = item;
            }
        }

        private void OK_Click(object? sender, RoutedEventArgs e)
        {
            Services.AudioService.Instance.Stop();
            Close(SelectedDifficulty);
        }

        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Services.AudioService.Instance.Stop();
            Close(null);
        }

        private void RateItem_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is Control control && control.DataContext is DifficultyItem item)
            {
                if (item.Difficulty != null && !string.IsNullOrEmpty(item.Difficulty.Mp3Path))
                {
                    // Use the preview time from the difficulty if available, or -1
                    // Actually, OsuMapDifficulty doesn't have PreviewTime directly, but MainViewModel sets it in MapItemGroup.
                    // We need to pass it or just use 0 if not found.
                    // In RatesWindow, we don't have the MapItemGroup context easily.
                    // Let's check how we can get it.
                    Services.AudioService.Instance.PlayPreview(item.Difficulty.Mp3Path, 0); // Default to start for rates for now
                }
            }
        }

        private void RateItem_PointerExited(object? sender, PointerEventArgs e)
        {
            Services.AudioService.Instance.Stop();
        }
    }
}
