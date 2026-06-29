using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NextLearn.Desktop.Services;

public class DeckFileWriter : IDeckFileWriter
{
    public bool AppendTags(string filePath, List<string> newTags, out string? error)
    {
        error = null;

        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(newTags);

        try
        {
            var lines = File.ReadAllLines(filePath).ToList();
            var isOrg = filePath.EndsWith(".org", StringComparison.OrdinalIgnoreCase);
            var existingTags = new List<string>();
            int tagsLineIndex = -1;
            string? detectedFormat = null;

            if (isOrg)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var match = Regex.Match(lines[i].Trim(), @"^#\+TAGS:\s*(.*)", RegexOptions.IgnoreCase);
                    if (!match.Success)
                    {
                        continue;
                    }

                    tagsLineIndex = i;
                    var raw = match.Groups[1].Value.Trim();

                    if (raw.StartsWith(':') && raw.EndsWith(':'))
                    {
                        detectedFormat = "org-colon";
                        var inner = raw.Length >= 2 ? raw[1..^1] : string.Empty;
                        inner = inner.Replace("::", "/");
                        existingTags = inner.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    }
                    else
                    {
                        detectedFormat = "org-plain";
                        existingTags = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    }

                    break;
                }
            }
            else
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    var trimmed = lines[i].Trim();
                    if (!trimmed.StartsWith("tags:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var raw = trimmed[5..].Trim();

                    if ((raw.StartsWith('"') && raw.EndsWith('"')) ||
                        (raw.StartsWith('\'') && raw.EndsWith('\'')))
                    {
                        detectedFormat = "quoted";
                        raw = raw.Length >= 2 ? raw[1..^1] : string.Empty;
                        existingTags = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(t => t.Trim()).ToList();
                        tagsLineIndex = i;
                        break;
                    }

                    if (raw.StartsWith('[') && raw.EndsWith(']'))
                    {
                        detectedFormat = "array";
                        var inner = raw.Length >= 2 ? raw[1..^1] : string.Empty;
                        existingTags = inner.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .Select(t => t.Trim()).ToList();
                        tagsLineIndex = i;
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        var listItems = new List<string>();
                        int j = i + 1;
                        while (j < lines.Count)
                        {
                            var listMatch = Regex.Match(lines[j], @"^\s*-\s+(.+)$");
                            if (!listMatch.Success)
                            {
                                break;
                            }

                            listItems.Add(listMatch.Groups[1].Value.Trim());
                            j++;
                        }

                        if (listItems.Count > 0)
                        {
                            detectedFormat = "block-list";
                            existingTags = listItems;
                            tagsLineIndex = i;
                            break;
                        }

                        tagsLineIndex = i;
                        detectedFormat = "empty";
                        break;
                    }

                    detectedFormat = "inline";
                    existingTags = raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(t => t.Trim()).ToList();
                    tagsLineIndex = i;
                    break;
                }
            }

