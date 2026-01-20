using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Osutag.ViewModels;

namespace Osutag.Views
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
            if (sender is Control control && control.DataContext is MapItemGroup group)
            {
                lock (_hoverLock)
                {
                    _hoverDelayCancellation?.Cancel();
                    _hoverDelayCancellation = new CancellationTokenSource();
                }

                var currentToken = _hoverDelayCancellation;

                try
                {
                    await Task.Delay(100, currentToken.Token);
                    if (currentToken.Token.IsCancellationRequested) return;

                    if (!string.IsNullOrEmpty(group.PreviewMp3Path))
                    {
                        Services.AudioService.Instance.PlayPreview(group.PreviewMp3Path, group.PreviewTime);
                    }
                }
                catch (TaskCanceledException) { }
            }
        }

        private void MapCard_PointerExited(object? sender, PointerEventArgs e)
        {
            lock (_hoverLock) { _hoverDelayCancellation?.Cancel(); }
            Services.AudioService.Instance.Stop();
        }

        private async void DifficultyCard_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is Control control && control.DataContext is DifficultyItem diff)
            {
                lock (_hoverLock)
                {
                    _hoverDelayCancellation?.Cancel();
                    _hoverDelayCancellation = new CancellationTokenSource();
                }

                var currentToken = _hoverDelayCancellation;

                try
                {
                    await Task.Delay(150, currentToken.Token);
                    if (currentToken.Token.IsCancellationRequested) return;

                    if (!string.IsNullOrEmpty(diff.Difficulty.Mp3Path))
                    {
                        Services.AudioService.Instance.PlayPreview(diff.Difficulty.Mp3Path, diff.Difficulty.PreviewTime);
                    }
                }
                catch (TaskCanceledException) { }
            }
        }

        private void DifficultyCard_PointerExited(object? sender, PointerEventArgs e)
        {
            lock (_hoverLock) { _hoverDelayCancellation?.Cancel(); }
            Services.AudioService.Instance.Stop();
        }

        // Queue Bar Hover Logic
        private CancellationTokenSource? _queueBarCancellation;
        private readonly object _queueBarLock = new object();

        private async void BottomBar_PointerEntered(object? sender, PointerEventArgs e)
        {
            if (_viewModel == null) return;
            
            lock (_queueBarLock)
            {
                _queueBarCancellation?.Cancel();
                _queueBarCancellation = new CancellationTokenSource();
            }
            
            var token = _queueBarCancellation.Token;
            
            try
            {
                // Quick expand
                await Task.Delay(50, token); 
                if (token.IsCancellationRequested) return;
                
                _viewModel.IsBottomBarExpanded = true;
            }
            catch (TaskCanceledException) { }
        }

        private async void BottomBar_PointerExited(object? sender, PointerEventArgs e)
        {
             if (_viewModel == null) return;
            
            lock (_queueBarLock)
            {
                _queueBarCancellation?.Cancel();
                _queueBarCancellation = new CancellationTokenSource();
            }
            
            var token = _queueBarCancellation.Token;
            
            try
            {
                // Delayed collapse (for forgiving UI)
                await Task.Delay(300, token); 
                if (token.IsCancellationRequested) return;
                
                _viewModel.IsBottomBarExpanded = false;
            }
            catch (TaskCanceledException) { }
        }
    }
}

