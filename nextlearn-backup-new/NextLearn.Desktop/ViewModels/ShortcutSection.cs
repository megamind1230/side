using System.Collections.Generic;

namespace NextLearn.Desktop.ViewModels;

public class ShortcutSection
{
    public string Name { get; set; } = string.Empty;

    public List<ShortcutEntry> Entries { get; set; } = new();
}
