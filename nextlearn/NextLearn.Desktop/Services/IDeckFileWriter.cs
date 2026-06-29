using System.Collections.Generic;

namespace NextLearn.Desktop.Services;

public interface IDeckFileWriter
{
    bool AppendTags(string filePath, List<string> newTags, out string? error);

    bool EnsureHealthyFrontmatter(string filePath, out string? error);
}
