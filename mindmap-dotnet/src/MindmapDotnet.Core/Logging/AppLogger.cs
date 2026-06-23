using Serilog;

namespace MindmapDotnet.Core.Logging;

public static class AppLogger
{
    private static ILogger _logger = Serilog.Log.Logger;

    public static void Initialize(ILogger logger)
    {
        _logger = logger;
    }

    public static ILogger ForContext<T>() => _logger.ForContext<T>();

    public static void Debug(string message, params object?[] args) =>
        _logger.Debug(message, args);

    public static void Info(string message, params object?[] args) =>
        _logger.Information(message, args);

    public static void Warn(string message, params object?[] args) =>
        _logger.Warning(message, args);

    public static void Error(Exception ex, string message, params object?[] args) =>
        _logger.Error(ex, message, args);
}
