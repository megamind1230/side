using System.Collections.Generic;

namespace NextLearn.Desktop.Services;

public class TagInferenceResult
{
    public List<string> SuggestedTags { get; set; } = [];

    public bool Success { get; set; }

    public string? Error { get; set; }
}
