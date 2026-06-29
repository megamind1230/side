using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NextLearn.Desktop.Services;

public class MarkdownInlineRenderer : IInlineRenderer
{
    public string RenderInline(string text, string? imageDir = null, List<string>? accumulatedImagePaths = null, IReadOnlyDictionary<string, string>? footnoteDefinitions = null, string? decksPath = null, string? currentDir = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = HtmlContentBuilder.EscapeHtml(text);
        result = HtmlContentBuilder.PreserveLeadingWhitespace(result);

        var codeSpans = new List<string>();
        var placeholderIdx = 0;
        result = Regex.Replace(result, @"`([^`]+)`", m =>
        {
            codeSpans.Add(m.Groups[1].Value);
            return $"%%%CODE_{placeholderIdx++}%%%";
        });

        // Extract math expressions — code takes priority (code extracted first)
        var mathExpressions = new List<(string content, string delimiter)>();
        var mathIdx = 0;

        // $$...$$ display math
        result = Regex.Replace(result, @"\$\$([\s\S]*?)\$\$", m =>
        {
            mathExpressions.Add((m.Groups[1].Value, "$$"));
            return $"%%%MATH_{mathIdx++}%%%";
        });

        // $...$ inline math (no space after opening $, no space before closing $)
        result = Regex.Replace(result, @"\$([^$\s][^$]*[^$\s]|[^$\s])\$", m =>
        {
            mathExpressions.Add((m.Groups[1].Value, "$"));
            return $"%%%MATH_{mathIdx++}%%%";
        });

        // \[...\] display math
        result = Regex.Replace(result, @"\\\[([\s\S]*?)\\\]", m =>
        {
            mathExpressions.Add((m.Groups[1].Value, @"\["));
            return $"%%%MATH_{mathIdx++}%%%";
        });

        // \(...\) inline math
        result = Regex.Replace(result, @"\\\(([\s\S]*?)\\\)", m =>
        {
            mathExpressions.Add((m.Groups[1].Value, @"\("));
            return $"%%%MATH_{mathIdx++}%%%";
        });

        // Footnote references [^id] — only replace if id exists in definitions
        var footnotePlaceholders = new Dictionary<string, (string id, string rawText)>();
        if (footnoteDefinitions != null)
        {
            result = Regex.Replace(result, @"\[\^(\w+)\]", m =>
            {
                var id = m.Groups[1].Value;
                if (footnoteDefinitions.TryGetValue(id, out var rawText))
                {
                    var key = $"%%%FN_{id}%%%";
                    footnotePlaceholders[key] = (id, rawText);
                    return key;
                }

                return m.Value;
            });
        }

        result = Regex.Replace(result, @"\b(TODO|DONE)\b", m =>
            m.Groups[1].Value switch
            {
                "TODO" => "<span class=\"todo-keyword\">TODO</span>",
                "DONE" => "<span class=\"done-keyword\">DONE</span>",
                _ => m.Value,
            });
        result = Regex.Replace(result, @"\*\*([^*]+)\*\*", "<strong>$1</strong>");
        result = Regex.Replace(result, @"(?<!\w)\*([^*]+)\*(?!\w)", "<em>$1</em>");

        // ── Bang prefix ! : suppress deck-link → literal text; images still embed ──

        // ![[file:path][text]] — preserve as literal text (suppresses deck-link)
        result = Regex.Replace(result, @"!\[\[file:([^\]]+)\]\[([^\]]*)\]\]", m => m.Value);

        // ![[target|display]] — preserve as literal text
        result = Regex.Replace(result, @"!\[\[([^\]]+)\|([^\]]+)\]\]", m => m.Value);

        // ![[path]] — if deck-link → literal text; if image → embed; else → literal text
        result = Regex.Replace(result, @"!\[\[([^\]]+)\]\]", m =>
        {
            var content = m.Groups[1].Value;
            var path = content.StartsWith("file:") ? content[5..] : content;

            if (HtmlContentBuilder.TryGetDeckLink(path, decksPath, out _, currentDir))
            {
                return m.Value;
            }

            return HtmlContentBuilder.RenderImageTag(string.Empty, path, imageDir, accumulatedImagePaths);
        });

        // ── Non-bang wiki link formats ──

        // [[file:path][text]] — org-style cross-format
        result = Regex.Replace(result, @"\[\[file:([^\]]+)\]\[([^\]]*)\]\]", m =>
        {
            var path = m.Groups[1].Value;
            var text = m.Groups[2].Value;
            if (HtmlContentBuilder.TryGetDeckLink(path, decksPath, out var relPath, currentDir))
            {
                var display = string.IsNullOrEmpty(text) ? relPath : text;
                return $"<a data-decklink=\"{relPath}\" class=\"decklink\">{display}</a>";
            }

            return m.Value;
        });

        // [[target|display]] — Obsidian alias format
        result = Regex.Replace(result, @"\[\[([^\]]+)\|([^\]]+)\]\]", m =>
        {
            var target = m.Groups[1].Value;
            var display = m.Groups[2].Value;
            if (HtmlContentBuilder.TryGetDeckLink(target, decksPath, out var relPath, currentDir))
            {
                return $"<a data-decklink=\"{relPath}\" class=\"decklink\">{display}</a>";
            }

            return m.Value;
        });

        // [[target]] — bare wiki link (benefits from TryGetDeckLink extension fallback)
        result = Regex.Replace(result, @"\[\[([^\]]+)\]\]", m =>
        {
            var content = m.Groups[1].Value;
            if (content.StartsWith("http://") || content.StartsWith("https://"))
            {
                return $"<a data-href=\"{content}\" rel=\"noopener\">{content}</a>";
            }

            if (HtmlContentBuilder.TryGetDeckLink(content, decksPath, out var relPath, currentDir))
            {
                return $"<a data-decklink=\"{relPath}\" class=\"decklink\">{content}</a>";
            }

            return m.Value;
        });

        // ── Standard markdown links ──

        // [text](url) — skip ![...](...) and [![...](...)](...)
        result = Regex.Replace(result, @"(?<!\!)\[(?!\!)([^\]]+)\]\(([^)]+)\)", m =>
        {
            var url = m.Groups[2].Value;
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                return $"<a data-href=\"{url}\" rel=\"noopener\">{m.Groups[1].Value}</a>";
            }

            if (HtmlContentBuilder.TryGetDeckLink(url, decksPath, out var relPath, currentDir))
            {
                return $"<a data-decklink=\"{relPath}\" class=\"decklink\">{m.Groups[1].Value}</a>";
            }

            return m.Value;
        });

