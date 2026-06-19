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
        if (page == null)
        {
            return EmptyHtml();
        }

        var body = new StringBuilder();

        var textContent = page.TextContent ?? string.Empty;
        var lines = textContent.Split('\n');

        var inParagraph = false;
        var emptyLineCount = 0;
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

            if (isOrgFile && TryRenderOrgBlock(lines, ref i, out var orgBlockHtml))
            {
                CloseParagraph(body, ref inParagraph);
                body.AppendLine(orgBlockHtml);
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
                if (emptyLineCount > 0)
                {
                    body.AppendLine("<br>");
                }

                emptyLineCount++;
                i++;
                continue;
            }

            emptyLineCount = 0;

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

            if (TryRenderBlockquote(lines, ref i, isOrgFile, out var quoteHtml, imageDir, accumulatedImagePaths))
            {
                CloseParagraph(body, ref inParagraph);
                body.AppendLine(quoteHtml);
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

    // Detects ``` fences (md) or #+BEGIN_SRC / #+END_SRC (org), collects inner lines as <pre><code>
    private static bool TryStartCodeBlock(string[] lines, ref int index, bool isOrgFile, out string html)
    {
        html = string.Empty;

        var line = lines[index].TrimEnd('\r');

        if (isOrgFile)
        {
            var orgMatch = Regex.Match(line, @"^#\+BEGIN_SRC\s*(\w*)$", RegexOptions.IgnoreCase);
            if (!orgMatch.Success)
            {
                return false;
            }

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

            if (!foundEnd)
            {
                return false;
            }

            var escapedCode = EscapeHtml(code.ToString().TrimEnd('\n', '\r'));
            var langClass = string.IsNullOrEmpty(language) ? string.Empty : $" class=\"language-{EscapeHtml(language)}\"";
            html = $"<pre><code{langClass}>{escapedCode}</code></pre>";
            return true;
        }

        var match = Regex.Match(line, @"^```(\w*)$");
        if (!match.Success)
        {
            return false;
        }

        var language2 = match.Groups[1].Value;
        var code2 = new StringBuilder();
        var startIndex2 = index + 1;

        for (var j = startIndex2; j < lines.Length; j++)
        {
            if (lines[j].TrimEnd('\r').Trim() == "```")
            {
                index = j;
                var escapedCode = EscapeHtml(code2.ToString().TrimEnd('\n', '\r'));
                var langClass = string.IsNullOrEmpty(language2) ? string.Empty : $" class=\"language-{EscapeHtml(language2)}\"";
                html = $"<pre><code{langClass}>{escapedCode}</code></pre>";
                return true;
            }

            code2.AppendLine(lines[j]);
        }

        return false;
    }

    // Gathers | -delimited rows, separates header from body via ---+--- separator, renders <table>
    private static bool TryRenderTable(string[] lines, ref int index, bool isOrgFile, out string html, string? imageDir = null, List<string>? accumulatedImagePaths = null)
    {
        html = string.Empty;
        var start = index;
        var end = start;
        while (end < lines.Length && lines[end].TrimEnd('\r').Trim().StartsWith('|'))
        {
            end++;
        }

        if (end - start < 1)
        {
            return false;
        }

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
            if (IsTableSeparator(rows[r]))
            {
                continue;
            }

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

    // Matches # / ## (md) or * / ** (org) heading markers, renders <h1>–<h6> with visible marker span
    private static bool TryRenderHeading(string line, out string html, bool isOrgFile = false, string? imageDir = null, List<string>? accumulatedImagePaths = null)
    {
        html = string.Empty;

        string marker;
        int level;
        string content;

        if (isOrgFile)
        {
            var match = Regex.Match(line, @"^(\*{1,6})\s+(.+)$");
            if (!match.Success)
            {
                return false;
            }

            marker = match.Groups[1].Value;
            level = Math.Min(marker.Length, 6);
            content = match.Groups[2].Value;
        }
        else
        {
            var match = Regex.Match(line, @"^(#{1,6})\s+(.+)$");
            if (match.Success)
            {
                marker = match.Groups[1].Value;
                level = marker.Length;
                content = match.Groups[2].Value;
            }
            else
            {
                match = Regex.Match(line, @"^(\*{1,2})\s+(.+)$");
                if (!match.Success)
                {
                    return false;
                }

                marker = match.Groups[1].Value;
                level = marker.Length;
                content = match.Groups[2].Value;
            }
        }

        var renderedContent = RenderInline(content, isOrgFile, imageDir, accumulatedImagePaths);
        html = $"<h{level}><span class=\"heading-marker\">{EscapeHtml(marker)}</span> {renderedContent}</h{level}>";
        return true;
    }

    private static string RenderInline(string text, bool isOrgFile, string? imageDir = null, List<string>? accumulatedImagePaths = null)
    {
        var renderer = isOrgFile
            ? (IInlineRenderer)new OrgInlineRenderer()
            : new MarkdownInlineRenderer();
        return renderer.RenderInline(text, imageDir, accumulatedImagePaths);
    }

    internal static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    internal static string RenderImageTag(string alt, string rawPath, string? imageDir, List<string>? accumulatedImagePaths)
    {
        if (rawPath.StartsWith("http://") || rawPath.StartsWith("https://"))
        {
            return $"<a data-href=\"{rawPath}\" rel=\"noopener\">{EscapeHtml(alt)}</a>";
        }

        if (imageDir == null)
        {
            return $"<span class=\"image-error\">image folder not configured: {EscapeHtml(rawPath)}</span>";
        }

        var fullPath = Path.GetFullPath(Path.Combine(imageDir, rawPath));

        // Must stay within $DECKS_DIR
        if (!fullPath.StartsWith(Path.GetFullPath(imageDir), StringComparison.Ordinal))
        {
            return $"<span class=\"image-error\">image path mis-referenced: {EscapeHtml(rawPath)}</span>";
        }

        if (!File.Exists(fullPath))
        {
            return $"<span class=\"image-error\">image not found: {EscapeHtml(rawPath)}</span>";
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
                _ => "application/octet-stream",
            };
            accumulatedImagePaths?.Add(fullPath);
            var encodedPath = Convert.ToBase64String(Encoding.UTF8.GetBytes(fullPath));
            return $"<a href=\"http://img.local/{encodedPath}\"><img class=\"inline-image\" src=\"data:{mime};base64,{b64}\" alt=\"{EscapeHtml(alt)}\" /></a>";
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException or PathTooLongException)
        {
            return $"<span class=\"image-error\">image not found: {EscapeHtml(rawPath)}</span>";
        }
    }

    private static bool TryRenderList(string[] lines, ref int index, bool isOrgFile, out string html, string? imageDir = null, List<string>? accumulatedImagePaths = null)
    {
        html = string.Empty;
        var firstLine = lines[index].TrimEnd('\r');
        var firstTrimmed = firstLine.Trim();

        string? listTag;
        if (Regex.IsMatch(firstTrimmed, @"^[-+*]\s"))
        {
            listTag = "ul";
        }
        else if (Regex.IsMatch(firstTrimmed, @"^\d+[.)]\s"))
        {
            listTag = "ol";
        }
        else
        {
            return false;
        }

        var sb = new StringBuilder();
        var i = index;

        // Stack of (listTag, indent) — tracks nesting depth for sub-lists
        var levels = new List<(string tag, int indent)>();
        levels.Add((listTag, firstLine.Length - firstLine.TrimStart().Length));
        sb.Append($"<{listTag}>");

        while (i < lines.Length)
        {
            var rawLine = lines[i].TrimEnd('\r');
            var lineTrimmed = rawLine.Trim();

            if (string.IsNullOrEmpty(lineTrimmed))
            {
                sb.AppendLine("<br>");
                i++;
                continue;
            }

            bool isUl = Regex.IsMatch(lineTrimmed, @"^[-+*]\s");
            bool isOl = Regex.IsMatch(lineTrimmed, @"^\d+[.)]\s");
            if (!isUl && !isOl)
            {
                break;
            }

            var lineIndent = rawLine.Length - rawLine.TrimStart().Length;
            var lineListTag = isUl ? "ul" : "ol";

            // Find parent level (closest indent <= current)
            var parentIdx = levels.Count - 1;
            while (parentIdx >= 0 && levels[parentIdx].indent > lineIndent)
            {
                parentIdx--;
            }

            if (parentIdx < 0)
            {
                parentIdx = 0;
            }

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

            // Render [ ], [x], or [-] as styled todo-checkbox spans
            var checkboxHtml = string.Empty;
            var cbMatch = Regex.Match(content, @"^\[( |x|X|-)\]\s*");
            if (cbMatch.Success)
            {
                checkboxHtml = cbMatch.Groups[1].Value switch
                {
                    " " => "<span class=\"todo-unchecked\"></span>",
                    "x" or "X" => "<span class=\"todo-checked\"></span>",
                    "-" => "<span class=\"todo-inprogress\"></span>",
                    _ => string.Empty,
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

    // Lines starting with > are grouped into <blockquote><p>…</p></blockquote>
    private static bool TryRenderBlockquote(string[] lines, ref int index, bool isOrgFile, out string html, string? imageDir = null, List<string>? accumulatedImagePaths = null)
    {
        html = string.Empty;

        var line = lines[index].TrimEnd('\r');
        var trimmed = line.Trim();

        if (!trimmed.StartsWith('>'))
        {
            return false;
        }

        var sb = new StringBuilder();
        sb.AppendLine("<blockquote>");

        var i = index;
        while (i < lines.Length)
        {
            var rawLine = lines[i].TrimEnd('\r');
            var trimmedLine = rawLine.Trim();

            if (!trimmedLine.StartsWith('>'))
            {
                if (string.IsNullOrEmpty(trimmedLine))
                {
                    i++;
                    break;
                }

                break;
            }

            var content = trimmedLine.Substring(1).TrimStart();
            sb.AppendLine($"<p>{RenderInline(content, isOrgFile, imageDir, accumulatedImagePaths)}</p>");
            i++;
        }

        sb.AppendLine("</blockquote>");
        html = sb.ToString();
        index = i - 1;
        return true;
    }

    private static bool TryRenderOrgBlock(string[] lines, ref int index, out string html)
    {
        html = string.Empty;

        var line = lines[index].TrimEnd('\r');
        var trimmed = line.Trim();

        var match = Regex.Match(trimmed, @"^#\+BEGIN_(\w+)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        var blockType = match.Groups[1].Value;
        if (string.Equals(blockType, "SRC", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var content = new StringBuilder();
        var startIndex = index + 1;
        var foundEnd = false;

        for (var j = startIndex; j < lines.Length; j++)
        {
            if (Regex.IsMatch(lines[j].TrimEnd('\r').Trim(), @"^#\+END_" + blockType + "$", RegexOptions.IgnoreCase))
            {
                foundEnd = true;
                index = j;
                break;
            }

            content.AppendLine(EscapeHtml(lines[j].TrimEnd('\r')));
        }

        if (!foundEnd)
        {
            return false;
        }

        var escapedContent = content.ToString().TrimEnd('\n', '\r');
        html = $"<pre class=\"org-block\">{escapedContent}</pre>";
        return true;
    }

    internal static string PreserveLeadingWhitespace(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var i = 0;
        while (i < text.Length && (text[i] == ' ' || text[i] == '\t'))
        {
            i++;
        }

        if (i == 0)
        {
            return text;
        }

        var prefix = new StringBuilder();
        for (var j = 0; j < i; j++)
        {
            prefix.Append(text[j] == '\t' ? "&nbsp;&nbsp;&nbsp;&nbsp;" : "&nbsp;");
        }

        return prefix + text.Substring(i);
    }

    private static string EmptyHtml()
    {
        return WrapInHtml(string.Empty);
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
        tab-size: 4;
    }
    .heading-marker { opacity: 0.6; user-select: none; }
    h1 { font-size: 1.6em; font-weight: 700; color: #FBBF24; margin: 0 0 12px 0; padding-bottom: 8px; border-bottom: 1px solid #334155; }
    h2 { font-size: 1.35em; font-weight: 600; color: #F59E0B; margin: 0 0 12px 0; }
    h3 { font-size: 1.2em; font-weight: 600; color: #10B981; margin: 0 0 10px 0; }
    h4 { font-size: 1.1em; font-weight: 600; color: #3B82F6; margin: 0 0 8px 0; }
    h5 { font-size: 1.0em; font-weight: 600; color: #8B5CF6; margin: 0 0 8px 0; }
    h6 { font-size: 0.9em; font-weight: 600; color: #EC4899; margin: 0 0 6px 0; }
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
    blockquote {
        margin: 0 0 12px 0;
        padding: 8px 16px;
        border-left: 4px solid #60A5FA;
        background: #334155;
        border-radius: 0 4px 4px 0;
    }
    blockquote p {
        margin: 0 0 4px 0;
    }
    <!--HIGHLIGHT_CSS-->
</style>
</head>
<body>
{{bodyContent}}
<script>/* HIGHLIGHT_JS */</script>
<script>hljs.highlightAll();</script>
<script>(function(){var kr=document.createElement('iframe');kr.style.cssText='display:none!important;width:0!important;height:0!important;border:none!important;position:fixed!important';document.body.appendChild(kr);document.addEventListener('keydown',function(e){var k=e.key,m='',h=false;if(e.ctrlKey)m+='C';if(e.shiftKey)m+='S';if(e.altKey)m+='A';switch(k){case'n':case'N':case'p':case'P':case'j':case'J':case'k':case'K':case'h':case'H':case'l':case'L':case'q':case'Q':case'd':case'D':case'e':case'E':case'i':case'I':case'g':case'G':case'Escape':case'?':case'/':case'Enter':h=true;break;case',':case'=':case'-':case'+':case'_':case'0':case')':if(e.ctrlKey)h=true;break;}if(!h)return;e.preventDefault();e.stopPropagation();kr.src='http://key.local/'+encodeURIComponent(k)+'/'+m+'/'+Date.now();},true);})();</script>
<script>(function(){var lr=document.createElement('iframe');lr.style.cssText='display:none!important;width:0!important;height:0!important;border:none!important;position:fixed!important';document.body.appendChild(lr);document.addEventListener('click',function(e){var t=e.target.closest('a');if(!t)return;var h=t.getAttribute('data-href');if(!h)return;e.preventDefault();e.stopPropagation();lr.src='http://openurl.local/'+encodeURIComponent(h)+'/'+Date.now();},true);})();</script>
</body>
</html>
""";
    }
}
