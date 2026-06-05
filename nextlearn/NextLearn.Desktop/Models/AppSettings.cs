using System;
using System.IO;

namespace NextLearn.Desktop.Models;

public class AppSettings
{
    public string Editor { get; set; } = "neovim";
    public string Theme { get; set; } = "Dark";
    public string Font { get; set; } = "Inter";
    public string DecksPath { get; set; } = "$HOME/nextlearn/decks";

    public static string ResolvePath(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path.Replace("$HOME", home);
    }
}
