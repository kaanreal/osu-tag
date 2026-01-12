using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OsuTag.Services;
using OsuTag.Views;

namespace OsuTag;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            
            // Initialize telemetry (fire-and-forget)
            _ = TelemetryService.TrackAppLaunch();
            
            desktop.Exit += (sender, args) =>
            {
                // Track session stop on exit
                try
                {
                    _ = TelemetryService.TrackSessionStop();
                }
                catch
                {
                    // Telemetry must not crash the app
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
