using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public static class HtmlContentBuilder
{
    public static string Build(Page? page, bool isOrgFile, string? imageDir = null, List<string>? accumulatedImagePaths = null)
    {
        if (page == null) return EmptyHtml();

        var body = new StringBuilder();

        body.AppendLine($"<h2>{RenderInline(page.Title ?? "", isOrgFile, imageDir, accumulatedImagePaths)}</h2>");
        body.AppendLine("<hr>");

        var textContent = page.TextContent ?? "";
        var lines = textContent.Split('\n');

        var inParagraph = false;
        var i = 0;

        while (i < lines.Length)
        {
            var rawLine = lines[i].TrimEnd('\r');
            var trimmed = rawLine.Trim();

            if (TryStartCodeBlock(lines, ref i, isOrgFile, out var codeBlockHtml))
            {
                CloseParagraph(body, ref inParagraph);
                body.AppendLine(codeBlockHtml);
                i++;
                continue;
            }

            if (trimmed.StartsWith('|'))
            {
                if (TryRenderTable(lines, ref i, isOrgFile, out var tableHtml, imageDir, accumulatedImagePaths))
                {
                    CloseParagraph(body, ref inParagraph);
                    body.AppendLine(tableHtml);
                    i++;
                    continue;
                }
            }

            if (string.IsNullOrEmpty(trimmed))
            {
                CloseParagraph(body, ref inParagraph);
                i++;
                continue;
            }

            if (TryRenderHeading(rawLine, out var headingHtml, isOrgFile, imageDir, accumulatedImagePaths))
            {
                CloseParagraph(body, ref inParagraph);
                body.AppendLine(headingHtml);
                i++;
                continue;
            }

            if (trimmed is "---" or "***" or "___")
            {
                CloseParagraph(body, ref inParagraph);
                body.AppendLine("<hr>");
                i++;
                continue;
            }

            if (TryRenderList(lines, ref i, isOrgFile, out var listHtml, imageDir, accumulatedImagePaths))
            {
                CloseParagraph(body, ref inParagraph);
                body.AppendLine(listHtml);
                i++;
                continue;
            }

            if (!inParagraph)
            {
                body.Append("<p>");
                inParagraph = true;
            }
            else
            {
                body.AppendLine("<br>");
            }

            body.Append(RenderInline(rawLine, isOrgFile, imageDir, accumulatedImagePaths));
            i++;
        }

        CloseParagraph(body, ref inParagraph);

        return WrapInHtml(body.ToString());
    }

    private static bool TryStartCodeBlock(string[] lines, ref int index, bool isOrgFile, out string html)
    {
        html = "";

        var line = lines[index].TrimEnd('\r');

        if (isOrgFile)
        {
            var orgMatch = Regex.Match(line, @"^#\+BEGIN_SRC\s*(\w*)$", RegexOptions.IgnoreCase);
            if (!orgMatch.Success) return false;

            var language = orgMatch.Groups[1].Value;
            var code = new StringBuilder();
            var startIndex = index + 1;
            var foundEnd = false;

            for (var j = startIndex; j < lines.Length; j++)
            {
                if (Regex.IsMatch(lines[j].TrimEnd('\r').Trim(), @"^#\+END_SRC$", RegexOptions.IgnoreCase))
                {
                    foundEnd = true;
                    index = j;
                    break;
                }
                code.AppendLine(lines[j]);
            }

            if (!foundEnd) return false;

            var escapedCode = EscapeHtml(code.ToString().TrimEnd('\n', '\r'));
            var langClass = string.IsNullOrEmpty(language) ? "" : $" class=\"language-{EscapeHtml(language)}\"";
            html = $"<pre><code{langClass}>{escapedCode}</code></pre>";
            return true;
        }

        var match = Regex.Match(line, @"^```(\w*)$");
        if (!match.Success) return false;

        var language2 = match.Groups[1].Value;
        var code2 = new StringBuilder();
        var startIndex2 = index + 1;

        for (var j = startIndex2; j < lines.Length; j++)
        {
            if (lines[j].TrimEnd('\r').Trim() == "```")
            {
                index = j;
                var escapedCode = EscapeHtml(code2.ToString().TrimEnd('\n', '\r'));
                var langClass = string.IsNullOrEmpty(language2) ? "" : $" class=\"language-{EscapeHtml(language2)}\"";
                html = $"<pre><code{langClass}>{escapedCode}</code></pre>";
                return true;
            }
            code2.AppendLine(lines[j]);
        }

        return false;
    }

    private static bool TryRenderTable(string[] lines, ref int index, bool isOrgFile, out string html, string? imageDir = null, List<string>? accumulatedImagePaths = null)
    {
        html = "";
        var start = index;
        var end = start;
        while (end < lines.Length && lines[end].TrimEnd('\r').Trim().StartsWith('|'))
        {
            end++;
        }
        if (end - start < 1) return false;

        var rows = new List<string>();
        for (var r = start; r < end; r++)
        {
            rows.Add(lines[r].TrimEnd('\r').Trim());
        }

        var sepIndex = -1;
        for (var r = 1; r < rows.Count; r++)
        {
            if (IsTableSeparator(rows[r]))
            {
                sepIndex = r;
                break;
            }
        }

        var result = new StringBuilder();
        result.AppendLine("<table>");

        if (sepIndex > 0)
        {
            result.AppendLine("<thead><tr>");
            foreach (var cell in SplitTableCell(rows[0], isOrgFile, imageDir, accumulatedImagePaths))
            {
                result.AppendLine($"<th>{cell}</th>");
            }
            result.AppendLine("</tr></thead>");
        }

        result.AppendLine("<tbody>");
        for (var r = sepIndex > 0 ? sepIndex + 1 : 0; r < rows.Count; r++)
        {
            if (IsTableSeparator(rows[r])) continue;
            result.AppendLine("<tr>");
            foreach (var cell in SplitTableCell(rows[r], isOrgFile, imageDir, accumulatedImagePaths))
            {
                result.AppendLine($"<td>{cell}</td>");
            }
            result.AppendLine("</tr>");
        }
        result.AppendLine("</tbody>");
        result.AppendLine("</table>");

        index = end - 1;
        html = result.ToString();
        return true;
    }

    private static bool IsTableSeparator(string row)
    {
        var inner = row.TrimStart('|').TrimEnd('|').Trim();
        return !string.IsNullOrEmpty(inner) && Regex.IsMatch(inner, @"^[\s\|\-\+\:]+$");
    }

    private static List<string> SplitTableCell(string row, bool isOrgFile, string? imageDir = null, List<string>? accumulatedImagePaths = null)
    {
        var cells = row.Split('|');
        var result = new List<string>();
        for (var i = 1; i < cells.Length - 1; i++)
        {
            result.Add(RenderInline(cells[i].Trim(), isOrgFile, imageDir, accumulatedImagePaths));
        }
        return result;
    }

    private static void CloseParagraph(StringBuilder body, ref bool inParagraph)
    {
        if (inParagraph)
        {
            body.AppendLine("</p>");
            inParagraph = false;
        }
    }

    private static bool TryRenderHeading(string line, out string html, bool isOrgFile = false, string? imageDir = null, List<string>? accumulatedImagePaths = null)
    {
        html = "";

        var h2Match = Regex.Match(line, @"^(?:##|\*\*)\s+(.+)$");
        if (h2Match.Success)
        {
            html = $"<h2>{RenderInline(h2Match.Groups[1].Value, isOrgFile, imageDir, accumulatedImagePaths)}</h2>";
            return true;
        }

        var h1Match = Regex.Match(line, @"^[#*]\s+(.+)$");
        if (h1Match.Success)
        {
            html = $"<h1>{RenderInline(h1Match.Groups[1].Value, isOrgFile, imageDir, accumulatedImagePaths)}</h1>";
            return true;
        }

        return false;
    }

    private static string RenderInline(string text, bool isOrgFile, string? imageDir = null, List<string>? accumulatedImagePaths = null)
    {
        if (isOrgFile) return RenderOrgInline(text, imageDir, accumulatedImagePaths);
        return RenderMarkdownInline(text, imageDir, accumulatedImagePaths);
    }

    private static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    private static string RenderImageTag(string alt, string rawPath, string? imageDir, List<string>? accumulatedImagePaths)
    {
        if (rawPath.StartsWith("http://") || rawPath.StartsWith("https://"))
        {
            return $"<a href=\"{rawPath}\" target=\"_blank\" rel=\"noopener\">{EscapeHtml(alt)}</a>";
        }

        if (rawPath.Contains('/') || rawPath.Contains('\\'))
        {
            return $"<span class=\"image-error\">[not found: {EscapeHtml(rawPath)} || image mis-typed]</span>";
        }

        if (imageDir == null)
            return $"<span class=\"image-error\">[not found: {EscapeHtml(rawPath)} || image mis-typed]</span>";

        var fullPath = Path.Combine(imageDir, rawPath);
        accumulatedImagePaths?.Add(fullPath);

        if (!File.Exists(fullPath))
        {
            return $"<span class=\"image-error\">[not found: {EscapeHtml(rawPath)} || image mis-typed]</span>";
        }

        try
        {
            var bytes = File.ReadAllBytes(fullPath);
            var b64 = Convert.ToBase64String(bytes);
            var ext = Path.GetExtension(fullPath).ToLowerInvariant();
            var mime = ext switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
            var encodedPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(fullPath));
            return $"<a href=\"http://img.local/{encodedPath}\"><img class=\"inline-image\" src=\"data:{mime};base64,{b64}\" alt=\"{EscapeHtml(alt)}\" /></a>";
        }
        catch
        {
            return $"<span class=\"image-error\">[not found: {EscapeHtml(rawPath)} || image mis-typed]</span>";
        }
    }

    private static string RenderOrgInline(string text, string? imageDir = null, List<string>? accumulatedImagePaths = null)
    {
        var result = EscapeHtml(text);

        result = Regex.Replace(result, @"\b(TODO|DONE)\b", m =>
            m.Groups[1].Value switch
            {
                "TODO" => "<span class=\"todo-keyword\">TODO</span>",
                "DONE" => "<span class=\"done-keyword\">DONE</span>",
                _ => m.Value
            });
        result = Regex.Replace(result, @"~([^~]+)~", "<code>$1</code>");
        result = Regex.Replace(result, @"(?<!\*)\*([^*]+)\*(?!\*)", "<strong>$1</strong>");
        result = Regex.Replace(result, @"/([^/]+)/", "<em>$1</em>");
        result = Regex.Replace(result, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\" target=\"_blank\" rel=\"noopener\">$1</a>");

        result = Regex.Replace(result, @"\[\[file:([^\]]+)\]\[([^\]]*)\]\]", m =>
        {
            var path = m.Groups[1].Value;
            var alt = m.Groups[2].Value;
            return RenderImageTag(alt, path, imageDir, accumulatedImagePaths);
        });

        // Obsidian-style image ![[path]]
        result = Regex.Replace(result, @"!\[\[([^\]]+)\]\]", m =>
        {
            return RenderImageTag("", m.Groups[1].Value, imageDir, accumulatedImagePaths);
        });

        // Standard markdown image ![](path)
        result = Regex.Replace(result, @"!\[([^\]]*)\]\(([^)]+)\)", m =>
        {
            return RenderImageTag(m.Groups[1].Value, m.Groups[2].Value, imageDir, accumulatedImagePaths);
        });

        // Wiki-style link [[text]]
        result = Regex.Replace(result, @"\[\[([^\]]+)\]\]", "$1");

        return result;
    }

    private static string RenderMarkdownInline(string text, string? imageDir = null, List<string>? accumulatedImagePaths = null)
    {
        var result = EscapeHtml(text);

        var codeSpans = new List<string>();
        var placeholderIdx = 0;
        result = Regex.Replace(result, @"`([^`]+)`", m =>
        {
            codeSpans.Add(m.Groups[1].Value);
            return $"%%%CODE_{placeholderIdx++}%%%";
        });

        result = Regex.Replace(result, @"\b(TODO|DONE)\b", m =>
            m.Groups[1].Value switch
            {
                "TODO" => "<span class=\"todo-keyword\">TODO</span>",
                "DONE" => "<span class=\"done-keyword\">DONE</span>",
                _ => m.Value
            });
        result = Regex.Replace(result, @"\*\*([^*]+)\*\*", "<strong>$1</strong>");
        result = Regex.Replace(result, @"(?<!\w)\*([^*]+)\*(?!\w)", "<em>$1</em>");
        result = Regex.Replace(result, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\" target=\"_blank\" rel=\"noopener\">$1</a>");

        // Obsidian-style image ![[path]]
        result = Regex.Replace(result, @"!\[\[([^\]]+)\]\]", m =>
        {
            return RenderImageTag("", m.Groups[1].Value, imageDir, accumulatedImagePaths);
        });

        // Standard markdown image ![](path)
        result = Regex.Replace(result, @"!\[([^\]]*)\]\(([^)]+)\)", m =>
        {
            return RenderImageTag(m.Groups[1].Value, m.Groups[2].Value, imageDir, accumulatedImagePaths);
        });

        // Wiki-style link [[text]]
        result = Regex.Replace(result, @"\[\[([^\]]+)\]\]", "$1");

        for (var i = 0; i < codeSpans.Count; i++)
        {
            result = result.Replace($"%%%CODE_{i}%%%", $"<code>{codeSpans[i]}</code>");
        }

        return result;
    }

    private static bool TryRenderList(string[] lines, ref int index, bool isOrgFile, out string html, string? imageDir = null, List<string>? accumulatedImagePaths = null)
    {
        html = "";
        var firstLine = lines[index].TrimEnd('\r');
        var firstTrimmed = firstLine.Trim();

        string? listTag;
        if (Regex.IsMatch(firstTrimmed, @"^[-+*]\s"))
            listTag = "ul";
        else if (Regex.IsMatch(firstTrimmed, @"^\d+[.)]\s"))
            listTag = "ol";
        else
            return false;

        var sb = new StringBuilder();
        var i = index;

        // Stack of (listTag, indent)
        var levels = new List<(string tag, int indent)>();
        levels.Add((listTag, firstLine.Length - firstLine.TrimStart().Length));
        sb.Append($"<{listTag}>");

        while (i < lines.Length)
        {
            var rawLine = lines[i].TrimEnd('\r');
            var lineTrimmed = rawLine.Trim();

            if (string.IsNullOrEmpty(lineTrimmed))
            {
                i++;
                continue;
            }

            bool isUl = Regex.IsMatch(lineTrimmed, @"^[-+*]\s");
            bool isOl = Regex.IsMatch(lineTrimmed, @"^\d+[.)]\s");
            if (!isUl && !isOl)
                break;

            var lineIndent = rawLine.Length - rawLine.TrimStart().Length;
            var lineListTag = isUl ? "ul" : "ol";

            // Find parent level (closest indent <= current)
            var parentIdx = levels.Count - 1;
            while (parentIdx >= 0 && levels[parentIdx].indent > lineIndent)
                parentIdx--;
            if (parentIdx < 0) parentIdx = 0;

            // Close over-indented levels
            while (levels.Count > parentIdx + 1)
            {
                sb.Append("</li>");
                sb.Append($"</{levels[^1].tag}>");
                levels.RemoveAt(levels.Count - 1);
            }

            if (levels.Count > 0 && levels[^1].indent == lineIndent)
            {
                // Same level → close previous item
                sb.Append("</li>");
            }
            else if (lineIndent > levels[^1].indent)
            {
                // Deeper indent → nested list inside current item
                sb.Append($"<{lineListTag}>");
                levels.Add((lineListTag, lineIndent));
            }

            // Extract content after list marker
            var contentStart = isUl ? 2 : Regex.Match(lineTrimmed, @"^\d+[.)]\s").Length;
            var content = lineTrimmed.Substring(contentStart);

            // Handle checkbox
            var checkboxHtml = "";
            var cbMatch = Regex.Match(content, @"^\[( |x|X|-)\]\s*");
            if (cbMatch.Success)
            {
                checkboxHtml = cbMatch.Groups[1].Value switch
                {
                    " " => "<span class=\"todo-unchecked\"></span>",
                    "x" or "X" => "<span class=\"todo-checked\"></span>",
                    "-" => "<span class=\"todo-inprogress\"></span>",
                    _ => ""
                };
                content = content.Substring(cbMatch.Length);
            }

            var rendered = RenderInline(content, isOrgFile, imageDir, accumulatedImagePaths);
            sb.Append($"<li>{checkboxHtml}{rendered}");
            i++;
        }

        // Close all remaining levels
        for (var l = levels.Count - 1; l >= 0; l--)
        {
            sb.Append("</li>");
            sb.Append($"</{levels[l].tag}>");
        }

        html = sb.ToString();
        index = i - 1;
        return true;
    }

    private static string EmptyHtml()
    {
        return WrapInHtml("");
    }

    private static string WrapInHtml(string bodyContent)
    {
        return $$"""
<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<style>
    * { margin: 0; padding: 0; box-sizing: border-box; }
    html { overflow-x: auto; }
    ::selection { background: #2563EB; color: #FFFFFF; }
    body {
        font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
        font-size: 16px; line-height: 1.7;
        color: #E2E8F0; background: #1E293B;
        padding: 24px; max-width: none; min-width: calc(100vw + 200px); overflow-x: auto;
    }
    h1 { font-size: 1.5em; font-weight: 700; color: #F1F5F9; margin: 0 0 12px 0; padding-bottom: 8px; border-bottom: 1px solid #334155; }
    h2 { font-size: 1.25em; font-weight: 600; color: #F1F5F9; margin: 0 0 12px 0; }
    hr { border: none; border-top: 1px solid #334155; margin: 16px 0; }
    p { margin: 0 0 12px 0; }
    code { font-family: 'JetBrains Mono', 'Fira Code', Consolas, monospace; font-size: 0.9em; background: #78350F; color: #FBBF24; padding: 2px 6px; border-radius: 4px; }
    a { color: #60A5FA; text-decoration: underline; }
    a:hover { color: #93C5FD; }
    strong { font-weight: 700; color: #F8FAFC; }
    em { font-style: italic; color: #CBD5E1; }
    ul, ol { margin: 0 0 12px 0; padding-left: 24px; }
    li { margin: 4px 0; }
    pre { background: #282C34; border-radius: 8px; padding: 16px; overflow-x: auto; margin: 0 0 12px 0; white-space: pre; }
    pre code { background: none; color: #ABB2BF; padding: 0; border-radius: 0; }
    table { border-collapse: collapse; width: 100%; margin: 0 0 12px 0; }
    th, td { border: 1px solid #475569; padding: 8px 12px; text-align: left; }
    th { background: #334155; color: #F1F5F9; font-weight: 600; }
    td { color: #E2E8F0; }
    .inline-image { max-width: 260px; max-height: 180px; border-radius: 8px; cursor: pointer; }
    .inline-image:hover { opacity: 0.85; }
    .image-error { display: inline-block; border: 1px solid #EF4444; color: #EF4444; padding: 4px 8px; border-radius: 4px; font-size: 0.85em; }
    .todo-unchecked, .todo-checked, .todo-inprogress {
        display: inline-flex; align-items: center; justify-content: center;
        width: 1.3em; height: 1.3em; margin-right: 6px;
        vertical-align: middle; flex-shrink: 0;
    }
    .todo-unchecked { border: 2px solid #64748B; border-radius: 3px; background: transparent; }
    .todo-checked { background: #10B981; border-radius: 3px; }
    .todo-checked::after { content: "\2713"; color: #fff; font-weight: bold; font-size: 0.95em; }
    .todo-inprogress { border: 2px solid #F59E0B; border-radius: 3px; background: transparent; }
    .todo-inprogress::after { content: "\2212"; color: #F59E0B; font-weight: bold; font-size: 1.2em; }
    .todo-keyword { color: #10B981; font-weight: 600; }
    .done-keyword { color: #94A3B8; }
    <!--HIGHLIGHT_CSS-->
</style>
</head>
<body>
{{bodyContent}}
<script>/* HIGHLIGHT_JS */</script>
<script>hljs.highlightAll();</script>
<script>(function(){var kr=document.createElement('iframe');kr.style.cssText='display:none!important;width:0!important;height:0!important;border:none!important;position:fixed!important';document.body.appendChild(kr);document.addEventListener('keydown',function(e){var k=e.key,m='',h=false;if(e.ctrlKey)m+='C';if(e.shiftKey)m+='S';if(e.altKey)m+='A';switch(k){case'n':case'N':case'p':case'P':case'j':case'J':case'k':case'K':case'h':case'H':case'l':case'L':case'q':case'Q':case'd':case'D':case'e':case'E':case'i':case'I':case'g':case'G':case'Escape':case'?':case'/':case'Enter':h=true;break;case',':case'=':case'-':if(e.ctrlKey)h=true;break;}if(!h)return;e.preventDefault();e.stopPropagation();kr.src='http://key.local/'+encodeURIComponent(k)+'/'+m+'/'+Date.now();},true);})();</script>
</body>
</html>
""";
    }
}
