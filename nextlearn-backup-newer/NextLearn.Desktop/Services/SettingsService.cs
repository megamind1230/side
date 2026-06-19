using System;
using System.IO;
using System.Text.Json;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public class SettingsService : ISettingsService
{
    private readonly string _filePath;
    private AppSettings _settings;

    public SettingsService()
        : this(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "nextlearn"))
    {
    }

    public SettingsService(string configDir)
    {
        Directory.CreateDirectory(configDir);
        _filePath = Path.Combine(configDir, "settings.json");
        _settings = Load();
    }

    public AppSettings Settings => _settings;

    public string Theme
    {
        get => _settings.Theme;
        set => _settings.Theme = value;
    }

    public string Font
    {
        get => _settings.Font;
        set => _settings.Font = value;
    }

    public string DecksPath
    {
        get => _settings.DecksPath;
        set => _settings.DecksPath = value;
    }

    public string KeyBindingsProfile
    {
        get => _settings.KeyBindingsProfile;
        set => _settings.KeyBindingsProfile = value;
    }

    public string ResolvedDecksPath => AppSettings.ResolvePath(DecksPath);

    public static AppSettings Defaults() => new();

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or JsonException or IOException)
        {
        }

        return new AppSettings();
    }

    public bool TrySave(out string? error)
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or JsonException)
        {
            error = ex.Message;
            return false;
        }
    }
}
