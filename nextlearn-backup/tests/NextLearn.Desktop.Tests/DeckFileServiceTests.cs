using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NextLearn.Desktop.Data;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;
using Xunit;

namespace NextLearn.Desktop.Tests;

public class DeckFileServiceTests : IDisposable
{
    private readonly List<string> _dirsToCleanup = [];

    public void Dispose()
    {
        foreach (var dir in _dirsToCleanup)
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }

    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private string CreateDecksDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NextLearnTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        _dirsToCleanup.Add(dir);
        return dir;
    }

    private static string MinimalDeckContent() =>
        "---\ntitle: Test Deck\n---\n# Section\n## Page 1\nContent\n";

    private static string CreateDeckFile(string decksDir, string fileName)
    {
        var path = Path.Combine(decksDir, fileName);
        File.WriteAllText(path, MinimalDeckContent());
        return path;
    }

    private static Deck CreateDeckRecord(Guid id, string fileName)
    {
        return new Deck
        {
            Id = id,
            FileName = fileName,
            Title = "Test Deck",
            IsPublished = true,
            IsReviewed = true,
        };
    }

    [Fact]
    public void PinDeck_RenamesFileAndUpdatesDatabase()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckFileService(ctx);
        var decksDir = CreateDecksDir();
        var deckId = Guid.NewGuid();
        CreateDeckFile(decksDir, "deck.md");
        ctx.Decks.Add(CreateDeckRecord(deckId, "deck.md"));
        ctx.SaveChanges();

        svc.PinDeck(deckId, decksDir);

        File.Exists(Path.Combine(decksDir, "+deck.md")).Should().BeTrue();
        File.Exists(Path.Combine(decksDir, "deck.md")).Should().BeFalse();
        var deck = ctx.Decks.Find(deckId);
        deck.Should().NotBeNull();
        deck!.IsPinned.Should().BeTrue();
        deck.FileName.Should().Be("+deck.md");
    }

    [Fact]
    public void UnpinDeck_RemovesPrefixAndUpdatesDatabase()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckFileService(ctx);
        var decksDir = CreateDecksDir();
        var deckId = Guid.NewGuid();
        CreateDeckFile(decksDir, "+deck.md");
        var pinnedDeck = CreateDeckRecord(deckId, "+deck.md");
        pinnedDeck.IsPinned = true;
        ctx.Decks.Add(pinnedDeck);
        ctx.SaveChanges();

        svc.UnpinDeck(deckId, decksDir);

        File.Exists(Path.Combine(decksDir, "deck.md")).Should().BeTrue();
        File.Exists(Path.Combine(decksDir, "+deck.md")).Should().BeFalse();
        var deck = ctx.Decks.Find(deckId);
        deck.Should().NotBeNull();
        deck!.IsPinned.Should().BeFalse();
        deck.FileName.Should().Be("deck.md");
    }

    [Fact]
    public void ArchiveDeck_AppendsTildeAndUpdatesDatabase()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckFileService(ctx);
        var decksDir = CreateDecksDir();
        var deckId = Guid.NewGuid();
        CreateDeckFile(decksDir, "deck.md");
        ctx.Decks.Add(CreateDeckRecord(deckId, "deck.md"));
        ctx.SaveChanges();

        svc.ArchiveDeck(deckId, decksDir);

        File.Exists(Path.Combine(decksDir, "deck.md~")).Should().BeTrue();
        File.Exists(Path.Combine(decksDir, "deck.md")).Should().BeFalse();
        var deck = ctx.Decks.Find(deckId);
        deck.Should().NotBeNull();
        deck!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void UnarchiveDeck_RemovesTildeAndUpdatesDatabase()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckFileService(ctx);
        var decksDir = CreateDecksDir();
        var deckId = Guid.NewGuid();
        CreateDeckFile(decksDir, "deck.md~");
        var archivedDeck = CreateDeckRecord(deckId, "deck.md");
        archivedDeck.IsArchived = true;
        ctx.Decks.Add(archivedDeck);
        ctx.SaveChanges();

        svc.UnarchiveDeck(deckId, decksDir);

        File.Exists(Path.Combine(decksDir, "deck.md")).Should().BeTrue();
        File.Exists(Path.Combine(decksDir, "deck.md~")).Should().BeFalse();
        var deck = ctx.Decks.Find(deckId);
        deck.Should().NotBeNull();
        deck!.IsArchived.Should().BeFalse();
    }

    [Fact]
    public void PinDeck_FileMissing_SkipsGracefully()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckFileService(ctx);
        var decksDir = CreateDecksDir();
        var deckId = Guid.NewGuid();
        ctx.Decks.Add(CreateDeckRecord(deckId, "deck.md"));
        ctx.SaveChanges();

        svc.PinDeck(deckId, decksDir);

        var deck = ctx.Decks.Find(deckId);
        deck.Should().NotBeNull();
        deck!.IsPinned.Should().BeTrue();
    }

    [Fact]
    public void ArchiveDeck_FileMissing_SkipsGracefully()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckFileService(ctx);
        var decksDir = CreateDecksDir();
        var deckId = Guid.NewGuid();
        ctx.Decks.Add(CreateDeckRecord(deckId, "deck.md"));
        ctx.SaveChanges();

        svc.ArchiveDeck(deckId, decksDir);

        var deck = ctx.Decks.Find(deckId);
        deck.Should().NotBeNull();
        deck!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public void ArchiveDeck_DeckNotFound_DoesNothing()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckFileService(ctx);
        var decksDir = CreateDecksDir();

        svc.ArchiveDeck(Guid.NewGuid(), decksDir);

        ctx.Decks.Should().BeEmpty();
    }

    [Fact]
    public void UnpinDeck_NotPinned_DoesNothing()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckFileService(ctx);
        var decksDir = CreateDecksDir();
        var deckId = Guid.NewGuid();
        CreateDeckFile(decksDir, "deck.md");
        ctx.Decks.Add(CreateDeckRecord(deckId, "deck.md"));
        ctx.SaveChanges();

        svc.UnpinDeck(deckId, decksDir);

        var deck = ctx.Decks.Find(deckId);
        deck.Should().NotBeNull();
        deck!.IsPinned.Should().BeFalse();
        deck.FileName.Should().Be("deck.md");
    }

    [Fact]
    public void GetPinnedDecks_ReturnsOnlyPinnedFiles()
    {
        var decksDir = CreateDecksDir();
        CreateDeckFile(decksDir, "+pinned.md");
        CreateDeckFile(decksDir, "normal.md");
        CreateDeckFile(decksDir, "+also-pinned.org");

        var result = DeckFileService.GetPinnedDecks(decksDir);

        result.Should().HaveCount(2);
        result.All(d => d.IsPinned).Should().BeTrue();
        result.Select(d => d.FileName).Should().BeEquivalentTo("+pinned.md", "+also-pinned.org");
    }

    [Fact]
    public void GetArchivedDecks_ReturnsOnlyArchivedFiles()
    {
        var decksDir = CreateDecksDir();
        CreateDeckFile(decksDir, "archived.md~");
        CreateDeckFile(decksDir, "normal.md");
        CreateDeckFile(decksDir, "also-archived.org~");

        var result = DeckFileService.GetArchivedDecks(decksDir);

        result.Should().HaveCount(2);
        result.All(d => d.IsArchived).Should().BeTrue();
        result.Select(d => d.FileName).Should().BeEquivalentTo("archived.md~", "also-archived.org~");
    }

    [Fact]
    public void GetPinnedDecks_EmptyDir_ReturnsEmptyList()
    {
        var decksDir = CreateDecksDir();

        var result = DeckFileService.GetPinnedDecks(decksDir);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetArchivedDecks_EmptyDir_ReturnsEmptyList()
    {
        var decksDir = CreateDecksDir();

        var result = DeckFileService.GetArchivedDecks(decksDir);

        result.Should().BeEmpty();
    }
}
