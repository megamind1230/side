using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace NextLearn.Desktop.Models;

public class KeyBindingsDocument
{
    [YamlMember(Alias = "_help")]
    public string? Help { get; set; }

    [YamlMember(Alias = "bindings")]
    public List<KeyBinding> Bindings { get; set; } = new();
}
