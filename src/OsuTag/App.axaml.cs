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
            Console.WriteLine($"[App] ApplyTheme called with color: {hexColor}");
            
            if (string.IsNullOrEmpty(hexColor))
            {
                Console.WriteLine("[App] hexColor is null or empty, skipping");
                return;
            }
            
            var color = Avalonia.Media.Color.Parse(hexColor);
            Console.WriteLine($"[App] Parsed color: {color}");
            
            if (Application.Current == null)
            {
                Console.WriteLine("[App] Application.Current is null!");
                return;
            }
            
            // Update AccentBrush
            try
            {
                if (Application.Current.Resources.TryGetValue("AccentBrush", out var brushObj) && 
                    brushObj is Avalonia.Media.SolidColorBrush brush)
                {
                    // Create a new brush instead of modifying the existing one
                    Application.Current.Resources["AccentBrush"] = new Avalonia.Media.SolidColorBrush(color);
                    Console.WriteLine("[App] Updated AccentBrush");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App] Error updating AccentBrush: {ex.Message}");
            }
            
            // Update AccentHoverBrush
            try
            {
                if (Application.Current.Resources.TryGetValue("AccentHoverBrush", out var hoverObj) && 
                    hoverObj is Avalonia.Media.SolidColorBrush hoverBrush)
                {
                    Application.Current.Resources["AccentHoverBrush"] = new Avalonia.Media.SolidColorBrush(color);
                    Console.WriteLine("[App] Updated AccentHoverBrush");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App] Error updating AccentHoverBrush: {ex.Message}");
            }

            // Update AccentShadow
            try
            {
                var shadowColor = Avalonia.Media.Color.FromUInt32((0x40000000 | (uint)(color.R << 16) | (uint)(color.G << 8) | color.B));
                var shadow = new Avalonia.Media.BoxShadows(new Avalonia.Media.BoxShadow 
                { 
                    Blur = 16, 
                    Color = shadowColor 
                });
                
                Application.Current.Resources["AccentShadow"] = shadow;
                Console.WriteLine("[App] Updated AccentShadow");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App] Error updating AccentShadow: {ex.Message}");
            }
            
            // Update AccentSubtleBrush
            try
            {
                if (Application.Current.Resources.TryGetValue("AccentSubtleBrush", out var subtleObj) && 
                    subtleObj is Avalonia.Media.SolidColorBrush subtleBrush)
                {
                    var subtleColor = Avalonia.Media.Color.FromUInt32((0x4D000000 | (uint)(color.R << 16) | (uint)(color.G << 8) | color.B));
                    Application.Current.Resources["AccentSubtleBrush"] = new Avalonia.Media.SolidColorBrush(subtleColor);
                    Console.WriteLine("[App] Updated AccentSubtleBrush");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App] Error updating AccentSubtleBrush: {ex.Message}");
            }

            // Update AccentHoverShadow
            try
            {
                var hoverShadowColor = Avalonia.Media.Color.FromUInt32((0x80000000 | (uint)(color.R << 16) | (uint)(color.G << 8) | color.B));
                var hoverShadow = new Avalonia.Media.BoxShadows(new Avalonia.Media.BoxShadow 
                { 
                    Blur = 16, 
                    Color = hoverShadowColor 
                });
                Application.Current.Resources["AccentHoverShadow"] = hoverShadow;
                Console.WriteLine("[App] Updated AccentHoverShadow");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[App] Error updating AccentHoverShadow: {ex.Message}");
            }
            
            Console.WriteLine("[App] ApplyTheme completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[App] ApplyTheme failed: {ex.Message}");
            Console.WriteLine($"[App] Stack trace: {ex.StackTrace}");
        }
    }
}
