using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

/// <summary>Manages application settings persisted as JSON.</summary>
public interface ISettingsService
{
    /// <summary>Gets the current settings object.</summary>
    AppSettings Settings { get; }

    /// <summary>Gets or sets the UI theme.</summary>
    string Theme { get; set; }

    /// <summary>Gets or sets the display font.</summary>
    string Font { get; set; }

    /// <summary>Gets or sets the decks directory path (may contain $HOME).</summary>
    string DecksPath { get; set; }

    /// <summary>Gets the resolved decks path with $HOME expanded.</summary>
    string ResolvedDecksPath { get; }

    /// <summary>Tries to persist the current settings to disk.</summary>
    /// <param name="error">When false, contains the error message.</param>
    /// <returns>True if saved successfully.</returns>
    bool TrySave(out string? error);
}