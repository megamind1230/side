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

            string title;
            string description;
            int contentStart;

            string tags;
            var frontmatter = ParseFrontmatter(lines);
            if (frontmatter != null)
            {
                title = frontmatter.Value.title ?? Path.GetFileNameWithoutExtension(filePath);
                description = frontmatter.Value.description ?? "";
                tags = frontmatter.Value.tags ?? "";
                contentStart = frontmatter.Value.endIndex + 1;
            }
            else
            {
                var nonEmpty = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Take(2).ToArray();
                title = nonEmpty.Length > 0 ? nonEmpty[0].Trim() : Path.GetFileNameWithoutExtension(filePath);
                description = nonEmpty.Length > 1 ? (nonEmpty[1].Length > 200 ? nonEmpty[1][..200] : nonEmpty[1]) : "";
                tags = "";
                contentStart = 0;
            }

            var pages = ParsePages(lines, contentStart, title);
            if (pages.Count == 0)
            {
                pages.Add(new Page
                {
                    Id = Guid.NewGuid(),
                    Title = title,
                    TextContent = content.Trim(),
                    ContentType = ContentType.Text,
                    PageNumber = 1
                });
            }

            var deck = new Deck
            {
                Id = DeckFileIdentity.GetId(filePath),
                FileName = fileName,
                Title = title,
                Description = description,
                Tags = tags,
                Difficulty = "lvl0",
                IsPublished = true,
                IsReviewed = true,
                CreatedAt = File.GetCreationTime(filePath),
                PageCount = pages.Count,
                Pages = pages
            };

            foreach (var page in pages)
            {
                page.DeckId = deck.Id;
            }

            return deck;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to parse deck file {FilePath}", filePath);
            return null;
        }
    }

    private static (string? title, string? description, string? tags, int endIndex)? ParseFrontmatter(string[] lines)
    {
        if (lines.Length == 0 || !lines[0].Trim().Equals("---"))
            return null;

        string? title = null;
        string? description = null;
        string? tags = null;
        int i;

        for (i = 1; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed == "---")
                return (title, description, tags, i);

            if (trimmed.StartsWith("title:", StringComparison.OrdinalIgnoreCase))
                title = trimmed[6..].Trim();
            else if (trimmed.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                description = trimmed[12..].Trim();
            else if (trimmed.StartsWith("tags:", StringComparison.OrdinalIgnoreCase))
                tags = trimmed[5..].Trim();
        }

        return (title, description, tags, i);
    }

    private static List<Page> ParsePages(string[] lines, int startLine, string fallbackTitle)
    {
        var pages = new List<Page>();
        var currentSection = "";
        var currentTitle = "";
        var currentContent = new StringBuilder();
        bool inPage = false;
        int pageNum = 1;
        bool hasH2 = false;

        for (int i = startLine; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"^(?:##|\*\*)\s+"))
            {
                hasH2 = true;
                break;
            }
        }

        for (int i = startLine; i < lines.Length; i++)
        {
            var line = lines[i];
            var h1Match = Regex.Match(line, @"^[#*]\s+(.+)$");
            var h2Match = Regex.Match(line, @"^(?:##|\*\*)\s+(.+)$");

            if (h2Match.Success)
            {
                if (inPage && currentTitle.Length > 0)
                {
                    pages.Add(new Page
                    {
                        Id = Guid.NewGuid(),
                        SectionTitle = currentSection,
                        Title = currentTitle,
                        TextContent = currentContent.ToString().Trim(),
                        ContentType = ContentType.Text,
                        PageNumber = pageNum++
                    });
                }
                currentTitle = h2Match.Groups[1].Value.Trim();
                currentContent.Clear();
                inPage = true;
            }
            else if (h1Match.Success)
            {
                if (!hasH2)
                {
                    if (inPage && currentTitle.Length > 0)
                    {
                        pages.Add(new Page
                        {
                            Id = Guid.NewGuid(),
                            Title = currentTitle,
                            TextContent = currentContent.ToString().Trim(),
                            ContentType = ContentType.Text,
                            PageNumber = pageNum++
                        });
                    }
                    currentTitle = h1Match.Groups[1].Value.Trim();
                    currentContent.Clear();
                    inPage = true;
                }
                else
                {
                    if (inPage && currentTitle.Length > 0)
                    {
                        pages.Add(new Page
                        {
                            Id = Guid.NewGuid(),
                            SectionTitle = currentSection,
                            Title = currentTitle,
                            TextContent = currentContent.ToString().Trim(),
                            ContentType = ContentType.Text,
                            PageNumber = pageNum++
                        });
                    }
                    currentSection = h1Match.Groups[1].Value.Trim();
                    currentTitle = "";
                    currentContent.Clear();
                    inPage = false;
                }
            }
            else if (inPage)
            {
                currentContent.AppendLine(line);
            }
        }

        if (inPage && currentTitle.Length > 0)
        {
            pages.Add(new Page
            {
                Id = Guid.NewGuid(),
                SectionTitle = hasH2 ? currentSection : null,
                Title = currentTitle,
                TextContent = currentContent.ToString().Trim(),
                ContentType = ContentType.Text,
                PageNumber = pageNum++
            });
        }

        return pages;
    }

}
