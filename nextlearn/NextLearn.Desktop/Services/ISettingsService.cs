using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }

    string Theme { get; set; }

    string Font { get; set; }

    string DecksPath { get; set; }

    string ResolvedDecksPath { get; }

    bool TrySave(out string? error);
}
