using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OsuTag.ViewModels;

namespace OsuTag.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel? _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel = new MainViewModel();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            
            // Initialize Discord RPC
            Services.DiscordRpcService.Initialize();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            
            // Shutdown Discord RPC
            Services.DiscordRpcService.Shutdown();

            // Stop any playing audio
            Services.AudioService.Instance.Stop();
        }

        private void MapCard_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is Control control && control.DataContext is MapItemGroup group)
            {
                if (!string.IsNullOrEmpty(group.PreviewMp3Path))
                {
                    Services.AudioService.Instance.PlayPreview(group.PreviewMp3Path, group.PreviewTime);
                }
            }
        }

        private void MapCard_PointerExited(object? sender, PointerEventArgs e)
        {
            Services.AudioService.Instance.Stop();
        }
    }
}
