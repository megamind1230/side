using System;
using System.Diagnostics;
using Serilog;

namespace NextLearn.Desktop.Services;

public class EditorLauncher
{
    private readonly SettingsService _settings;

    public EditorLauncher(SettingsService settings)
    {
        _settings = settings;
    }

    public void Open(string filePath, string? searchText = null)
    {
        var editor = _settings.Editor.ToLowerInvariant();
        var (fileName, arguments) = BuildCommand(editor, filePath, searchText);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Normal
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to launch editor '{Editor}' for {FilePath}", editor, filePath);
        }
    }

    private static (string fileName, string arguments) BuildCommand(string editor, string filePath, string? search)
    {
        var hasSearch = !string.IsNullOrEmpty(search);

        return editor switch
        {
            "vscode" => hasSearch
                ? ("code", $"--goto \"{filePath}:1\"")
                : ("code", $"\"{filePath}\""),

            "emacs" => hasSearch
                ? ("emacs", $"--eval \"(progn (find-file \\\"{filePath}\\\") (search-forward \\\"{search}\\\"))\"")
                : ("emacs", $"\"{filePath}\""),

            "sublime" => hasSearch
                ? ("subl", $"\"{filePath}\":1")
                : ("subl", $"\"{filePath}\""),

            _ => hasSearch
                ? ("nvim", $"\"+/{search}\" \"{filePath}\"")
                : ("nvim", $"\"{filePath}\""),
        };
    }
}
