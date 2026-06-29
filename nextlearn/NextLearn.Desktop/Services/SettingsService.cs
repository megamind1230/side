using System;
using System.IO;
using NextLearn.Desktop.Models;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NextLearn.Desktop.Services;

public class SettingsService : ISettingsService
{
    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly ISerializer YamlSerializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

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
        _filePath = Path.Combine(configDir, "settings.yaml");
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

    public bool FalconEyeEnabled
    {
        get => _settings.FalconEyeEnabled;
        set => _settings.FalconEyeEnabled = value;
    }

    public string GeminiApiKey
    {
        get => _settings.GeminiApiKey;
        set => _settings.GeminiApiKey = value;
    }

    public string ResolvedDecksPath => AppSettings.ResolvePath(DecksPath);

    public static AppSettings Defaults() => new();

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var yaml = File.ReadAllText(_filePath);
                return YamlDeserializer.Deserialize<AppSettings>(yaml);
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or IOException or YamlException)
        {
        }

        return new AppSettings();
    }

    public bool TrySave(out string? error)
    {
        try
        {
            var yaml = YamlSerializer.Serialize(_settings);
            File.WriteAllText(_filePath, yaml);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or YamlException)
        {
            error = ex.Message;
            return false;
        }
    }
}
