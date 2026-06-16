using System;
using System.IO;
using Avalonia;
using Serilog;

namespace NextLearn.Desktop;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        Serilog.Debugging.SelfLog.Enable(msg => System.Diagnostics.Debug.WriteLine(msg));

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var logDir = Path.Combine(home, "magnus", "nextlearn");
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "nextlearn.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(logPath, shared: true)
            .CreateLogger();

        Log.Information("=== NextLearn starting ===");
        Log.Information("Log path: {LogPath}", logPath);

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
#pragma warning disable CA1031
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .WithInterFont()
            .LogToTrace();

        var sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        if (sessionType?.Equals("wayland", StringComparison.OrdinalIgnoreCase) == true)
        {
            Log.Information("Wayland session detected, using X11 backend via XWayland");
            Environment.SetEnvironmentVariable("WAYLAND_DISPLAY", null);
            builder.UseSkia().UseX11();
        }
        else
        {
            builder.UsePlatformDetect();
        }

        return builder;
    }
}
