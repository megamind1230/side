using FluentAssertions;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;
using Xunit;

namespace NextLearn.Desktop.Tests;

public class DeckFileParserTests : IDisposable
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

    private string CreateFile(string content, string extension = ".md")
    {
        var dir = Path.Combine(Path.GetTempPath(), "NextLearnTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"deck{extension}");
        File.WriteAllText(path, content);
        _filesToCleanup.Add(path);
        return path;
    }

    [Fact]
    public void LoadDeckFromFile_ValidMdWithFrontmatter_ReturnsCorrectDeckAndPages()
    {
        var content = @"---
title: My Deck
description: A test deck
tags: tag1, tag2
---
# Section 1
## Page 1
Content for page 1
## Page 2
Content for page 2
";
        var path = CreateFile(content);
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.Title.Should().Be("My Deck");
        deck.Description.Should().Be("A test deck");
        deck.Tags.Should().Be("tag1, tag2");
        deck.FileName.Should().Be("deck.md");
        deck.HasExplicitTitle.Should().BeTrue();
        deck.IsPublished.Should().BeTrue();
        deck.IsArchived.Should().BeFalse();
        deck.IsPinned.Should().BeFalse();
        deck.PageCount.Should().Be(2);
        deck.Pages.Should().HaveCount(2);
        deck.Pages.ElementAt(0).Title.Should().Be("Page 1");
        deck.Pages.ElementAt(0).SectionTitle.Should().Be("Section 1");
        deck.Pages.ElementAt(0).PageNumber.Should().Be(1);
        deck.Pages.ElementAt(0).ContentType.Should().Be(ContentType.Text);
        deck.Pages.ElementAt(1).Title.Should().Be("Page 2");
        deck.Pages.ElementAt(1).SectionTitle.Should().Be("Section 1");
        deck.Pages.ElementAt(1).PageNumber.Should().Be(2);
    }

    [Fact]
    public void LoadDeckFromFile_ValidOrgWithKeywords_ReturnsCorrectDeck()
    {
        var content = @"#+TITLE: Org Deck
#+DESCRIPTION: Org description
#+TAGS: :tag1:tag2:
* Section 1
** Page 1
Org content
";
        var path = CreateFile(content, ".org");
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.Title.Should().Be("Org Deck");
        deck.Description.Should().Be("Org description");
        deck.Tags.Should().Be("tag1, tag2");
        deck.HasExplicitTitle.Should().BeTrue();
        deck.Pages.Should().HaveCount(1);
        deck.Pages.ElementAt(0).Title.Should().Be("Page 1");
    }

    [Fact]
    public void LoadDeckFromFile_MissingFrontmatter_FallsBackToFirstContentLine()
    {
        var content = @"First content line
Second line for description
## Page 1
Some content
";
        var path = CreateFile(content);
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.Title.Should().Be("First content line");
        deck.Description.Should().Be("Second line for description");
        deck.HasExplicitTitle.Should().BeFalse();
    }

    [Fact]
    public void LoadDeckFromFile_MissingFrontmatterNoContentLines_FallsBackToFileName()
    {
        var path = CreateFile(string.Empty);
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.HasExplicitTitle.Should().BeFalse();
    }

    [Fact]
    public void LoadDeckFromFile_EmptyFile_ReturnsSingleEmptyPage()
    {
        var path = CreateFile(string.Empty);
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.Pages.Should().HaveCount(1);
        deck.Pages.ElementAt(0).TextContent.Should().BeEmpty();
        deck.Pages.ElementAt(0).PageNumber.Should().Be(1);
    }

    [Fact]
    public void LoadDeckFromFile_NoHeadings_ReturnsSinglePage()
    {
        var content = "Just a plain paragraph with no headings whatsoever.";
        var path = CreateFile(content);
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.Pages.Should().HaveCount(1);
        deck.Pages.ElementAt(0).TextContent.Should().Contain("plain paragraph");
        deck.Pages.ElementAt(0).IsPreHeadingPage.Should().BeTrue();
    }

    [Fact]
    public void LoadDeckFromFile_TagsPlainText_ParsesCorrectly()
    {
        var content = @"---
title: Tags Test
tags: a, b, c
---
Content
";
        var path = CreateFile(content);
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.Tags.Should().Be("a, b, c");
    }

    [Fact]
    public void LoadDeckFromFile_OrgTagsWithColonDelimiter_NormalizesToCommaSeparated()
    {
        var content = @"#+TITLE: Org Tags
#+TAGS: :hero:dota2:
* Heading
Content
";
        var path = CreateFile(content, ".org");
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.Tags.Should().Be("hero, dota2");
    }

    [Fact]
    public void LoadDeckFromFile_OrgTagsDoubleColon_ConvertsToSlash()
    {
        var content = @"#+TITLE: Org Tags
#+TAGS: :dota2::hero:
* Heading
Content
";
        var path = CreateFile(content, ".org");
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.Tags.Should().Be("dota2/hero");
    }

    [Fact]
    public void LoadDeckFromFile_NonExistentFile_ReturnsNull()
    {
        var path = "/nonexistent/path/deck.md";
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().BeNull();
    }

    [Fact]
    public void LoadDeckFromFile_ArchivedFile_DetectsIsArchived()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NextLearnTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "deck.md~");
        File.WriteAllText(path, "# Title\nContent");
        _filesToCleanup.Add(path);

        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.IsArchived.Should().BeTrue();
        deck.FileName.Should().Be("deck.md~");
    }

    [Fact]
    public void LoadDeckFromFile_PinnedFile_DetectsIsPinned()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NextLearnTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "+deck.md");
        File.WriteAllText(path, "# Title\nContent");
        _filesToCleanup.Add(path);

        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.IsPinned.Should().BeTrue();
        deck.FileName.Should().Be("+deck.md");
    }

    [Fact]
    public void LoadDeckFromFile_EmptyTagsString_ReturnsEmpty()
    {
        var content = @"---
title: No Tags

---
Content
";
        var path = CreateFile(content);
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.Tags.Should().BeEmpty();
    }

    [Fact]
    public void LoadDeckFromFile_TagsBlockList_ParsesCorrectly()
    {
        var content = @"---
title: Block List
tags:
  - food
  - computer
  - nested/tag
---
Content
";
        var path = CreateFile(content);
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.Tags.Should().Be("food, computer, nested/tag");
    }

    [Fact]
    public void LoadDeckFromFile_TagsInlineArray_ParsesCorrectly()
    {
        var content = @"---
title: Inline Array
tags: [tag1, tag2, some-keyword]
---
Content
";
        var path = CreateFile(content);
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.Tags.Should().Be("tag1, tag2, some-keyword");
    }

    [Fact]
    public void LoadDeckFromFile_TagsQuotedString_ParsesCorrectly()
    {
        var content = @"---
title: Quoted
tags: ""tag1, tag2""
---
Content
";
        var path = CreateFile(content);
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.Tags.Should().Be("tag1, tag2");
    }

    [Fact]
    public void LoadDeckFromFile_TagsSingleQuotedString_ParsesCorrectly()
    {
        var content = @"---
title: Single Quoted
tags: 'tag1, tag2'
---
Content
";
        var path = CreateFile(content);
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.Tags.Should().Be("tag1, tag2");
    }

    [Fact]
    public void LoadDeckFromFile_TagsEmptyBlockList_ReturnsEmpty()
    {
        var content = @"---
title: Empty Block List
tags:
---
Content
";
        var path = CreateFile(content);
        var deck = DeckFileParser.LoadDeckFromFile(path);

        deck.Should().NotBeNull();
        deck!.Tags.Should().BeEmpty();
    }
}
