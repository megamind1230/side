using FluentAssertions;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;
using Xunit;

namespace NextLearn.Desktop.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly List<string> _dirsToCleanup = [];

    public void Dispose()
    {
        foreach (var dir in _dirsToCleanup)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private string CreateConfigDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NextLearnTests", Guid.NewGuid().ToString());
        _dirsToCleanup.Add(dir);
        return dir;
    }

    [Fact]
    public void Defaults_WhenFileDoesNotExist()
    {
        var configDir = CreateConfigDir();
        var svc = new SettingsService(configDir);

        svc.Theme.Should().Be("Dark");
        svc.Font.Should().Be("Inter");
        svc.DecksPath.Should().Be("$HOME/nextlearn/decks");
    }

    [Fact]
    public void RoundTrip_SaveThenReload_ReturnsIdenticalSettings()
    {
        var configDir = CreateConfigDir();
        var svc = new SettingsService(configDir);

        svc.Theme = "Light";
        svc.Font = "Monospace";
        svc.DecksPath = "$HOME/custom/path";
        var saved = svc.TrySave(out var error);

        saved.Should().BeTrue();
        error.Should().BeNull();

        var reloaded = new SettingsService(configDir);
        reloaded.Theme.Should().Be("Light");
        reloaded.Font.Should().Be("Monospace");
        reloaded.DecksPath.Should().Be("$HOME/custom/path");
    }

    [Fact]
    public void TrySave_ReturnsTrueOnSuccess()
    {
        var configDir = CreateConfigDir();
        var svc = new SettingsService(configDir);

        var result = svc.TrySave(out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
    }

    [Fact]
    public void DecksPath_UpdatePersistsCorrectly()
    {
        var configDir = CreateConfigDir();
        var svc = new SettingsService(configDir);

        svc.DecksPath = "/absolute/path";
        svc.TrySave(out _);

        var reloaded = new SettingsService(configDir);
        reloaded.DecksPath.Should().Be("/absolute/path");
    }

    [Fact]
    public void CorruptedJson_FallsBackToDefaults()
    {
        var configDir = CreateConfigDir();
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "settings.json"), "this is not valid json {{{");

        var svc = new SettingsService(configDir);

        svc.Theme.Should().Be("Dark");
        svc.Font.Should().Be("Inter");
        svc.DecksPath.Should().Be("$HOME/nextlearn/decks");
    }

    [Fact]
    public void CorruptedJson_OverwritesOnSave()
    {
        var configDir = CreateConfigDir();
        Directory.CreateDirectory(configDir);
        File.WriteAllText(Path.Combine(configDir, "settings.json"), "{{{garbage}}}");

        var svc = new SettingsService(configDir);
        svc.Theme = "Custom";
        svc.TrySave(out _);

        var json = File.ReadAllText(Path.Combine(configDir, "settings.json"));
        json.Should().Contain("Custom");
        json.Should().Contain("\"Theme\"");
    }

    [Fact]
    public void Settings_ReturnsCurrentSettings()
    {
        var configDir = CreateConfigDir();
        var svc = new SettingsService(configDir);

        var settings = svc.Settings;

        settings.Should().NotBeNull();
        settings.Theme.Should().Be(svc.Theme);
        settings.Font.Should().Be(svc.Font);
        settings.DecksPath.Should().Be(svc.DecksPath);
    }

    [Fact]
    public void ResolvedDecksPath_ReplacesHomeVariable()
    {
        var configDir = CreateConfigDir();
        var svc = new SettingsService(configDir);

        var resolved = svc.ResolvedDecksPath;

        resolved.Should().NotContain("$HOME");
        resolved.Should().EndWith("/nextlearn/decks");
    }
}
