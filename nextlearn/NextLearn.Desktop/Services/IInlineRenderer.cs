using System.Collections.Generic;

namespace NextLearn.Desktop.Services;

/// <summary>Renders inline formatting (bold, italic, links, images, code) within a line of text.</summary>
public interface IInlineRenderer
{
    /// <summary>Processes inline formatting and returns HTML.</summary>
    /// <param name="text">The text to render.</param>
    /// <param name="imageDir">Optional base directory for resolving image paths.</param>
    /// <param name="accumulatedImagePaths">Optional list to collect successfully loaded image paths.</param>
    /// <param name="footnoteDefinitions">Optional map of footnote id to rendered HTML content (for hover preview).</param>
    /// <returns>HTML-formatted string.</returns>
    string RenderInline(string text, string? imageDir = null, List<string>? accumulatedImagePaths = null, IReadOnlyDictionary<string, string>? footnoteDefinitions = null);
}