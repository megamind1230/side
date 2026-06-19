using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NextLearn.Desktop.Models;

public class KeyBindingsDocument
{
    [JsonPropertyName("_help")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Help { get; set; }

    [JsonPropertyName("bindings")]
    public List<KeyBinding> Bindings { get; set; } = new();
}
