using FluentAssertions;
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

    private string CreateDecksDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "NextLearnTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        _dirsToCleanup.Add(dir);
        return dir;
    }

    private static string MinimalDeckContent() =>
        "---\ntitle: Test Deck\n---\n# Section\n## Page 1\nContent\n";

    private static void WriteFile(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, MinimalDeckContent());
    }

    private static (Deck Deck, string Path) CreateDeckFileAndDeck(string decksDir, string fileName)
    {
        var path = Path.Combine(decksDir, fileName);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, MinimalDeckContent());
        var deck = new Deck
        {
            Id = DeckFileIdentity.GetId(path),
            FileName = fileName,
            Title = "Test Deck",
        };
        return (deck, path);
    }

    [Fact]
    public void PinDeck_RenamesFileAndUpdatesDeckObject()
    {
        var svc = new DeckFileService();
        var decksDir = CreateDecksDir();
        var (deck, _) = CreateDeckFileAndDeck(decksDir, "deck.md");

        svc.PinDeck(deck, decksDir);

        File.Exists(Path.Combine(decksDir, "+deck.md")).Should().BeTrue();
        File.Exists(Path.Combine(decksDir, "deck.md")).Should().BeFalse();
        deck.IsPinned.Should().BeTrue();
        deck.FileName.Should().Be("+deck.md");
    }

    [Fact]
    public void UnpinDeck_RemovesPrefixAndUpdatesDeckObject()
    {
        var svc = new DeckFileService();
        var decksDir = CreateDecksDir();
        var (deck, _) = CreateDeckFileAndDeck(decksDir, "+deck.md");

        svc.UnpinDeck(deck, decksDir);

        File.Exists(Path.Combine(decksDir, "deck.md")).Should().BeTrue();
        File.Exists(Path.Combine(decksDir, "+deck.md")).Should().BeFalse();
        deck.IsPinned.Should().BeFalse();
        deck.FileName.Should().Be("deck.md");
    }

    [Fact]
    public void ArchiveDeck_AppendsTildeAndUpdatesDeckObject()
    {
        var svc = new DeckFileService();
        var decksDir = CreateDecksDir();
        var (deck, _) = CreateDeckFileAndDeck(decksDir, "deck.md");

        svc.ArchiveDeck(deck, decksDir);

        File.Exists(Path.Combine(decksDir, "deck.md~")).Should().BeTrue();
        File.Exists(Path.Combine(decksDir, "deck.md")).Should().BeFalse();
        deck.IsArchived.Should().BeTrue();
        deck.FileName.Should().Be("deck.md~");
    }

    [Fact]
    public void UnarchiveDeck_RemovesTildeAndUpdatesDeckObject()
    {
        var svc = new DeckFileService();
        var decksDir = CreateDecksDir();
        var (deck, _) = CreateDeckFileAndDeck(decksDir, "deck.md~");

        svc.UnarchiveDeck(deck, decksDir);

        File.Exists(Path.Combine(decksDir, "deck.md")).Should().BeTrue();
        File.Exists(Path.Combine(decksDir, "deck.md~")).Should().BeFalse();
        deck.IsArchived.Should().BeFalse();
        deck.FileName.Should().Be("deck.md");
    }

    [Fact]
    public void PinDeck_AlreadyPinned_DoesNothing()
    {
        var svc = new DeckFileService();
        var decksDir = CreateDecksDir();
        var (deck, _) = CreateDeckFileAndDeck(decksDir, "+deck.md");
        var originalName = deck.FileName;

        svc.PinDeck(deck, decksDir);

        File.Exists(Path.Combine(decksDir, "+deck.md")).Should().BeTrue();
        deck.IsPinned.Should().BeFalse(); // was already false, PinDeck returned early
        deck.FileName.Should().Be(originalName);
    }

    [Fact]
    public void UnpinDeck_NotPinned_DoesNothing()
    {
        var svc = new DeckFileService();
        var decksDir = CreateDecksDir();
        var (deck, _) = CreateDeckFileAndDeck(decksDir, "deck.md");

        svc.UnpinDeck(deck, decksDir);

        File.Exists(Path.Combine(decksDir, "deck.md")).Should().BeTrue();
        deck.IsPinned.Should().BeFalse();
        deck.FileName.Should().Be("deck.md");
    }

    [Fact]
    public void ArchiveDeck_AlreadyArchived_DoesNothing()
    {
        var svc = new DeckFileService();
        var decksDir = CreateDecksDir();
        var (deck, _) = CreateDeckFileAndDeck(decksDir, "deck.md~");

        svc.ArchiveDeck(deck, decksDir);

        File.Exists(Path.Combine(decksDir, "deck.md~")).Should().BeTrue();
        deck.IsArchived.Should().BeFalse(); // was already false, ArchiveDeck returned early
        deck.FileName.Should().Be("deck.md~");
    }

    [Fact]
    public void UnarchiveDeck_NotArchived_DoesNothing()
    {
        var svc = new DeckFileService();
        var decksDir = CreateDecksDir();
        var (deck, _) = CreateDeckFileAndDeck(decksDir, "deck.md");

        svc.UnarchiveDeck(deck, decksDir);

        deck.IsArchived.Should().BeFalse();
        deck.FileName.Should().Be("deck.md");
    }

    [Fact]
    public void PinDeck_FileMissing_SkipsGracefully()
    {
        var svc = new DeckFileService();
        var decksDir = CreateDecksDir();
        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            FileName = "deck.md",
            Title = "Test Deck",
        };

        svc.PinDeck(deck, decksDir);

        deck.IsPinned.Should().BeTrue();
        deck.FileName.Should().Be("+deck.md");
    }

    [Fact]
    public void ArchiveDeck_FileMissing_SkipsGracefully()
    {
        var svc = new DeckFileService();
        var decksDir = CreateDecksDir();
        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            FileName = "deck.md",
            Title = "Test Deck",
        };

        svc.ArchiveDeck(deck, decksDir);

        deck.IsArchived.Should().BeTrue();
        deck.FileName.Should().Be("deck.md~");
    }

    [Fact]
    public void PinDeck_Subdirectory_PrependsPlusToFileNameOnly()
    {
        var svc = new DeckFileService();
        var decksDir = CreateDecksDir();
        var (deck, _) = CreateDeckFileAndDeck(decksDir, "sub/deck.md");

        svc.PinDeck(deck, decksDir);

        File.Exists(Path.Combine(decksDir, "sub/+deck.md")).Should().BeTrue();
        deck.FileName.Should().Be("sub/+deck.md");
    }

    [Fact]
    public void UnpinDeck_Subdirectory_StripsPlusFromFileNameOnly()
    {
        var svc = new DeckFileService();
        var decksDir = CreateDecksDir();
        var (deck, _) = CreateDeckFileAndDeck(decksDir, "sub/+deck.md");

        svc.UnpinDeck(deck, decksDir);

        File.Exists(Path.Combine(decksDir, "sub/deck.md")).Should().BeTrue();
        deck.FileName.Should().Be("sub/deck.md");
    }

    [Fact]
    public void GetPinnedDecks_ReturnsOnlyPinnedFiles()
    {
        var decksDir = CreateDecksDir();
        WriteFile(Path.Combine(decksDir, "+pinned.md"));
        WriteFile(Path.Combine(decksDir, "normal.md"));
        WriteFile(Path.Combine(decksDir, "+also-pinned.org"));

        var result = DeckFileService.GetPinnedDecks(decksDir);

        result.Should().HaveCount(2);
        result.Select(d => d.FileName).Should().BeEquivalentTo("+pinned.md", "+also-pinned.org");
    }

    [Fact]
    public void GetArchivedDecks_ReturnsOnlyArchivedFiles()
    {
        var decksDir = CreateDecksDir();
        WriteFile(Path.Combine(decksDir, "archived.md~"));
        WriteFile(Path.Combine(decksDir, "normal.md"));
        WriteFile(Path.Combine(decksDir, "also-archived.org~"));

        var result = DeckFileService.GetArchivedDecks(decksDir);

        result.Should().HaveCount(2);
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
