using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using NextLearn.Desktop.Data;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;
using Xunit;

namespace NextLearn.Desktop.Tests;

public class DeckServiceTests
{
    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    private static IUserService MockUserService(Guid userId, User? user = null)
    {
        var mock = Substitute.For<IUserService>();
        mock.GetCurrentUserId().Returns(userId);
        mock.GetCurrentUser().Returns(user ?? new User { Id = userId, DisplayName = "TestUser" });
        return mock;
    }

    private static Deck CreateDeck(string title, bool published = true, bool reviewed = true, int pageCount = 1)
    {
        var deck = new Deck
        {
            Id = Guid.NewGuid(),
            Title = title,
            FileName = $"{title}.md",
            IsPublished = published,
            IsReviewed = reviewed,
            Description = $"Description of {title}",
            PageCount = pageCount,
            CreatedAt = DateTime.UtcNow,
        };

        for (var i = 1; i <= pageCount; i++)
        {
            deck.Pages.Add(new Page
            {
                Id = Guid.NewGuid(),
                Title = $"Page {i}",
                TextContent = $"Content {i}",
                ContentType = ContentType.Text,
                PageNumber = i,
            });
        }

        return deck;
    }

    [Fact]
    public void GetPublishedDecks_ReturnsOnlyPublishedAndReviewed()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(Guid.NewGuid()));

        ctx.Decks.AddRange(
            CreateDeck("Published", published: true, reviewed: true),
            CreateDeck("Unpublished", published: false, reviewed: true),
            CreateDeck("Unreviewed", published: true, reviewed: false));
        ctx.SaveChanges();

        var results = svc.GetPublishedDecks();

        results.Should().HaveCount(1);
        results[0].Title.Should().Be("Published");
    }

    [Fact]
    public void GetPublishedDecks_ReturnsOrderedByCreatedAtDesc()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(Guid.NewGuid()));

        var old = CreateDeck("Old");
        old.CreatedAt = DateTime.UtcNow.AddDays(-2);
        var mid = CreateDeck("Mid");
        mid.CreatedAt = DateTime.UtcNow.AddDays(-1);
        var recent = CreateDeck("Recent");
        recent.CreatedAt = DateTime.UtcNow;

        ctx.Decks.AddRange(old, mid, recent);
        ctx.SaveChanges();

        var results = svc.GetPublishedDecks();

        results.Should().HaveCount(3);
        results[0].Title.Should().Be("Recent");
        results[1].Title.Should().Be("Mid");
        results[2].Title.Should().Be("Old");
    }

    [Fact]
    public void GetDeckById_ReturnsDeckWithPagesOrderedByPageNumber()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(Guid.NewGuid()));

        var deck = CreateDeck("TestDeck", pageCount: 3);
        ctx.Decks.Add(deck);
        ctx.SaveChanges();

        var result = svc.GetDeckById(deck.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be("TestDeck");
        result.Pages.Should().HaveCount(3);
        result.Pages.Select(p => p.PageNumber).Should().BeInAscendingOrder();
    }

    [Fact]
    public void GetDeckById_NonExistent_ReturnsNull()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(Guid.NewGuid()));

        var result = svc.GetDeckById(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public void SaveOrUpdateDeck_CreatesNewDeckWithAuthorId()
    {
        var userId = Guid.NewGuid();
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(userId));

        var deck = CreateDeck("NewDeck");
        svc.SaveOrUpdateDeck(deck);

        ctx.Decks.Should().HaveCount(1);
        ctx.Decks.Single().AuthorId.Should().Be(userId);
    }

    [Fact]
    public void SaveOrUpdateDeck_UpdatesExistingDeck()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(Guid.NewGuid()));

        var deck = CreateDeck("Original");
        ctx.Decks.Add(deck);
        ctx.SaveChanges();

        deck.Title = "Updated";
        deck.Pages = deck.Pages.Select(p => new Page
        {
            Id = Guid.NewGuid(),
            Title = p.Title,
            TextContent = p.TextContent,
            ContentType = p.ContentType,
            PageNumber = p.PageNumber,
        }).ToList();
        svc.SaveOrUpdateDeck(deck);

        ctx.Decks.Should().HaveCount(1);
        ctx.Decks.Single().Title.Should().Be("Updated");
    }

    [Fact]
    public void GetPage_ReturnsPageById()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(Guid.NewGuid()));

        var deck = CreateDeck("Test", pageCount: 2);
        ctx.Decks.Add(deck);
        ctx.SaveChanges();

        var page = deck.Pages.First();
        var result = svc.GetPage(page.Id);

        result.Should().NotBeNull();
        result!.Title.Should().Be(page.Title);
    }

    [Fact]
    public void GetPage_NonExistent_ReturnsNull()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(Guid.NewGuid()));

        var result = svc.GetPage(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetUserProgressAsync_ReturnsProgressForUserAndDeck()
    {
        var userId = Guid.NewGuid();
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(userId));

        var deck = CreateDeck("Test");
        ctx.Decks.Add(deck);
        ctx.SaveChanges();

        var progress = new UserProgress
        {
            UserId = userId,
            DeckId = deck.Id,
            CurrentPage = 2,
            IsCompleted = false,
        };
        ctx.UserProgress.Add(progress);
        ctx.SaveChanges();

        var result = await svc.GetUserProgressAsync(deck.Id);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.DeckId.Should().Be(deck.Id);
        result.CurrentPage.Should().Be(2);
    }

    [Fact]
    public async Task GetUserProgressAsync_NonExistent_ReturnsNull()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(Guid.NewGuid()));

        var result = await svc.GetUserProgressAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task StartLearningAsync_CreatesProgressWhenMissing()
    {
        var userId = Guid.NewGuid();
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(userId));

        var deck = CreateDeck("Test");
        ctx.Decks.Add(deck);
        ctx.SaveChanges();

        var result = await svc.StartLearningAsync(deck.Id);

        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.DeckId.Should().Be(deck.Id);
        result.CurrentPage.Should().Be(1);
        result.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task StartLearningAsync_AddsActiveLearningSlot()
    {
        var userId = Guid.NewGuid();
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(userId));

        var deck = CreateDeck("Test");
        ctx.Decks.Add(deck);
        ctx.SaveChanges();

        await svc.StartLearningAsync(deck.Id);

        var slots = ctx.ActiveLearning.Where(al => al.UserId == userId).ToList();
        slots.Should().HaveCount(1);
        slots[0].DeckId.Should().Be(deck.Id);
        slots[0].Slot.Should().Be(1);
    }

    [Fact]
    public async Task StartLearningAsync_RespectsMaxTwoActiveSlots()
    {
        var userId = Guid.NewGuid();
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(userId));

        var deck1 = CreateDeck("Deck1");
        var deck2 = CreateDeck("Deck2");
        var deck3 = CreateDeck("Deck3");
        ctx.Decks.AddRange(deck1, deck2, deck3);
        ctx.SaveChanges();

        await svc.StartLearningAsync(deck1.Id);
        await svc.StartLearningAsync(deck2.Id);
        await svc.StartLearningAsync(deck3.Id);

        var slots = ctx.ActiveLearning.Where(al => al.UserId == userId).ToList();
        slots.Should().HaveCount(2);
    }

    [Fact]
    public async Task StartLearningAsync_ReturnsExistingProgress()
    {
        var userId = Guid.NewGuid();
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(userId));

        var deck = CreateDeck("Test");
        ctx.Decks.Add(deck);
        ctx.SaveChanges();

        var first = await svc.StartLearningAsync(deck.Id);
        var second = await svc.StartLearningAsync(deck.Id);

        first.Id.Should().Be(second.Id);
    }

    [Fact]
    public async Task UpdateProgressAsync_CreatesProgressWhenMissing()
    {
        var userId = Guid.NewGuid();
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(userId));

        var deck = CreateDeck("Test");
        ctx.Decks.Add(deck);
        ctx.SaveChanges();

        await svc.UpdateProgressAsync(deck.Id, 3);

        var progress = ctx.UserProgress.Single();
        progress.UserId.Should().Be(userId);
        progress.DeckId.Should().Be(deck.Id);
        progress.CurrentPage.Should().Be(3);
    }

    [Fact]
    public async Task UpdateProgressAsync_UpdatesExistingProgress()
    {
        var userId = Guid.NewGuid();
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(userId));

        var deck = CreateDeck("Test");
        ctx.Decks.Add(deck);
        ctx.SaveChanges();

        await svc.UpdateProgressAsync(deck.Id, 2);
        await svc.UpdateProgressAsync(deck.Id, 5);

        var progress = ctx.UserProgress.Single();
        progress.CurrentPage.Should().Be(5);
    }

    [Fact]
    public async Task UpdateProgressAsync_SetsLastAccessedAt()
    {
        var userId = Guid.NewGuid();
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(userId));

        var deck = CreateDeck("Test");
        ctx.Decks.Add(deck);
        ctx.SaveChanges();

        await svc.UpdateProgressAsync(deck.Id, 1);

        ctx.UserProgress.Single().LastAccessedAt.Should().BeCloseTo(DateTime.UtcNow, precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MarkCompletedAsync_SetsIsCompleted()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, DisplayName = "Test" };
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(userId, user));

        var deck = CreateDeck("Test");
        ctx.Decks.Add(deck);
        await ctx.SaveChangesAsync();

        await svc.StartLearningAsync(deck.Id);
        await svc.MarkCompletedAsync(deck.Id);

        var progress = ctx.UserProgress.Single();
        progress.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task MarkCompletedAsync_IncrementsTotalDecksCompleted()
    {
        var userId = Guid.NewGuid();
        var user = new User { Id = userId, DisplayName = "Test", TotalDecksCompleted = 0 };
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(userId, user));

        var deck = CreateDeck("Test");
        ctx.Decks.Add(deck);
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        await svc.StartLearningAsync(deck.Id);
        await svc.MarkCompletedAsync(deck.Id);

        user.TotalDecksCompleted.Should().Be(1);
    }

    [Fact]
    public void GetActiveLearningSlots_ReturnsOrderedSlots()
    {
        var userId = Guid.NewGuid();
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(userId));

        var deck1 = CreateDeck("Deck1");
        var deck2 = CreateDeck("Deck2");
        ctx.Decks.AddRange(deck1, deck2);
        ctx.SaveChanges();

        ctx.ActiveLearning.AddRange(
            new ActiveLearning { UserId = userId, DeckId = deck2.Id, Slot = 2 },
            new ActiveLearning { UserId = userId, DeckId = deck1.Id, Slot = 1 });
        ctx.SaveChanges();

        var results = svc.GetActiveLearningSlots();

        results.Should().HaveCount(2);
        results[0].Slot.Should().Be(1);
        results[1].Slot.Should().Be(2);
    }

    [Fact]
    public void GetActiveLearningSlots_IncludesDeckNavigation()
    {
        var userId = Guid.NewGuid();
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(userId));

        var deck = CreateDeck("MyDeck");
        ctx.Decks.Add(deck);
        ctx.SaveChanges();

        ctx.ActiveLearning.Add(new ActiveLearning { UserId = userId, DeckId = deck.Id, Slot = 1 });
        ctx.SaveChanges();

        var results = svc.GetActiveLearningSlots();

        results.Should().HaveCount(1);
        results[0].Deck.Should().NotBeNull();
        results[0].Deck!.Title.Should().Be("MyDeck");
    }

    [Fact]
    public void GetActiveLearningSlots_OtherUsers_NotReturned()
    {
        var userId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new DeckService(ctx, MockUserService(userId));

        var deck = CreateDeck("MyDeck");
        var otherDeck = CreateDeck("OtherDeck");
        ctx.Decks.AddRange(deck, otherDeck);
        ctx.SaveChanges();

        ctx.ActiveLearning.AddRange(
            new ActiveLearning { UserId = userId, DeckId = deck.Id, Slot = 1 },
            new ActiveLearning { UserId = otherId, DeckId = otherDeck.Id, Slot = 1 });
        ctx.SaveChanges();

        var results = svc.GetActiveLearningSlots();

        results.Should().HaveCount(1);
        results[0].DeckId.Should().Be(deck.Id);
    }
}
