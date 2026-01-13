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
                RenderingMode = new[] { Win32RenderingMode.CompositionBackbuffer, Win32RenderingMode.Wgl },
                CompositionMode = new[] { Win32CompositionMode.LowLatencyComposition, Win32CompositionMode.RedirectionQueues }
            })
            .With(new MacOSPlatformOptions
            {
                RenderingMode = new[] { MacOSRenderingMode.Metal, MacOSRenderingMode.Layered }
            })
            .LogToTrace();
}
