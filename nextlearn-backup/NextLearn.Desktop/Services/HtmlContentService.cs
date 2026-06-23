using System.Collections.Generic;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public class HtmlContentService : IHtmlContentBuilder
{
    public string Build(Page? page, bool isOrgFile, string? imageDir = null, string? fontFamily = null, List<string>? accumulatedImagePaths = null)
    {
        return HtmlContentBuilder.Build(page, isOrgFile, imageDir, fontFamily, accumulatedImagePaths);
    }
}
