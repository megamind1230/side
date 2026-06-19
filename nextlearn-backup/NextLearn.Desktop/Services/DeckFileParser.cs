using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NextLearn.Desktop.Models;
using Serilog;

namespace NextLearn.Desktop.Services;

public static class DeckFileParser
{
    public static Deck? LoadDeckFromFile(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var lines = File.ReadAllLines(filePath);
            var fileName = Path.GetFileName(filePath);
            bool isOrg = fileName.EndsWith(".org", StringComparison.OrdinalIgnoreCase);

            string title;
            string description;
            string tags;
            int contentStart;
            bool hasExplicitTitle;

            if (isOrg)
            {
                var orgMeta = ParseOrgKeywords(lines);
                hasExplicitTitle = orgMeta.title != null;
                title = orgMeta.title ?? FirstNonEmptyLine(lines, orgMeta.contentStart) ?? Path.GetFileName(filePath);
                description = orgMeta.description ?? SecondNonEmptyLine(lines, orgMeta.contentStart) ?? string.Empty;
                tags = orgMeta.tags ?? string.Empty;
                contentStart = orgMeta.contentStart;
            }
            else
            {
                var frontmatter = ParseFrontmatter(lines);
                if (frontmatter != null)
                {
                    hasExplicitTitle = frontmatter.Value.title != null;
                    title = frontmatter.Value.title ?? FirstNonEmptyLine(lines, frontmatter.Value.endIndex + 1) ?? Path.GetFileName(filePath);
                    description = frontmatter.Value.description ?? SecondNonEmptyLine(lines, frontmatter.Value.endIndex + 1) ?? string.Empty;
                    tags = frontmatter.Value.tags ?? string.Empty;
                    contentStart = frontmatter.Value.endIndex + 1;
                }
                else
                {
                    hasExplicitTitle = false;
                    var nonEmpty = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Take(2).ToArray();
                    title = nonEmpty.Length > 0 ? nonEmpty[0].Trim() : Path.GetFileName(filePath);
                    description = nonEmpty.Length > 1 ? (nonEmpty[1].Length > 200 ? nonEmpty[1][..200] : nonEmpty[1]) : string.Empty;
                    tags = string.Empty;
                    contentStart = 0;
                }
            }

            var pages = ParsePages(lines, contentStart, title, isOrg);
            if (pages.Count == 0)
            {
                pages.Add(new Page
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    TextContent = content.Trim(),
                    ContentType = ContentType.Text,
                    PageNumber = 1,
                });
            }

            var deck = new Deck
            {
                Id = DeckFileIdentity.GetId(filePath),
                FileName = fileName,
                Title = title,
                Description = description,
                Tags = tags,
                HasExplicitTitle = hasExplicitTitle,
                IsPublished = true,
                IsReviewed = true,
                IsArchived = fileName.EndsWith('~'),
                IsPinned = fileName.StartsWith('+'),
                CreatedAt = File.GetCreationTime(filePath),
                PageCount = pages.Count,
                Pages = pages,
            };

            foreach (var page in pages)
            {
                page.DeckId = deck.Id;
            }