        // ![](path) with optional title
        result = Regex.Replace(result, @"!\[([^\]]*)\]\(([^\s)]+)(?:\s+(?:""|&quot;)[^""]*(?:""|&quot;))?\)", m =>
        {
            return HtmlContentBuilder.RenderImageTag(m.Groups[1].Value, m.Groups[2].Value, imageDir, accumulatedImagePaths);
        });

        for (var i = 0; i < codeSpans.Count; i++)
        {
            result = result.Replace($"%%%CODE_{i}%%%", $"<code>{codeSpans[i]}</code>");
        }

        // Restore math expressions with original delimiters (KaTeX auto-render finds them in DOM)
        for (var i = 0; i < mathExpressions.Count; i++)
        {
            var (content, delim) = mathExpressions[i];
            var left = delim;
            var right = delim switch
            {
                "$" => "$",
                "$$" => "$$",
                @"\[" => @"\]",
                @"\(" => @"\)",
                _ => delim,
            };
            result = result.Replace($"%%%MATH_{i}%%%", $"{left}{content}{right}");
        }

        // Bare URL auto-linking (not already inside <a> or href="")
        result = Regex.Replace(result, @"(?<![""=\w])(https?://[^\s<>""'\]\[()]+)", m =>
        {
            var url = m.Groups[1].Value;
            url = Regex.Replace(url, @"[.,;:!?)]+$", string.Empty);
            if (string.IsNullOrEmpty(url))
            {
                return m.Value;
            }

            return $"<a data-href=\"{url}\" rel=\"noopener\">{url}</a>";
        });

        // Restore footnote references with superscript HTML
        foreach (var (key, (id, rawText)) in footnotePlaceholders)
        {
            var escapedTitle = HtmlContentBuilder.EscapeHtml(rawText);
            result = result.Replace(key, $"<sup class=\"footnote-ref\"><a href=\"#fn-{id}\" id=\"fnref-{id}\" title=\"{escapedTitle}\">{id}</a></sup>");
        }

        return result;
    }
}
