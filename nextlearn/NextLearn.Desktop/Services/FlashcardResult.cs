namespace NextLearn.Desktop.Services;

public class FlashcardResult
{
    public FlashcardGenerationMode Mode { get; set; }

    public string Content { get; set; } = string.Empty;

    public int Count { get; set; }

    public bool Success { get; set; }

    public string? Error { get; set; }
}