            return deck;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException or PathTooLongException or IOException)
        {
            Log.Error(ex, "Failed to parse deck file {FilePath}", filePath);
            return null;
        }
    }

    private static string? FirstNonEmptyLine(string[] lines, int start)
    {
        for (int i = start; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                return lines[i].Trim();
            }
        }

        return null;
    }

    private static string? SecondNonEmptyLine(string[] lines, int start)
    {
        int count = 0;
        for (int i = start; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                if (count == 1)
                {
                    var val = lines[i].Trim();
                    return val.Length > 200 ? val[..200] : val;
                }

                count++;
            }
        }

        return null;
    }

    private static (string? title, string? description, string? tags, int contentStart) ParseOrgKeywords(string[] lines)
    {
        string? title = null;
        string? description = null;
        string? tags = null;
        int i;

        for (i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            var match = Regex.Match(trimmed, @"^#\+(\w+):\s*(.*)", RegexOptions.IgnoreCase);
            if (!match.Success)
            {
                break;
            }

            var keyword = match.Groups[1].Value.ToUpperInvariant();
            var value = match.Groups[2].Value.Trim();

            switch (keyword)
            {
                case "TITLE":
                    title = value;
                    break;
                case "DESCRIPTION":
                    description = value;
                    break;
                case "TAGS":
                    tags = NormalizeOrgTags(value);
                    break;
            }
        }

        return (title, description, tags, i);
    }

    private static string NormalizeOrgTags(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        raw = raw.Trim();
        if (!raw.StartsWith(':') || !raw.EndsWith(':'))
        {
            return raw;
        }

        var inner = raw.Substring(1, raw.Length - 2);
        inner = inner.Replace("::", "/");
        var parts = inner.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(", ", parts);
    }

    private static (string? title, string? description, string? tags, int endIndex)? ParseFrontmatter(string[] lines)
    {
        if (lines.Length == 0 || !lines[0].Trim().Equals("---"))
        {
            return null;
        }

        string? title = null;
        string? description = null;
        string? tags = null;
        int i;

        for (i = 1; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed == "---")
            {
                return (title, description, tags, i);
            }

            if (trimmed.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
            {
                title = trimmed[6..].Trim();
            }
            else if (trimmed.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
            {
                description = trimmed[12..].Trim();
            }
            else if (trimmed.StartsWith("tags:", StringComparison.OrdinalIgnoreCase))
            {
                tags = trimmed[5..].Trim();
            }
        }

        return (title, description, tags, i);
    }

    private static List<Page> ParsePages(string[] lines, int startLine, string fallbackTitle, bool isOrg)
    {
        var pages = new List<Page>();
        var currentSection = string.Empty;
        var currentTitle = string.Empty;
        var currentContent = new StringBuilder();
        var sectionContent = new StringBuilder();
        bool inPage = false;
        bool hasSectionContent = false;
        int pageNum = 1;

        // Scan once to determine two-level (H1→H2 section/page) or flat (H1-only) heading structure
        bool hasH2 = false;
        var h2Marker = isOrg ? "**" : "##";

        for (int i = startLine; i < lines.Length; i++)
        {
            if (isOrg ? Regex.IsMatch(lines[i], @"^\*\*\s+") : Regex.IsMatch(lines[i], @"^(?:##|\*\*)\s+"))
            {
                hasH2 = true;
                break;
            }
        }

        var preContent = new StringBuilder();
        bool seenFirstHeading = false;
        bool inCodeBlock = false;

        for (int i = startLine; i < lines.Length; i++)
        {
            var line = lines[i];

            var isFence = Regex.IsMatch(line, @"^```");
            var isBeginSrc = Regex.IsMatch(line, @"^#\+BEGIN_SRC", RegexOptions.IgnoreCase);
            var isEndSrc = Regex.IsMatch(line, @"^#\+END_SRC", RegexOptions.IgnoreCase);

            if (isFence)
            {
                inCodeBlock = !inCodeBlock;
                if (seenFirstHeading)
                {
                    if (inPage)
                    {
                        currentContent.AppendLine(line);
                    }
                    else if (hasSectionContent)
                    {
                        sectionContent.AppendLine(line);
                    }
                }
                else
                {
                    preContent.AppendLine(line);
                }

                continue;
            }

            if (isBeginSrc)
            {
                inCodeBlock = true;
                if (seenFirstHeading)
                {
                    if (inPage)
                    {
                        currentContent.AppendLine(line);
                    }
                    else if (hasSectionContent)
                    {
                        sectionContent.AppendLine(line);
                    }
                }
                else
                {
                    preContent.AppendLine(line);
                }

                continue;
            }

            if (isEndSrc)
            {
                inCodeBlock = false;
                if (seenFirstHeading)
                {
                    if (inPage)
                    {
                        currentContent.AppendLine(line);
                    }
                    else if (hasSectionContent)
                    {
                        sectionContent.AppendLine(line);
                    }
                }
                else
                {
                    preContent.AppendLine(line);
                }

                continue;
            }

            if (inCodeBlock)
            {
                if (seenFirstHeading)
                {
                    if (inPage)
                    {
                        currentContent.AppendLine(line);
                    }
                    else if (hasSectionContent)
                    {
                        sectionContent.AppendLine(line);
                    }
                }
                else
                {
                    preContent.AppendLine(line);
                }

                continue;
            }

            // Content before the first heading becomes an IsPreHeadingPage (e.g. YAML frontmatter text)
            if (!seenFirstHeading)
            {
                var isH1 = Regex.IsMatch(line, isOrg ? @"^\*\s+" : @"^[#*]\s+");
                var isH2 = Regex.IsMatch(line, isOrg ? @"^\*\*\s+" : @"^(?:##|\*\*)\s+");
                if (isH1 || isH2)
                {
                    seenFirstHeading = true;
                    if (preContent.Length > 0)
                    {
                        pages.Add(new Page
                        {
                            Id = Guid.NewGuid(),
                            SectionTitle = null,
                            Title = fallbackTitle,
                            TextContent = preContent.ToString().Trim(),
                            ContentType = ContentType.Text,
                            PageNumber = pageNum++,
                            IsPreHeadingPage = true,
                        });
                    }
                }
                else
                {
                    preContent.AppendLine(line);
                    continue;
                }
            }

            var h1Match = Regex.Match(line, isOrg ? @"^\*\s+(.+)$" : @"^[#*]\s+(.+)$");
            var h2Match = Regex.Match(line, isOrg ? @"^\*\*\s+(.+)$" : @"^(?:##|\*\*)\s+(.+)$");

            // H2 heading: flush previous page, start a new page under the current H1 section
            if (h2Match.Success)
            {
                string? orphanContent = null;
                if (hasSectionContent && sectionContent.Length > 0)
                {
                    orphanContent = sectionContent.ToString().Trim();
                }

                hasSectionContent = false;
                sectionContent.Clear();

                if (inPage && currentTitle.Length > 0)
                {
                    pages.Add(new Page
                    {
                        Id = Guid.NewGuid(),
                        SectionTitle = currentSection,
                        Title = currentTitle,
                        TextContent = currentContent.ToString().Trim(),
                        ContentType = ContentType.Text,
                        PageNumber = pageNum++,
                    });
                }

                if (orphanContent != null)
                {
                    pages.Add(new Page
                    {
                        Id = Guid.NewGuid(),
                        SectionTitle = currentSection,
                        Title = currentSection,
                        TextContent = $"{h2Marker} no H2 heading found yet\n\n{orphanContent}",
                        ContentType = ContentType.Text,
                        PageNumber = pageNum++,
                    });
                }

                currentTitle = h2Match.Groups[1].Value.Trim();
                currentContent.Clear();
                inPage = true;

                currentContent.AppendLine(line);
            }

            // H1 heading: flush pending pages, set new section context. With H2→H1 sections act as heading groups; without H2→H1 is the page title directly
            else if (h1Match.Success)
            {
                // Flush any pending section-level page first
                if (hasSectionContent && sectionContent.Length > 0)
                {
                    pages.Add(new Page
                    {
                        Id = Guid.NewGuid(),
                        SectionTitle = currentSection,
                        Title = currentSection,
                        TextContent = $"{h2Marker} no H2 heading found yet\n\n{sectionContent.ToString().Trim()}",
                        ContentType = ContentType.Text,
                        PageNumber = pageNum++,
                    });
                }

                if (inPage && currentTitle.Length > 0)
                {
                    pages.Add(new Page
                    {
                        Id = Guid.NewGuid(),
                        SectionTitle = currentSection,
                        Title = currentTitle,
                        TextContent = currentContent.ToString().Trim(),
                        ContentType = ContentType.Text,
                        PageNumber = pageNum++,
                    });
                }

                currentSection = h1Match.Groups[1].Value.Trim();
                currentTitle = hasH2 ? string.Empty : currentSection;
                currentContent.Clear();
                sectionContent.Clear();
                inPage = !hasH2;
                hasSectionContent = hasH2;

                // Include H1 heading line in content for inline rendering (page-only mode)
                if (!hasH2)
                {
                    currentContent.AppendLine(line);
                }
            }
            else if (inPage)
            {
                currentContent.AppendLine(line);
            }
            else if (hasSectionContent)
            {
                sectionContent.AppendLine(line);
            }
        }

        if (!seenFirstHeading && preContent.Length > 0)
        {
            pages.Add(new Page
            {
                Id = Guid.NewGuid(),
                SectionTitle = null,
                Title = fallbackTitle,
                TextContent = preContent.ToString().Trim(),
                ContentType = ContentType.Text,
                PageNumber = pageNum++,
                IsPreHeadingPage = true,
            });
        }
        else if (hasSectionContent && sectionContent.Length > 0)
        {
            pages.Add(new Page
            {
                Id = Guid.NewGuid(),
                SectionTitle = currentSection,
                Title = currentSection,
                TextContent = $"{h2Marker} no H2 heading found yet\n\n{sectionContent.ToString().Trim()}",
                ContentType = ContentType.Text,
                PageNumber = pageNum++,
            });
        }
        else if (inPage && currentTitle.Length > 0)
        {
            pages.Add(new Page
            {
                Id = Guid.NewGuid(),
                SectionTitle = hasH2 ? currentSection : null,
                Title = currentTitle,
                TextContent = currentContent.ToString().Trim(),
                ContentType = ContentType.Text,
                PageNumber = pageNum++,
            });
        }

        return pages;
    }
}
