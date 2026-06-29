using System.Collections.Generic;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public class HtmlContentService : IHtmlContentBuilder
{
    public string Build(Page? page, bool isOrgFile, string? imageDir = null, string? fontFamily = null, List<string>? accumulatedImagePaths = null, IReadOnlyDictionary<string, string>? allFootnotes = null, string? decksPath = null, string? currentDir = null)
    {
        return HtmlContentBuilder.Build(page, isOrgFile, imageDir, fontFamily, accumulatedImagePaths, allFootnotes, decksPath, currentDir);
    }
}
