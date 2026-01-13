using System;
using Avalonia;

namespace OsuTag;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .With(new Win32PlatformOptions
            {
                CompositionMode = new[] { Win32CompositionMode.WinUIComposition }
            })
            .With(new SkiaOptions
            {
                MaxGpuResourceSizeBytes = 256000000 // 256MB GPU cache for ultra-smooth animations
            })
            .LogToTrace();
}
