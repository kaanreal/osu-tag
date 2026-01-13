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
            
            // Apply saved theme color
            ApplyTheme(SettingsService.Settings.ThemeColor);
            
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

    public static void ApplyTheme(string hexColor)
    {
        try
        {
            if (string.IsNullOrEmpty(hexColor)) return;
            var color = Avalonia.Media.Color.Parse(hexColor);
            
            if (Application.Current?.Resources.TryGetValue("AccentBrush", out var brushObj) == true && 
                brushObj is Avalonia.Media.SolidColorBrush brush)
            {
                brush.Color = color;
            }
            
            // Update hover color (slightly lighter/brighter)
            if (Application.Current?.Resources.TryGetValue("AccentHoverBrush", out var hoverObj) == true && 
                hoverObj is Avalonia.Media.SolidColorBrush hoverBrush)
            {
                hoverBrush.Color = color; 
            }

            // Update AccentShadow (0x40 alpha = ~25% opacity)
            var shadowColor = Avalonia.Media.Color.FromUInt32((0x40000000 | (uint)(color.R << 16) | (uint)(color.G << 8) | color.B));
            var shadow = new Avalonia.Media.BoxShadows(new Avalonia.Media.BoxShadow 
            { 
                Blur = 16, 
                Color = shadowColor 
            });
            
            Application.Current.Resources["AccentShadow"] = shadow;
            
            // Update AccentSubtleBrush (0x4D alpha = ~30% opacity)
            if (Application.Current?.Resources.TryGetValue("AccentSubtleBrush", out var subtleObj) == true && 
                subtleObj is Avalonia.Media.SolidColorBrush subtleBrush)
            {
               var subtleColor = Avalonia.Media.Color.FromUInt32((0x4D000000 | (uint)(color.R << 16) | (uint)(color.G << 8) | color.B));
               subtleBrush.Color = subtleColor;
            }

            // Update AccentHoverShadow (0x80 alpha = ~50% opacity)
            var hoverShadowColor = Avalonia.Media.Color.FromUInt32((0x80000000 | (uint)(color.R << 16) | (uint)(color.G << 8) | color.B));
             var hoverShadow = new Avalonia.Media.BoxShadows(new Avalonia.Media.BoxShadow 
            { 
                Blur = 16, 
                Color = hoverShadowColor 
            });
            Application.Current.Resources["AccentHoverShadow"] = hoverShadow;
        }
        catch { }
    }
}
