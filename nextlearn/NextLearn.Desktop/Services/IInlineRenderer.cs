using System.Collections.Generic;

namespace NextLearn.Desktop.Services;

public interface IInlineRenderer
{
    string RenderInline(string text, string? imageDir = null, List<string>? accumulatedImagePaths = null);
}