            var existingSet = new HashSet<string>(existingTags, StringComparer.OrdinalIgnoreCase);
            var toAdd = newTags
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t) && !existingSet.Contains(t))
                .ToList();

            if (toAdd.Count == 0)
            {
                error = "No new tags to add.";
                return false;
            }

            var combined = existingTags.Concat(toAdd).ToList();

            switch (detectedFormat)
            {
                case "org-colon":
                    var colonTags = string.Join(":", combined.Select(t => t.Replace("/", "::")));
                    lines[tagsLineIndex] = $"#+TAGS: :{colonTags}:";
                    break;

                case "org-plain":
                    lines[tagsLineIndex] = $"#+TAGS: {string.Join(", ", combined)}";
                    break;

                case "quoted":
                    lines[tagsLineIndex] = $"tags: \"{string.Join(", ", combined)}\"";
                    break;

                case "array":
                    lines[tagsLineIndex] = $"tags: [{string.Join(", ", combined)}]";
                    break;

                case "block-list":
                    var blockLines = combined.Select(t => $"  - {t}");
                    lines[tagsLineIndex] = "tags:";
                    lines.InsertRange(tagsLineIndex + 1, blockLines);
                    break;

                case "inline":
                case "empty":
                default:
                    lines[tagsLineIndex] = $"tags: {string.Join(", ", combined)}";
                    break;
            }

            File.WriteAllLines(filePath, lines);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool EnsureHealthyFrontmatter(string filePath, out string? error)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        error = null;

        try
        {
            var isOrg = filePath.EndsWith(".org", StringComparison.OrdinalIgnoreCase);
            var lines = File.ReadAllLines(filePath).ToList();
            var original = string.Join("\n", lines);

            if (isOrg)
            {
                NormalizeOrgFrontmatter(lines);
            }
            else
            {
                NormalizeMdFrontmatter(lines, filePath);
            }

            var normalized = string.Join("\n", lines);
            if (normalized == original)
            {
                return true;
            }

            File.WriteAllLines(filePath, lines);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void NormalizeMdFrontmatter(List<string> lines, string filePath)
    {
        var entries = new List<(string key, string value)>();
        var scanLimit = Math.Min(15, lines.Count);

        // Try to find --- delimiters within first 15 lines (not — horizontal rules in content)
        int openIdx = -1;
        int closeIdx = -1;
        for (int i = 0; i < scanLimit; i++)
        {
            if (lines[i].Trim() == "---")
            {
                if (openIdx == -1)
                {
                    openIdx = i;
                }
                else
                {
                    closeIdx = i;
                    break;
                }
            }
        }

        if (openIdx != -1 && closeIdx != -1)
        {
            // Collect entries between delimiters
            for (int i = openIdx + 1; i < closeIdx; i++)
            {
                var parsed = ParseMdEntry(lines[i]);
                if (parsed != null && !entries.Any(e => string.Equals(e.key, parsed.Value.key, StringComparison.OrdinalIgnoreCase)))
                {
                    entries.Add(parsed.Value);
                }
            }
        }
        else
        {
            // No frontmatter delimiters — scan first 15 lines for key: value
            for (int i = 0; i < scanLimit; i++)
            {
                var parsed = ParseMdEntry(lines[i]);
                if (parsed != null && !entries.Any(e => string.Equals(e.key, parsed.Value.key, StringComparison.OrdinalIgnoreCase)))
                {
                    entries.Add(parsed.Value);
                }
            }
        }

        // Determine desc key
        bool hasDesc = entries.Any(e => string.Equals(e.key, "desc", StringComparison.OrdinalIgnoreCase));
        bool hasDescription = entries.Any(e => string.Equals(e.key, "description", StringComparison.OrdinalIgnoreCase));

        // Build ordered entry list: title, desc/description, tags, then extras
        var ordered = new List<(string key, string value)>();
        var keysSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfMissing(string key, string value)
        {
            if (keysSeen.Add(key))
            {
                var existing = entries.FirstOrDefault(e => string.Equals(e.key, key, StringComparison.OrdinalIgnoreCase));
                ordered.Add(existing.key != null ? existing : (key, value));
            }
        }

        AddIfMissing("title", string.Empty);

        if (hasDesc)
        {
            AddIfMissing("desc", string.Empty);
        }
        else if (hasDescription)
        {
            AddIfMissing("description", string.Empty);
        }
        else
        {
            ordered.Add(("desc", string.Empty));
            keysSeen.Add("desc");
        }

        AddIfMissing("tags", string.Empty);

        // Add extras (any keys not yet added)
        foreach (var entry in entries)
        {
            if (keysSeen.Add(entry.key))
            {
                ordered.Add(entry);
            }
        }

        // Rebuild lines
        var newLines = new List<string> { "---" };
        foreach (var (key, value) in ordered)
        {
            newLines.Add(string.IsNullOrEmpty(value) ? $"{key}: " : $"{key}: {value}");
        }

        newLines.Add("---");

        // Append body — keep all content, never delete anything
        if (openIdx != -1 && closeIdx != -1 && closeIdx + 1 < lines.Count)
        {
            // Proper frontmatter: skip the original --- block
            newLines.AddRange(lines.Skip(closeIdx + 1));
        }
        else
        {
            // No or malformed frontmatter: keep all original content
            newLines.AddRange(lines);
        }

        lines.Clear();
        lines.AddRange(newLines);
    }

    private static (string key, string value)? ParseMdEntry(string line)
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed == "---")
        {
            return null;
        }

        var colonIdx = trimmed.IndexOf(':');
        if (colonIdx <= 0)
        {
            return null;
        }

        var key = trimmed[..colonIdx].Trim();
        var value = trimmed[(colonIdx + 1)..].Trim();

        if (!IsValidFrontmatterKey(key))
        {
            return null;
        }

        return (key, value);
    }

    private static bool IsValidFrontmatterKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        for (int i = 0; i < key.Length; i++)
        {
            var c = key[i];
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
            {
                return false;
            }
        }

        return true;
    }

    private static void NormalizeOrgFrontmatter(List<string> lines)
    {
        var scanLimit = Math.Min(15, lines.Count);
        var foundEntries = new List<(string key, string value)>();
        var foundIndices = new HashSet<int>();

        for (int i = 0; i < scanLimit; i++)
        {
            var trimmed = lines[i].Trim();
            var match = Regex.Match(trimmed, @"^#\+(\w+):\s*(.*)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                continue;
            }

            var key = match.Groups[1].Value.ToUpperInvariant();
            var value = match.Groups[2].Value.Trim();

            if (!foundEntries.Any(e => string.Equals(e.key, key, StringComparison.OrdinalIgnoreCase)))
            {
                foundEntries.Add((key, value));
                foundIndices.Add(i);
            }
        }

        // Ensure required keys
        bool hasTitle = foundEntries.Any(e => e.key == "TITLE");
        bool hasDesc = foundEntries.Any(e => e.key == "DESC");
        bool hasDescription = foundEntries.Any(e => e.key == "DESCRIPTION");
        bool hasTags = foundEntries.Any(e => e.key == "TAGS");

        var ordered = new List<(string key, string value)>();
        var keysAdded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfMissing(string key, string value)
        {
            if (keysAdded.Add(key))
            {
                var existing = foundEntries.FirstOrDefault(e => string.Equals(e.key, key, StringComparison.OrdinalIgnoreCase));
                ordered.Add(existing.key != null ? existing : (key, value));
            }
        }

        AddIfMissing("TITLE", string.Empty);

        if (hasDesc)
        {
            AddIfMissing("DESC", string.Empty);
        }
        else if (hasDescription)
        {
            AddIfMissing("DESCRIPTION", string.Empty);
        }
        else
        {
            ordered.Add(("DESC", string.Empty));
            keysAdded.Add("DESC");
        }

        AddIfMissing("TAGS", string.Empty);

        // Add extras (any keys not yet added, in order found)
        foreach (var entry in foundEntries)
        {
            if (keysAdded.Add(entry.key))
            {
                ordered.Add(entry);
            }
        }

        // Remove old #+ lines from the top
        lines.RemoveAll((line) =>
        {
            var trimmed = line.Trim();
            return Regex.IsMatch(trimmed, @"^#\+\w+:", RegexOptions.IgnoreCase);
        });

        // Remove leading blank lines
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        // Insert normalized entries at top
        var newLines = new List<string>();
        foreach (var (key, value) in ordered)
        {
            newLines.Add(string.IsNullOrEmpty(value) ? $"#+{key}: " : $"#+{key}: {value}");
        }

        newLines.AddRange(lines);
        lines.Clear();
        lines.AddRange(newLines);
    }
}
