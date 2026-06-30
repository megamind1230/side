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

    /// <summary>Gets or sets the key bindings profile name.</summary>
    string KeyBindingsProfile { get; set; }

    /// <summary>Gets or sets a value indicating whether Falcon Eye (table of contents) is enabled.</summary>
    bool FalconEyeEnabled { get; set; }

    /// <summary>Gets or sets the flashcards export path (may contain $HOME).</summary>
    string FlashcardsPath { get; set; }

    /// <summary>Gets the resolved flashcards path with $HOME expanded.</summary>
    string ResolvedFlashcardsPath { get; }

    /// <summary>Gets or sets the Gemini API key for AI tag inference.</summary>
    string GeminiApiKey { get; set; }

    /// <summary>Gets the resolved decks path with $HOME expanded.</summary>
    string ResolvedDecksPath { get; }

    /// <summary>Tries to persist the current settings to disk.</summary>
    /// <param name="error">When false, contains the error message.</param>
    /// <returns>True if saved successfully.</returns>
    bool TrySave(out string? error);
}