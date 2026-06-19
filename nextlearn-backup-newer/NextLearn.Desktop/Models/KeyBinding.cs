using System.Text.Json.Serialization;
using NextLearn.Desktop.Services;

namespace NextLearn.Desktop.Models;

public class KeyBinding
{
    [JsonPropertyName("action")]
    public KeyboardActionKind Action { get; set; }

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("modifiers")]
    public string Modifiers { get; set; } = string.Empty;

    [JsonPropertyName("context")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Context { get; set; }

    [JsonPropertyName("textBox")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool TextBox { get; set; }

    [JsonPropertyName("_comment")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Comment { get; set; }
}
