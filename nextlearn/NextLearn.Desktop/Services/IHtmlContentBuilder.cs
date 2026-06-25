using System.Collections.Generic;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

/// <summary>Generates HTML documents from Page content for WebView rendering.</summary>
public interface IHtmlContentBuilder
{
    /// <summary>Builds a full HTML document for the given page.</summary>
    /// <param name="page">The page to render. Null produces an empty document.</param>
    /// <param name="isOrgFile">True if the source is an .org file (affects heading syntax).</param>
    /// <param name="imageDir">Optional base directory for resolving image paths.</param>
    /// <param name="fontFamily">Optional font family for the body text. Defaults to "Inter".</param>
    /// <param name="accumulatedImagePaths">Optional list to collect successfully loaded image paths.</param>
    /// <param name="allFootnotes">Optional merged footnotes from all pages (enables cross-page hover preview). If null, only local page footnotes are used.</param>
    /// <returns>A complete HTML document string.</returns>
    string Build(Page? page, bool isOrgFile, string? imageDir = null, string? fontFamily = null, List<string>? accumulatedImagePaths = null, IReadOnlyDictionary<string, string>? allFootnotes = null);
}