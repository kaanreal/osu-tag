using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Osutag.ViewModels;

namespace Osutag.Views
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
                    float rate = 1.0f;
                    if (!string.IsNullOrEmpty(item.Difficulty.Rate))
                    {
                        var rateStr = item.Difficulty.Rate.Replace("x", "", StringComparison.OrdinalIgnoreCase).Trim();
                        if (float.TryParse(rateStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                            rate = parsed;
                    }

                    int previewTime = item.Difficulty.PreviewTime;
                    if (previewTime <= 0) previewTime = 0;

                    Services.AudioService.Instance.PlayPreview(item.Difficulty.Mp3Path, previewTime, null, rate);
                }
            }
        }

        private void RateItem_PointerExited(object? sender, PointerEventArgs e)
        {
            Services.AudioService.Instance.Stop();
        }
    }
}
