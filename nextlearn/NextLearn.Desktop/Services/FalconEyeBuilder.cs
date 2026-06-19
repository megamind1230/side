using System.Collections.Generic;
using System.Linq;
using System.Text;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public static class FalconEyeBuilder
{
    public static string BuildTocHtml(List<Page> pages, string deckTitle, string? fontFamily = null)
    {
        if (pages == null)
        {
            throw new System.ArgumentNullException(nameof(pages));
        }

        if (deckTitle == null)
        {
            throw new System.ArgumentNullException(nameof(deckTitle));
        }

        fontFamily ??= "Inter";
        var body = new StringBuilder();

        body.AppendLine($"<h1>👁 Falcon Eye — {HtmlContentBuilder.EscapeHtml(deckTitle)}</h1>");
        body.AppendLine("<div class=\"toc-subtitle\">Table of Contents</div>");
        body.AppendLine("<ol class=\"falcon-eye-list\">");

        var contentPages = pages
            .Where(p => !p.IsTocPage && !p.IsPreHeadingPage)
            .OrderBy(p => p.PageNumber)
            .ToList();

        string? lastSection = null;

        foreach (var page in contentPages)
        {
            var sectionTitle = page.SectionTitle ?? string.Empty;
            var title = page.Title ?? string.Empty;
            var hasSubHeading = !string.IsNullOrEmpty(sectionTitle)
                && sectionTitle != title;

            if (hasSubHeading && sectionTitle != lastSection)
            {
                if (lastSection != null)
                {
                    body.AppendLine("</ol></li>");
                }

                lastSection = sectionTitle;
                body.AppendLine($"<li><span class=\"toc-section-title\">{HtmlContentBuilder.EscapeHtml(sectionTitle)}</span><ol class=\"toc-sub-list\">");
                body.AppendLine($"<li><span class=\"toc-heading\">{HtmlContentBuilder.EscapeHtml(title)}</span> <span class=\"toc-page-num\">— Page {page.PageNumber}</span></li>");
            }
            else if (hasSubHeading)
            {
                body.AppendLine($"<li><span class=\"toc-heading\">{HtmlContentBuilder.EscapeHtml(title)}</span> <span class=\"toc-page-num\">— Page {page.PageNumber}</span></li>");
            }
            else
            {
                if (lastSection != null)
                {
                    body.AppendLine("</ol></li>");
                    lastSection = null;
                }

                body.AppendLine($"<li><span class=\"toc-heading\">{HtmlContentBuilder.EscapeHtml(title)}</span> <span class=\"toc-page-num\">— Page {page.PageNumber}</span></li>");
            }
        }

        if (lastSection != null)
        {
            body.AppendLine("</ol></li>");
        }

        body.AppendLine("</ol>");

        return WrapInHtml(body.ToString(), fontFamily);
    }

    private static string WrapInHtml(string bodyContent, string fontFamily)
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
        font-family: '{{fontFamily}}', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
        font-size: 16px; line-height: 1.7;
        color: #E2E8F0; background: #1E293B;
        padding: 24px; max-width: none; min-width: calc(100vw + 200px); overflow-x: auto;
    }
    h1 { font-size: 1.6em; font-weight: 700; color: #FBBF24; margin: 0 0 4px 0; padding-bottom: 8px; border-bottom: 1px solid #334155; }
    .toc-subtitle { color: #94A3B8; font-size: 0.95em; margin-bottom: 20px; }
    .falcon-eye-list { padding-left: 24px; }
    .toc-section-title { font-weight: 600; color: #F59E0B; font-size: 1.1em; }
    .toc-sub-list { padding-left: 24px; }
    .toc-heading { color: #E2E8F0; }
    .toc-page-num { color: #94A3B8; font-size: 0.85em; }
    li { margin: 4px 0; }
</style>
</head>
<body>
{{bodyContent}}
<script>(function(){var kr=document.createElement('iframe');kr.style.cssText='display:none!important;width:0!important;height:0!important;border:none!important;position:fixed!important';document.body.appendChild(kr);document.addEventListener('keydown',function(e){var k=e.key,m='',h=false;if(e.ctrlKey)m+='C';if(e.shiftKey)m+='S';if(e.altKey)m+='A';switch(k){case'n':case'N':case'p':case'P':case'j':case'J':case'k':case'K':case'h':case'H':case'l':case'L':case'q':case'Q':case'd':case'D':case'e':case'E':case'i':case'I':case'g':case'G':case'Escape':case'?':case'/':case'Enter':h=true;break;case',':case'=':case'-':case'+':case'_':case'0':case')':if(e.ctrlKey)h=true;break;}if(!h)return;e.preventDefault();e.stopPropagation();kr.src='http://key.local/'+encodeURIComponent(k)+'/'+m+'/'+Date.now();},true);})();</script>
<script>(function(){var lr=document.createElement('iframe');lr.style.cssText='display:none!important;width:0!important;height:0!important;border:none!important;position:fixed!important';document.body.appendChild(lr);document.addEventListener('click',function(e){var t=e.target.closest('a');if(!t)return;var h=t.getAttribute('data-href');if(!h)return;e.preventDefault();e.stopPropagation();lr.src='http://openurl.local/'+encodeURIComponent(h)+'/'+Date.now();},true);})();</script>
</body>
</html>
""";
    }
}
