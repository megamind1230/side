using System;
using YamlDotNet.Serialization;

namespace NextLearn.Desktop.Models;

public class AppSettings
{
    [YamlMember(Alias = "theme")]
    public string Theme { get; set; } = "Dark";

    [YamlMember(Alias = "font")]
    public string Font { get; set; } = "Inter";

    [YamlMember(Alias = "decksPath")]
    public string DecksPath { get; set; } = "$HOME/nextlearn/decks";

    [YamlMember(Alias = "keyBindingsProfile")]
    public string KeyBindingsProfile { get; set; } = "Vim";

    [YamlMember(Alias = "falconEyeEnabled")]
    public bool FalconEyeEnabled { get; set; } = false;

    [YamlMember(Alias = "flashcardsPath")]
    public string FlashcardsPath { get; set; } = "$HOME/magnus/nextlearn/flashcards";

    [YamlMember(Alias = "geminiApiKey")]
    public string GeminiApiKey { get; set; } = string.Empty;

    public static string ResolvePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path.Replace("$HOME", home);
    }
}
