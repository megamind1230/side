using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NextLearn.Desktop.Data;
using NextLearn.Desktop.Models;
using NextLearn.Desktop.Services;
using Xunit;

namespace NextLearn.Desktop.Tests;

public class UserServiceTests
{
    private static DateTime Today() => new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day);

    private static AppDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void Constructor_CreatesGuestUser()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());

        var svc = new UserService(ctx);

        var guest = ctx.Users.SingleOrDefault(u => u.IsGuest);
        guest.Should().NotBeNull();
        guest!.DisplayName.Should().Be("Guest");
        svc.GetCurrentUser().Should().BeSameAs(guest);
        svc.GetCurrentUserId().Should().Be(guest.Id);
    }

    [Fact]
    public void Constructor_ReusesExistingGuestUser()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        ctx.Users.Add(new User { DisplayName = "ExistingGuest", IsGuest = true });
        ctx.SaveChanges();

        var svc = new UserService(ctx);

        ctx.Users.Count(u => u.IsGuest).Should().Be(1);
        svc.GetCurrentUser().DisplayName.Should().Be("ExistingGuest");
    }

    [Fact]
    public void ComputeStreak_NoActivity_ReturnsZero()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);

        var streak = svc.ComputeStreak();

        streak.Should().Be(0);
    }

    [Fact]
    public void ComputeStreak_ActivityToday_ReturnsOne()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);
        ctx.DailyActivities.Add(new DailyActivity
        {
            UserId = svc.GetCurrentUserId(),
            Date = Today(),
            MinutesLearned = 1,
        });
        ctx.SaveChanges();

        var streak = svc.ComputeStreak();

        streak.Should().Be(1);
    }

    [Fact]
    public void ComputeStreak_ConsecutiveDays_ReturnsCorrectCount()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);
        var userId = svc.GetCurrentUserId();
        ctx.DailyActivities.AddRange(
            new DailyActivity { UserId = userId, Date = Today(), MinutesLearned = 1 },
            new DailyActivity { UserId = userId, Date = Today().AddDays(-1), MinutesLearned = 1 },
            new DailyActivity { UserId = userId, Date = Today().AddDays(-2), MinutesLearned = 1 }
        );
        ctx.SaveChanges();

        var streak = svc.ComputeStreak();

        streak.Should().Be(3);
    }

    [Fact]
    public void ComputeStreak_GapResetsToZero()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);
        var userId = svc.GetCurrentUserId();
        ctx.DailyActivities.AddRange(
            new DailyActivity { UserId = userId, Date = Today(), MinutesLearned = 1 },
            new DailyActivity { UserId = userId, Date = Today().AddDays(-2), MinutesLearned = 1 }
        );
        ctx.SaveChanges();

        var streak = svc.ComputeStreak();

        streak.Should().Be(1);
    }

    [Fact]
    public void ComputeStreak_OnlyYesterday_ReturnsOne()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);
        ctx.DailyActivities.Add(new DailyActivity
        {
            UserId = svc.GetCurrentUserId(),
            Date = Today().AddDays(-1),
            MinutesLearned = 1,
        });
        ctx.SaveChanges();

        var streak = svc.ComputeStreak();

        streak.Should().Be(1);
    }

    [Fact]
    public void ComputeStreak_ActivityEndsTwoDaysAgo_ReturnsZero()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);
        var userId = svc.GetCurrentUserId();
        ctx.DailyActivities.AddRange(
            new DailyActivity { UserId = userId, Date = Today().AddDays(-2), MinutesLearned = 1 },
            new DailyActivity { UserId = userId, Date = Today().AddDays(-3), MinutesLearned = 1 }
        );
        ctx.SaveChanges();

        var streak = svc.ComputeStreak();

        streak.Should().Be(0);
    }

    [Fact]
    public async Task RecordTimeAsync_WithNoExistingActivity_CreatesNewRecord()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);
        var userId = svc.GetCurrentUserId();

        await svc.RecordTimeAsync(10);

        var activity = ctx.DailyActivities.Single(a => a.UserId == userId);
        activity.MinutesLearned.Should().Be(10);
        activity.Date.Should().Be(Today());
    }

    [Fact]
    public async Task RecordTimeAsync_WithExistingActivity_AccumulatesMinutes()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);
        var userId = svc.GetCurrentUserId();
        ctx.DailyActivities.Add(new DailyActivity
        {
            UserId = userId,
            Date = Today(),
            MinutesLearned = 5,
        });
        ctx.SaveChanges();

        await svc.RecordTimeAsync(10);

        var activity = ctx.DailyActivities.Single(a => a.UserId == userId);
        activity.MinutesLearned.Should().Be(15);
    }

    [Fact]
    public async Task RecordTimeAsync_ZeroMinutes_DoesNothing()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);

        await svc.RecordTimeAsync(0);

        ctx.DailyActivities.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordTimeAsync_NegativeMinutes_DoesNothing()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);

        await svc.RecordTimeAsync(-5);

        ctx.DailyActivities.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordPageViewAsync_WithNoExistingActivity_CreatesNewRecord()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);
        var userId = svc.GetCurrentUserId();

        await svc.RecordPageViewAsync(3);

        var activity = ctx.DailyActivities.Single(a => a.UserId == userId);
        activity.PagesViewed.Should().Be(3);
        activity.Date.Should().Be(Today());
    }

    [Fact]
    public async Task RecordPageViewAsync_WithExistingActivity_AccumulatesPages()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);
        var userId = svc.GetCurrentUserId();
        ctx.DailyActivities.Add(new DailyActivity
        {
            UserId = userId,
            Date = Today(),
            PagesViewed = 2,
        });
        ctx.SaveChanges();

        await svc.RecordPageViewAsync(3);

        var activity = ctx.DailyActivities.Single(a => a.UserId == userId);
        activity.PagesViewed.Should().Be(5);
    }

    [Fact]
    public async Task GetTodayStats_NoActivity_ReturnsZeros()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);

        var (minutes, pages, decks, streak) = svc.GetTodayStats();

        minutes.Should().Be(0);
        pages.Should().Be(0);
        decks.Should().Be(0);
        streak.Should().Be(0);
    }

    [Fact]
    public async Task GetTodayStats_WithActivity_ReturnsCorrectValues()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);
        var userId = svc.GetCurrentUserId();
        ctx.DailyActivities.Add(new DailyActivity
        {
            UserId = userId,
            Date = Today(),
            MinutesLearned = 15,
            PagesViewed = 7,
        });
        ctx.UserProgress.Add(new UserProgress
        {
            UserId = userId,
            DeckId = Guid.NewGuid(),
            LastAccessedAt = DateTime.UtcNow,
        });
        ctx.SaveChanges();

        var (minutes, pages, decks, streak) = svc.GetTodayStats();

        minutes.Should().Be(15);
        pages.Should().Be(7);
        decks.Should().Be(1);
        streak.Should().Be(1);
    }

    [Fact]
    public void GetActivityHistory_ReturnsOrderedRecordsInWindow()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);
        var userId = svc.GetCurrentUserId();
        ctx.DailyActivities.AddRange(
            new DailyActivity { UserId = userId, Date = Today().AddDays(-5), MinutesLearned = 1 },
            new DailyActivity { UserId = userId, Date = Today().AddDays(-3), MinutesLearned = 1 },
            new DailyActivity { UserId = userId, Date = Today().AddDays(-1), MinutesLearned = 1 }
        );
        ctx.SaveChanges();

        var history = svc.GetActivityHistory(10);

        history.Should().HaveCount(3);
        history.Select(a => a.Date).Should().BeInAscendingOrder();
    }

    [Fact]
    public void GetActivityHistory_ExcludesRecordsOutsideWindow()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);
        var userId = svc.GetCurrentUserId();
        ctx.DailyActivities.AddRange(
            new DailyActivity { UserId = userId, Date = Today().AddDays(-400), MinutesLearned = 1 },
            new DailyActivity { UserId = userId, Date = Today().AddDays(-1), MinutesLearned = 1 }
        );
        ctx.SaveChanges();

        var history = svc.GetActivityHistory(30);

        history.Should().HaveCount(1);
        history[0].Date.Should().Be(Today().AddDays(-1));
    }

    [Fact]
    public void GetDecksCompleted_ReturnsCountOfCompletedProgress()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);
        var userId = svc.GetCurrentUserId();
        ctx.UserProgress.AddRange(
            new UserProgress { UserId = userId, DeckId = Guid.NewGuid(), IsCompleted = true },
            new UserProgress { UserId = userId, DeckId = Guid.NewGuid(), IsCompleted = true },
            new UserProgress { UserId = userId, DeckId = Guid.NewGuid(), IsCompleted = false }
        );
        ctx.SaveChanges();

        var completed = svc.GetDecksCompleted();

        completed.Should().Be(2);
    }

    [Fact]
    public void GetDecksCompleted_ReturnsZeroWhenNoneComplete()
    {
        var ctx = CreateContext(Guid.NewGuid().ToString());
        var svc = new UserService(ctx);

        var completed = svc.GetDecksCompleted();

        completed.Should().Be(0);
    }
}
