using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using OsuTag.ViewModels;

namespace OsuTag.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel? _viewModel;
        private CancellationTokenSource? _hoverDelayCancellation;
        private readonly object _hoverLock = new object();

        public MainWindow()
        {
            InitializeComponent();
            DataContext = _viewModel = new MainViewModel();
        }

        public void Minimize_Click(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        public void Maximize_Click(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        public void Close_Click(object? sender, RoutedEventArgs e)
        {
            Close();
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

        private async void MapCard_PointerEntered(object? sender, PointerEventArgs e)
        {
            Console.WriteLine("[MainWindow] MapCard_PointerEntered");

            if (sender is Control control && control.DataContext is MapItemGroup group)
            {
                Console.WriteLine($"[MainWindow] Group: {group.Artist} - {group.Title}");

                // Cancel any pending hover action
                lock (_hoverLock)
                {
                    _hoverDelayCancellation?.Cancel();
                    _hoverDelayCancellation = new CancellationTokenSource();
                }

                var currentToken = _hoverDelayCancellation;

                try
                {
                    // Small delay to prevent accidental triggers (200ms)
                    Console.WriteLine("[MainWindow] Waiting 200ms before playing...");
                    await Task.Delay(200, currentToken.Token);

                    // Check if hover was cancelled
                    if (currentToken.Token.IsCancellationRequested)
                    {
                        Console.WriteLine("[MainWindow] Hover was cancelled");
                        return;
                    }

                    if (!string.IsNullOrEmpty(group.PreviewMp3Path))
                    {
                        Console.WriteLine($"[MainWindow] Calling PlayPreview: {group.PreviewMp3Path}, time: {group.PreviewTime}");
                        Services.AudioService.Instance.PlayPreview(group.PreviewMp3Path, group.PreviewTime);
                    }
                    else
                    {
                        Console.WriteLine("[MainWindow] No preview path available");
                    }
                }
                catch (TaskCanceledException)
                {
                    Console.WriteLine("[MainWindow] Hover was cancelled (TaskCanceledException)");
                }
            }
            else
            {
                Console.WriteLine("[MainWindow] Sender is not a Control or DataContext is not MapItemGroup");
            }
        }

        private void MapCard_PointerExited(object? sender, PointerEventArgs e)
        {
            Console.WriteLine("[MainWindow] MapCard_PointerExited");

            // Cancel any pending hover action
            lock (_hoverLock)
            {
                _hoverDelayCancellation?.Cancel();
            }

            Services.AudioService.Instance.Stop();
        }

        private async void DifficultyCard_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is Control control && control.DataContext is DifficultyItem diff)
            {
                // Cancel any pending hover action
                lock (_hoverLock)
                {
                    _hoverDelayCancellation?.Cancel();
                    _hoverDelayCancellation = new CancellationTokenSource();
                }

                var currentToken = _hoverDelayCancellation;

                try
                {
                    // Small delay to prevent accidental triggers (200ms)
                    await Task.Delay(200, currentToken.Token);

                    // Check if hover was cancelled
                    if (currentToken.Token.IsCancellationRequested)
                        return;

                    if (!string.IsNullOrEmpty(diff.Difficulty.Mp3Path))
                    {
                        Services.AudioService.Instance.PlayPreview(diff.Difficulty.Mp3Path, diff.Difficulty.PreviewTime);
                    }
                }
                catch (TaskCanceledException)
                {
                    // Hover was cancelled, this is expected
                }
            }
        }

        private void DifficultyCard_PointerExited(object? sender, PointerEventArgs e)
        {
            // Cancel any pending hover action
            lock (_hoverLock)
            {
                _hoverDelayCancellation?.Cancel();
            }

            Services.AudioService.Instance.Stop();
        }
    }
}
