using System;
using Avalonia;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Rendering.Composition;
using Avalonia.Styling;
using Avalonia.Media.Transformation;
using System.Diagnostics;
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

        protected override async void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            
            // Initialize Discord RPC
            Services.DiscordRpcService.Initialize();

            if (_viewModel != null)
            {
                // Start both animation and initialization in parallel
                var animTask = PlayEntranceAnimationAsync();
                var initTask = _viewModel.InitializeAsync();

                // Wait for both to complete
                await Task.WhenAll(animTask, initTask);

                // Now that both work and animation are done, signal completion
                _viewModel.IsInitialLoadDone = true;
            }
        }

        private async Task PlayEntranceAnimationAsync()
        {
            var icon = this.FindControl<Border>("BrandingIcon");
            var text = this.FindControl<TextBlock>("BrandingText");

            if (icon == null || text == null) return;

            // Wait for window to settle
            await Task.Delay(200);

            // Trigger XAML Transitions
            icon.Opacity = 1;
            icon.RenderTransform = TransformOperations.Parse("scale(1.0)");

            await Task.Delay(250);

            text.Opacity = 1;
            text.RenderTransform = TransformOperations.Parse("translateY(0px)");

            var loading = this.FindControl<Panel>("LoadingEntrance");
            if (loading != null)
            {
                await Task.Delay(200);
                loading.Opacity = 1;
                loading.RenderTransform = TransformOperations.Parse("translateY(0px)");
            }



            // Wait for entrance to finish (transitions are 1.0s)
            await Task.Delay(1200);
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
                    await Task.Delay(300, currentToken.Token);
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

        private void MapCard_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not Control control || control.DataContext is not MapItemGroup group || !group.IsStack)
                return;

            // Open Overlay (ViewModel)
            if (_viewModel != null)
            {
                 _viewModel.OverlayMapGroup = group;
                 _viewModel.IsOverlayOpen = true;
            }
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            BeginMoveDrag(e);
        }


    }
}

