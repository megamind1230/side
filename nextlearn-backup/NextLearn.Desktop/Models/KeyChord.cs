using YamlDotNet.Serialization;

namespace NextLearn.Desktop.Models;

public class KeyChord
{
    [YamlMember(Alias = "key")]
    public string Key { get; set; } = string.Empty;

    [YamlMember(Alias = "modifiers")]
    public string Modifiers { get; set; } = string.Empty;
}
