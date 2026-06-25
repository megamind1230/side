using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace NextLearn.Desktop.Services;

public class OrgInlineRenderer : IInlineRenderer
{
    public string RenderInline(string text, string? imageDir = null, List<string>? accumulatedImagePaths = null, IReadOnlyDictionary<string, string>? footnoteDefinitions = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = HtmlContentBuilder.EscapeHtml(text);
        result = HtmlContentBuilder.PreserveLeadingWhitespace(result);

        // Protect org [[...]] and ![[...]] constructs from inline formatting (italic / etc.)
        var orgLinkPlaceholders = new Dictionary<string, string>();
        result = Regex.Replace(result, @"!?\[\[[^\]]+\](?:\[[^\]]*\])?\]", m =>
        {
            var key = $"%%%ORGLINK_{orgLinkPlaceholders.Count}%%%";
            orgLinkPlaceholders[key] = m.Value;
            return key;
        });

        // Protect ![...](...) and [...](...) from inline formatting (italic paths like /temp/)
        result = Regex.Replace(result, @"!?\[[^\]]*\]\([^)]*\)", m =>
        {
            var key = $"%%%ORGLINK_{orgLinkPlaceholders.Count}%%%";
            orgLinkPlaceholders[key] = m.Value;
            return key;
        });

        // Extract math expressions before inline formatting
        var mathExpressions = new List<(string content, string delimiter)>();
        var mathIdx = 0;

        result = Regex.Replace(result, @"\$\$([\s\S]*?)\$\$", m =>
        {
            mathExpressions.Add((m.Groups[1].Value, "$$"));
            return $"%%%MATH_{mathIdx++}%%%";
        });

        result = Regex.Replace(result, @"\$([^$\s][^$]*[^$\s]|[^$\s])\$", m =>
        {
            mathExpressions.Add((m.Groups[1].Value, "$"));
            return $"%%%MATH_{mathIdx++}%%%";
        });

        result = Regex.Replace(result, @"\\\[([\s\S]*?)\\\]", m =>
        {
            mathExpressions.Add((m.Groups[1].Value, @"\["));
            return $"%%%MATH_{mathIdx++}%%%";
        });

        result = Regex.Replace(result, @"\\\(([\s\S]*?)\\\)", m =>
        {
            mathExpressions.Add((m.Groups[1].Value, @"\("));
            return $"%%%MATH_{mathIdx++}%%%";
        });

        // Extract ~code~ spans — code takes priority
        var codeSpans = new List<string>();
        var codeIdx = 0;
        result = Regex.Replace(result, @"~([^~]+)~", m =>
        {
            codeSpans.Add(m.Groups[1].Value);
            return $"%%%CODE_{codeIdx++}%%%";
        });

        // Org footnote references [fn:id] — only replace if id exists in definitions
        var footnotePlaceholders = new Dictionary<string, (string id, string rawText)>();
        if (footnoteDefinitions != null)
        {
            result = Regex.Replace(result, @"\[fn:(\w+)\]", m =>
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
        result = Regex.Replace(result, @"/([^/]+)/", "<em>$1</em>");
        result = Regex.Replace(result, @"(?<!\*)\*([^*]+)\*(?!\*)", "<strong>$1</strong>");

        // Restore org link placeholders
        foreach (var (key, value) in orgLinkPlaceholders)
            result = result.Replace(key, value);

        // Standard markdown link [text](url) — only process http/https URLs; skip ![...](...) and [![...](...)](...)
        result = Regex.Replace(result, @"(?<!\!)\[(?!\!)([^\]]+)\]\(([^)]+)\)", m =>
        {
            var url = m.Groups[2].Value;
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                return $"<a data-href=\"{url}\" rel=\"noopener\">{m.Groups[1].Value}</a>";
            }

            return m.Value;
        });

        // Standard markdown image ![](path) with optional title (handle &quot; from EscapeHtml)
        result = Regex.Replace(result, @"!\[([^\]]*)\]\(([^\s)]+)(?:\s+(?:""|&quot;)[^""]*(?:""|&quot;))?\)", m =>
        {
            return HtmlContentBuilder.RenderImageTag(m.Groups[1].Value, m.Groups[2].Value, imageDir, accumulatedImagePaths);
        });

        // [[file:path][alt]] — render as-is (no ! prefix means no image processing)
        result = Regex.Replace(result, @"\[\[file:([^\]]+)\]\[([^\]]*)\]\]", m =>
        {
            return m.Value;
        });

        // Org [[url][text]] links — only http/https; file: images handled by ![[...]] version
        result = Regex.Replace(result, @"\[\[([^\]]+)\]\[([^\]]*)\]\]", m =>
        {
            var url = m.Groups[1].Value;
            var text = m.Groups[2].Value;
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                return $"<a data-href=\"{url}\" rel=\"noopener\">{HtmlContentBuilder.EscapeHtml(text)}</a>";
            }

            return $"[[{HtmlContentBuilder.EscapeHtml(url)}][{HtmlContentBuilder.EscapeHtml(text)}]]";
        });

        // Org [[url]] links — only http/https; file: images handled by ![[...]] version
        result = Regex.Replace(result, @"\[\[([^\]]+)\]\]", m =>
        {
            var url = m.Groups[1].Value;
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                return $"<a data-href=\"{url}\" rel=\"noopener\">{url}</a>";
            }

            return $"[[{HtmlContentBuilder.EscapeHtml(url)}]]";
        });

        // ![[url][text]] — ! prefix: http/https → link, file: → image with alt, else → image
        result = Regex.Replace(result, @"!\[\[([^\]]+)\]\[([^\]]*)\]\]", m =>
        {
            var url = m.Groups[1].Value;
            var text = m.Groups[2].Value;
            if (url.StartsWith("http://") || url.StartsWith("https://"))
            {
                return $"<a data-href=\"{url}\" rel=\"noopener\">{HtmlContentBuilder.EscapeHtml(text)}</a>";
            }

            var path = url.StartsWith("file:") ? url[5..] : url;
            return HtmlContentBuilder.RenderImageTag(text, path, imageDir, accumulatedImagePaths);
        });

        // ![[path]] — ! prefix: http/https → link, file: → strip prefix, else → image
        result = Regex.Replace(result, @"!\[\[([^\]]+)\]\]", m =>
        {
            var content = m.Groups[1].Value;
            if (content.StartsWith("http://") || content.StartsWith("https://"))
            {
                return $"<a data-href=\"{content}\" rel=\"noopener\">{content}</a>";
            }

            var path = content.StartsWith("file:") ? content[5..] : content;
            return HtmlContentBuilder.RenderImageTag(string.Empty, path, imageDir, accumulatedImagePaths);
        });

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

        // Restore code spans
        for (var i = 0; i < codeSpans.Count; i++)
        {
            result = result.Replace($"%%%CODE_{i}%%%", $"<code>{codeSpans[i]}</code>");
        }

        // Restore footnote references with superscript HTML
        foreach (var (key, (id, rawText)) in footnotePlaceholders)
        {
            var escapedTitle = HtmlContentBuilder.EscapeHtml(rawText);
            result = result.Replace(key, $"<sup class=\"footnote-ref\"><a href=\"#fn-{id}\" id=\"fnref-{id}\" title=\"{escapedTitle}\">{id}</a></sup>");
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

        return result;
    }
}
