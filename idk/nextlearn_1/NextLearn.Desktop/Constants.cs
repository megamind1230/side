using System;
using System.IO;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop;

public static class Constants
{
    public static string DefaultDecksDir =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "nextlearn", "decks");

    public static string GetDecksPath(string? settingsPath = null)
    {
        var path = !string.IsNullOrEmpty(settingsPath)
            ? AppSettings.ResolvePath(settingsPath)
            : DefaultDecksDir;
        Directory.CreateDirectory(path);
        return path;
    }
}
