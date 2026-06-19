using NextLearn.Desktop.Services;
using YamlDotNet.Serialization;

namespace NextLearn.Desktop.Models;

public class KeyBinding
{
    [YamlMember(Alias = "action")]
    public KeyboardActionKind Action { get; set; }

    [YamlMember(Alias = "key")]
    public string Key { get; set; } = string.Empty;

    [YamlMember(Alias = "modifiers")]
    public string Modifiers { get; set; } = string.Empty;

    [YamlMember(Alias = "context")]
    public string? Context { get; set; }

    [YamlMember(Alias = "textBox")]
    public bool TextBox { get; set; }

    [YamlMember(Alias = "_comment")]
    public string? Comment { get; set; }
}
