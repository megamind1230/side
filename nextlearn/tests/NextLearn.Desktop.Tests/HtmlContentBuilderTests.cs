using FluentAssertions;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;
using Xunit;

namespace NextLearn.Desktop.Tests;

public class HtmlContentBuilderTests : IDisposable
{
    private readonly List<string> _filesToCleanup = [];

    public void Dispose()
    {
        foreach (var path in _filesToCleanup)
        {
            if (File.Exists(path))
                File.Delete(path);
            var dir = Path.GetDirectoryName(path);
            if (dir is not null && Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private static Page Page(string textContent)
    {
        return new Page
        {
            Id = Guid.NewGuid(),
            Title = "Test Page",
            TextContent = textContent,
            ContentType = ContentType.Text,
            PageNumber = 1,
        };
    }

    private static string Body(string html)
    {
        var start = html.IndexOf("<body>", StringComparison.Ordinal);
        var end = html.IndexOf("</body>", StringComparison.Ordinal);
        return start >= 0 && end > start
            ? html[(start + "<body>".Length)..end]
            : html;
    }

    private string CreateImageFile(string dir, string name)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        // Minimal 1x1 red PNG
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==");
        File.WriteAllBytes(path, png);
        _filesToCleanup.Add(path);
        return path;
    }

    [Fact]
    public void Build_MarkdownH1_RendersHeadingWithMarker()
    {
        var html = HtmlContentBuilder.Build(Page("# Heading 1"), isOrgFile: false);

        html.Should().Contain("<h1>");
        html.Should().Contain("<span class=\"heading-marker\">#</span>");
        html.Should().Contain("Heading 1");
    }

    [Fact]
    public void Build_MarkdownH2_RendersH2WithMarker()
    {
        var html = HtmlContentBuilder.Build(Page("## Heading 2"), isOrgFile: false);

        html.Should().Contain("<h2>");
        html.Should().Contain("<span class=\"heading-marker\">##</span>");
    }

    [Fact]
    public void Build_OrgH1_RendersHeadingWithAsteriskMarker()
    {
        var html = HtmlContentBuilder.Build(Page("* Heading 1"), isOrgFile: true);

        html.Should().Contain("<h1>");
        html.Should().Contain("<span class=\"heading-marker\">*</span>");
    }

    [Fact]
    public void Build_OrgH2_RendersH2WithAsteriskMarker()
    {
        var html = HtmlContentBuilder.Build(Page("** Heading 2"), isOrgFile: true);

        html.Should().Contain("<h2>");
        html.Should().Contain("<span class=\"heading-marker\">**</span>");
    }

    [Fact]
    public void Build_BoldText_RendersStrongTag()
    {
        var html = HtmlContentBuilder.Build(Page("This is **bold** text"), isOrgFile: false);

        html.Should().Contain("<strong>bold</strong>");
    }

    [Fact]
    public void Build_ItalicText_RendersEmTag()
    {
        var html = HtmlContentBuilder.Build(Page("This is *italic* text"), isOrgFile: false);

        html.Should().Contain("<em>italic</em>");
    }

    [Fact]
    public void Build_InlineCode_RendersCodeTag()
    {
        var html = HtmlContentBuilder.Build(Page("Use `code` here"), isOrgFile: false);

        html.Should().Contain("<code>code</code>");
    }

    [Fact]
    public void Build_CodeFence_RendersPreCodeWithLanguageClass()
    {
        var content = @"```csharp
var x = 1;
```";
        var html = HtmlContentBuilder.Build(Page(content), isOrgFile: false);

        html.Should().Contain("<pre><code class=\"language-csharp\">");
        html.Should().Contain("var x = 1;");
    }

    [Fact]
    public void Build_CodeFenceNoLanguage_RendersPreCodeWithoutClass()
    {
        var content = @"```
plain code
```";
        var html = HtmlContentBuilder.Build(Page(content), isOrgFile: false);

        html.Should().Contain("<pre><code>");
        html.Should().Contain("plain code");
    }

    [Fact]
    public void Build_OrgSrcBlock_RendersPreCodeWithLanguageClass()
    {
        var content = @"#+BEGIN_SRC python
def hello():
    pass
#+END_SRC";
        var html = HtmlContentBuilder.Build(Page(content), isOrgFile: true);

        html.Should().Contain("<pre><code class=\"language-python\">");
        html.Should().Contain("def hello():");
    }

    [Fact]
    public void Build_ImageNullImageDir_RendersErrorSpan()
    {
        var html = HtmlContentBuilder.Build(Page("![alt](image.png)"), isOrgFile: false);

        html.Should().Contain("image folder not configured");
    }

    [Fact]
    public void Build_ImageWithImageDir_RendersImgTag()
    {
        var imgDir = Path.Combine(Path.GetTempPath(), "NextLearnTests", Guid.NewGuid().ToString());
        CreateImageFile(imgDir, "test.png");

        var html = HtmlContentBuilder.Build(Page("![alt](test.png)"), isOrgFile: false, imgDir);

        html.Should().Contain("<img class=\"inline-image\"");
    }

    [Fact]
    public void Build_ImageNotFound_RendersErrorSpan()
    {
        var imgDir = Path.Combine(Path.GetTempPath(), "NextLearnTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(imgDir);

        var html = HtmlContentBuilder.Build(Page("![alt](nonexistent.png)"), isOrgFile: false, imgDir);

        html.Should().Contain("image not found");
    }

    [Fact]
    public void Build_HttpImage_RendersClickableLink()
    {
        var html = HtmlContentBuilder.Build(Page("![alt](https://example.com/img.png)"), isOrgFile: false);

        html.Should().Contain("<a data-href=\"https://example.com/img.png\"");
    }

    [Fact]
    public void Build_Blockquote_RendersBlockquoteTag()
    {
        var html = HtmlContentBuilder.Build(Page("> This is a quote"), isOrgFile: false);

        html.Should().Contain("<blockquote>");
        html.Should().Contain("This is a quote");
    }

    [Fact]
    public void Build_Table_RendersTableStructure()
    {
        var content = @"| A | B |
|---|---|
| 1 | 2 |";
        var html = HtmlContentBuilder.Build(Page(content), isOrgFile: false);

        var body = Body(html);
        body.Should().Contain("<table>");
        body.Should().Contain("<thead>");
        body.Should().Contain("<th>A</th>");
        body.Should().Contain("<th>B</th>");
        body.Should().Contain("</thead>");
        body.Should().Contain("<tbody>");
        body.Should().Contain("<td>1</td>");
        body.Should().Contain("<td>2</td>");
        body.Should().Contain("</tbody>");
        body.Should().Contain("</table>");
    }

    [Fact]
    public void Build_OrgTableWithoutSeparator_RendersTable()
    {
        var content = @"| One | Two |
| 3 | 4 |";
        var html = HtmlContentBuilder.Build(Page(content), isOrgFile: true);

        var body = Body(html);
        body.Should().Contain("<table>");
        body.Should().Contain("<td>3</td>");
        body.Should().Contain("<td>4</td>");
    }

    [Fact]
    public void Build_OrgBeginEndBlock_RendersOrgBlockContainer()
    {
        var content = @"#+BEGIN_VERSE
Twinkle, twinkle, little star
How I wonder what you are
#+END_VERSE";
        var html = HtmlContentBuilder.Build(Page(content), isOrgFile: true);

        html.Should().Contain("<pre class=\"org-block\">");
        html.Should().Contain("Twinkle, twinkle, little star");
    }

    [Fact]
    public void Build_BoldAndItalicSeparate_RendersBoth()
    {
        var html = HtmlContentBuilder.Build(Page("**bold** and *italic*"), isOrgFile: false);

        html.Should().Contain("<strong>bold</strong>");
        html.Should().Contain("<em>italic</em>");
    }

    [Fact]
    public void Build_CodeInsideBold_ProtectsCode()
    {
        var html = HtmlContentBuilder.Build(Page("**text `code` more**"), isOrgFile: false);

        html.Should().Contain("<strong>text <code>code</code> more</strong>");
    }

    [Fact]
    public void Build_NullPage_ReturnsEmptyHtml()
    {
        var html = HtmlContentBuilder.Build(null, isOrgFile: false);

        var body = Body(html);
        body.Should().NotContain("<p>");
    }

    [Fact]
    public void Build_EmptyTextContent_ReturnsValidHtml()
    {
        var html = HtmlContentBuilder.Build(Page(string.Empty), isOrgFile: false);

        html.Should().Contain("<body>");
    }

    [Fact]
    public void Build_HorizontalRule_RendersHrTag()
    {
        var html = HtmlContentBuilder.Build(Page("Before\n---\nAfter"), isOrgFile: false);

        html.Should().Contain("<hr>");
    }

    [Fact]
    public void Build_UnorderedList_RendersUlWithLi()
    {
        var content = @"- Item 1
- Item 2
- Item 3";
        var html = HtmlContentBuilder.Build(Page(content), isOrgFile: false);

        var body = Body(html);
        body.Should().Contain("<ul>");
        body.Should().Contain("<li>Item 1</li>");
        body.Should().Contain("<li>Item 2</li>");
        body.Should().Contain("<li>Item 3</li>");
        body.Should().Contain("</ul>");
    }

    [Fact]
    public void Build_OrderedList_RendersOlWithLi()
    {
        var content = @"1. First
2. Second";
        var html = HtmlContentBuilder.Build(Page(content), isOrgFile: false);

        var body = Body(html);
        body.Should().Contain("<ol>");
        body.Should().Contain("<li>First</li>");
        body.Should().Contain("</ol>");
    }

    [Fact]
    public void Build_NestedList_RendersNestedUl()
    {
        var content = @"- Parent
  - Child";
        var html = HtmlContentBuilder.Build(Page(content), isOrgFile: false);

        var body = Body(html);
        body.Should().Contain("<ul>");
        body.Should().Contain("<li>Parent");
        body.Should().Contain("<li>Child</li>");
    }

    [Fact]
    public void Build_CheckboxTodo_RendersTodoSpan()
    {
        var html = HtmlContentBuilder.Build(Page("- [ ] Task"), isOrgFile: false);

        html.Should().Contain("<span class=\"todo-unchecked\"></span>");
        html.Should().Contain("Task");
    }

    [Fact]
    public void Build_CheckboxDone_RendersCheckedSpan()
    {
        var html = HtmlContentBuilder.Build(Page("- [x] Done"), isOrgFile: false);

        html.Should().Contain("<span class=\"todo-checked\"></span>");
    }

    [Fact]
    public void Build_TodoKeyword_RendersTodoKeywordSpan()
    {
        var html = HtmlContentBuilder.Build(Page("TODO: fix this"), isOrgFile: false);

        html.Should().Contain("<span class=\"todo-keyword\">TODO</span>");
    }

    [Fact]
    public void Build_DoneKeyword_RendersDoneKeywordSpan()
    {
        var html = HtmlContentBuilder.Build(Page("DONE: completed"), isOrgFile: false);

        html.Should().Contain("<span class=\"done-keyword\">DONE</span>");
    }

    [Fact]
    public void Build_LinkMarkdown_RendersAnchorTag()
    {
        var html = HtmlContentBuilder.Build(Page("[text](https://example.com)"), isOrgFile: false);

        html.Should().Contain("<a data-href=\"https://example.com\"");
    }

    [Fact]
    public void Build_BareUrl_AutoLinks()
    {
        var html = HtmlContentBuilder.Build(Page("Visit https://example.com now"), isOrgFile: false);

        html.Should().Contain("<a data-href=\"https://example.com\"");
    }

    [Fact]
    public void Build_WrapInHtml_ContainsDoctypeAndStyle()
    {
        var html = HtmlContentBuilder.Build(Page("hello"), isOrgFile: false);

        html.Should().StartWith("<!DOCTYPE html>");
        html.Should().Contain("<html>");
        html.Should().Contain("<style>");
    }

    [Fact]
    public void Build_EscapeHtml_PreventsXss()
    {
        var html = HtmlContentBuilder.Build(Page("<script>alert('xss')</script>"), isOrgFile: false);

        var body = Body(html);
        body.Should().Contain("&lt;script&gt;");
        body.Should().NotContain("<script>alert");
    }

    [Fact]
    public void Build_InlineDollarMath_RendersDelimitersInHtml()
    {
        var html = HtmlContentBuilder.Build(Page("Value is $x^2$ here"), isOrgFile: false);

        var body = Body(html);
        body.Should().Contain("$x^2$");
    }

    [Fact]
    public void Build_InlineDollarMath_NotDestroyedByItalic()
    {
        var html = HtmlContentBuilder.Build(Page("Equation: $x^2 + y^2 = z^2$"), isOrgFile: false);

        var body = Body(html);
        body.Should().Contain("$x^2 + y^2 = z^2$");
        body.Should().NotContain("<em>");
    }

    [Fact]
    public void Build_DisplayMathDoubleDollar_RendersBlockDiv()
    {
        var html = HtmlContentBuilder.Build(Page("$$x^2$$"), isOrgFile: false);

        var body = Body(html);
        body.Should().Contain("<div class=\"math-display\"");
        body.Should().Contain("data-latex=\"x^2\"");
        body.Should().Contain("$$x^2$$");
    }

    [Fact]
    public void Build_DisplayMathBracket_RendersBlockDiv()
    {
        var html = HtmlContentBuilder.Build(Page(@"\[x^2\]"), isOrgFile: false);

        var body = Body(html);
        body.Should().Contain("<div class=\"math-display\"");
        body.Should().Contain(@"\[x^2\]");
    }

    [Fact]
    public void Build_InlineParenMath_RendersDelimiters()
    {
        var html = HtmlContentBuilder.Build(Page(@"Value is \(x^2\) here"), isOrgFile: false);

        var body = Body(html);
        body.Should().Contain(@"\(x^2\)");
    }

    [Fact]
    public void Build_CodeInsideDollar_StaysAsCode()
    {
        var html = HtmlContentBuilder.Build(Page(@"Code is `$x^2$` here"), isOrgFile: false);

        var body = Body(html);
        body.Should().Contain("<code>$x^2$</code>");
    }

    [Fact]
    public void Build_MultiLineDisplayMathDoubleDollar_RendersBlockDiv()
    {
        var content = @"$$
\begin{align}
x &= 1 \\
y &= 2
\end{align}
$$";
        var html = HtmlContentBuilder.Build(Page(content), isOrgFile: false);

        var body = Body(html);
        body.Should().Contain("<div class=\"math-display\"");
        body.Should().Contain("data-latex=\"\\begin{align}");
        body.Should().Contain("y &amp;= 2");
        body.Should().Contain("\\end{align}");
        body.Should().Contain("$$");
    }

    [Fact]
    public void Build_DollarSignInText_NotTreatedAsMath()
    {
        var html = HtmlContentBuilder.Build(Page("It costs $5 for coffee"), isOrgFile: false);

        var body = Body(html);
        body.Should().Contain("$5 for coffee");
        body.Should().NotContain("class=\"math-display\"");
    }

    [Fact]
    public void Build_OrgInlineDollarMath_RendersDelimiters()
    {
        var html = HtmlContentBuilder.Build(Page("Value is $x^2$ here"), isOrgFile: true);

        var body = Body(html);
        body.Should().Contain("$x^2$");
    }

    [Fact]
    public void Build_OrgDisplayMathBracket_RendersBlockDiv()
    {
        var html = HtmlContentBuilder.Build(Page(@"\[x^2\]"), isOrgFile: true);

        var body = Body(html);
        body.Should().Contain("<div class=\"math-display\"");
        body.Should().Contain(@"\[x^2\]");
    }

    [Fact]
    public void Build_MathInHeading_RendersMathInsideHeading()
    {
        var html = HtmlContentBuilder.Build(Page("# Section with $x^2$"), isOrgFile: false);

        html.Should().Contain("<h1>");
        html.Should().Contain("$x^2$");
    }

    [Fact]
    public void Build_WrapInHtml_ContainsKaTeXPlaceholders()
    {
        var html = HtmlContentBuilder.Build(Page("hello"), isOrgFile: false);

        html.Should().Contain("<!--KATEX_CSS-->");
        html.Should().Contain("/* KATEX_AUTO_RENDER */");
    }

    [Fact]
    public void Build_MathDisplay_ContainsCopyButton()
    {
        var html = HtmlContentBuilder.Build(Page("$$x^2$$"), isOrgFile: false);

        html.Should().Contain(".math-display:hover .copy-btn");
        html.Should().Contain("data-latex");
    }

    [Fact]
    public void Build_DisplayMathSingleLineDoubleDollar_NotInParagraph()
    {
        var html = HtmlContentBuilder.Build(Page("Text before\n$$x^2$$\nText after"), isOrgFile: false);

        var body = Body(html);
        body.Should().Contain("<div class=\"math-display\"");
        body.Should().Contain("$$x^2$$");
    }
}
