using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public static class HtmlContentBuilder
{
    public static string Build(Page? page, bool isOrgFile)
    {
        if (page == null) return EmptyHtml();

        var body = new StringBuilder();

        body.AppendLine($"<h2>{RenderInline(page.Title ?? "", isOrgFile)}</h2>");
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
                if (TryRenderTable(lines, ref i, isOrgFile, out var tableHtml))
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

            if (TryRenderHeading(rawLine, out var headingHtml))
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

            if (!inParagraph)
            {
                body.Append("<p>");
                inParagraph = true;
            }
            else
            {
                body.AppendLine("<br>");
            }

            body.Append(RenderInline(rawLine, isOrgFile));
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

    private static bool TryRenderTable(string[] lines, ref int index, bool isOrgFile, out string html)
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
            foreach (var cell in SplitTableCell(rows[0], isOrgFile))
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
            foreach (var cell in SplitTableCell(rows[r], isOrgFile))
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

    private static List<string> SplitTableCell(string row, bool isOrgFile)
    {
        var cells = row.Split('|');
        var result = new List<string>();
        for (var i = 1; i < cells.Length - 1; i++)
        {
            result.Add(RenderInline(cells[i].Trim(), isOrgFile));
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

    private static bool TryRenderHeading(string line, out string html)
    {
        html = "";

        var h2Match = Regex.Match(line, @"^(?:##|\*\*)\s+(.+)$");
        if (h2Match.Success)
        {
            html = $"<h2>{EscapeHtml(h2Match.Groups[1].Value)}</h2>";
            return true;
        }

        var h1Match = Regex.Match(line, @"^[#*]\s+(.+)$");
        if (h1Match.Success)
        {
            html = $"<h1>{EscapeHtml(h1Match.Groups[1].Value)}</h1>";
            return true;
        }

        return false;
    }

    private static string RenderInline(string text, bool isOrgFile)
    {
        if (isOrgFile) return RenderOrgInline(text);
        return RenderMarkdownInline(text);
    }

    private static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

    private static string RenderOrgInline(string text)
    {
        var result = EscapeHtml(text);

        result = Regex.Replace(result, @"~([^~]+)~", "<code>$1</code>");
        result = Regex.Replace(result, @"(?<!\*)\*([^*]+)\*(?!\*)", "<strong>$1</strong>");
        result = Regex.Replace(result, @"/([^/]+)/", "<em>$1</em>");
        result = Regex.Replace(result, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\" target=\"_blank\" rel=\"noopener\">$1</a>");

        return result;
    }

    private static string RenderMarkdownInline(string text)
    {
        var result = EscapeHtml(text);

        var codeSpans = new List<string>();
        var placeholderIdx = 0;
        result = Regex.Replace(result, @"`([^`]+)`", m =>
        {
            codeSpans.Add(m.Groups[1].Value);
            return $"%%%CODE_{placeholderIdx++}%%%";
        });

        result = Regex.Replace(result, @"\*\*([^*]+)\*\*", "<strong>$1</strong>");
        result = Regex.Replace(result, @"(?<!\w)\*([^*]+)\*(?!\w)", "<em>$1</em>");
        result = Regex.Replace(result, @"\[([^\]]+)\]\(([^)]+)\)", "<a href=\"$2\" target=\"_blank\" rel=\"noopener\">$1</a>");

        for (var i = 0; i < codeSpans.Count; i++)
        {
            result = result.Replace($"%%%CODE_{i}%%%", $"<code>{codeSpans[i]}</code>");
        }

        return result;
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
    <!--HIGHLIGHT_CSS-->
</style>
</head>
<body>
{{bodyContent}}
<script>/* HIGHLIGHT_JS */</script>
<script>hljs.highlightAll();</script>
</body>
</html>
""";
    }
}
