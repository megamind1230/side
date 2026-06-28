namespace NextLearn.Desktop.Models;

public class CommandPaletteEntry
{
    public string Name { get; init; } = string.Empty;

    public string[] Aliases { get; init; } = [];

    public string Description { get; init; } = string.Empty;

    public string[] Contexts { get; init; } = [];

    public System.Action Execute { get; init; } = () => { };

    public string ShortcutText { get; init; } = string.Empty;

    public string AliasesText => Aliases.Length > 0 ? string.Join(", ", Aliases) : string.Empty;
}
