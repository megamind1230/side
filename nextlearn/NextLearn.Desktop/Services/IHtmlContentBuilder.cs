using System.Collections.Generic;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public interface IHtmlContentBuilder
{
    string Build(Page? page, bool isOrgFile, string? imageDir = null, List<string>? accumulatedImagePaths = null);
}
