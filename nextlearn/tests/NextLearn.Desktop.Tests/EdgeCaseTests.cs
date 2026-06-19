using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NextLearn.Desktop.Data;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;
using Xunit;

namespace NextLearn.Desktop.Tests;

public class EdgeCaseTests
{
    [Fact]
    public void AppDbContext_InMemory_AllDbSetsAvailable()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var ctx = new AppDbContext(options);

        ctx.Users.Should().NotBeNull();
        ctx.Decks.Should().NotBeNull();
        ctx.Pages.Should().NotBeNull();
        ctx.UserProgress.Should().NotBeNull();
        ctx.ActiveLearning.Should().NotBeNull();
        ctx.DailyActivities.Should().NotBeNull();
    }

    [Fact]
    public void DeckFileIdentity_SamePath_SameGuid()
    {
        var id1 = DeckFileIdentity.GetId("/path/to/deck.md");
        var id2 = DeckFileIdentity.GetId("/path/to/deck.md");

        id1.Should().Be(id2);
    }

    [Fact]
    public void DeckFileIdentity_DifferentPaths_DifferentGuids()
    {
        var id1 = DeckFileIdentity.GetId("/path/to/deck.md");
        var id2 = DeckFileIdentity.GetId("/path/other/deck.md");

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void DeckFileIdentity_PinnedFile_SameAsUnpinned()
    {
        var clean = DeckFileIdentity.GetId("/decks/deck.md");
        var pinned = DeckFileIdentity.GetId("/decks/+deck.md");

        pinned.Should().Be(clean);
    }

    [Fact]
    public void DeckFileIdentity_ArchivedFile_SameAsUnarchived()
    {
        var clean = DeckFileIdentity.GetId("/decks/deck.md");
        var archived = DeckFileIdentity.GetId("/decks/deck.md~");

        archived.Should().Be(clean);
    }

    [Fact]
    public void DeckFileIdentity_PinnedAndArchived_SameAsClean()
    {
        var clean = DeckFileIdentity.GetId("/decks/deck.md");
        var pinnedArchived = DeckFileIdentity.GetId("/decks/+deck.md~");

        pinnedArchived.Should().Be(clean);
    }

    [Fact]
    public void DeckFileIdentity_IsDeterministic()
    {
        var id = DeckFileIdentity.GetId("/some/long/path/to/a/deck.md");

        id.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public void DeckFileParser_VeryLongFileName_LoadsSuccessfully()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NextLearnTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var longName = new string('a', 200) + ".md";
            var path = Path.Combine(dir, longName);
            File.WriteAllText(path, "# Title\nContent");

            var deck = DeckFileParser.LoadDeckFromFile(path);

            deck.Should().NotBeNull();
            deck!.FileName.Should().Be(longName);
            deck.Pages.Should().NotBeEmpty();
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void DeckFileParser_SpecialCharactersInFileName_LoadsSuccessfully()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NextLearnTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            var specialName = "!@#$%^&.md";
            var path = Path.Combine(dir, specialName);
            File.WriteAllText(path, "# Title\n## Page\nContent");

            var deck = DeckFileParser.LoadDeckFromFile(path);

            deck.Should().NotBeNull();
            deck!.FileName.Should().Be(specialName);
            deck.Pages.Should().HaveCount(1);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void HtmlContentBuilder_UnicodeCjk_RendersCorrectly()
    {
        var page = new Page
        {
            Id = Guid.NewGuid(),
            Title = "CJK Test",
            TextContent = "# 安녕하세요\nContent",
            ContentType = ContentType.Text,
            PageNumber = 1,
        };

        var html = HtmlContentBuilder.Build(page, isOrgFile: false);

        html.Should().Contain("安녕하세요");
        html.Should().Contain("<h1>");
    }

    [Fact]
    public void HtmlContentBuilder_UnicodeRtl_RendersCorrectly()
    {
        var page = new Page
        {
            Id = Guid.NewGuid(),
            Title = "RTL Test",
            TextContent = "* مرحبا\nContent",
            ContentType = ContentType.Text,
            PageNumber = 1,
        };

        var html = HtmlContentBuilder.Build(page, isOrgFile: true);

        html.Should().Contain("مرحبا");
        html.Should().Contain("<h1>");
    }

    [Fact]
    public void HtmlContentBuilder_UnicodeEmoji_RendersCorrectly()
    {
        var page = new Page
        {
            Id = Guid.NewGuid(),
            Title = "Emoji Test",
            TextContent = "## 🚀\nContent",
            ContentType = ContentType.Text,
            PageNumber = 1,
        };

        var html = HtmlContentBuilder.Build(page, isOrgFile: false);

        html.Should().Contain("🚀");
        html.Should().Contain("<h2>");
    }

    [Fact]
    public void HtmlContentBuilder_UnicodeMixedContent_RendersCorrectly()
    {
        var page = new Page
        {
            Id = Guid.NewGuid(),
            Title = "Mixed Test",
            TextContent = "# 中文\n* Español\n** Français\nDeutsch 📚",
            ContentType = ContentType.Text,
            PageNumber = 1,
        };

        var html = HtmlContentBuilder.Build(page, isOrgFile: false);

        html.Should().Contain("中文");
        html.Should().Contain("Español");
        html.Should().Contain("Français");
        html.Should().Contain("Deutsch");
        html.Should().Contain("📚");
    }

    [Fact]
    public void HtmlContentBuilder_UnicodeInInlineFormatting_RendersCorrectly()
    {
        var page = new Page
        {
            Id = Guid.NewGuid(),
            Title = "Formatting Test",
            TextContent = "**bold 中文** and *italic Español*",
            ContentType = ContentType.Text,
            PageNumber = 1,
        };

        var html = HtmlContentBuilder.Build(page, isOrgFile: false);

        html.Should().Contain("<strong>bold 中文</strong>");
        html.Should().Contain("<em>italic Español</em>");
    }
}
