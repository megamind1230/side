using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NextLearn.Desktop.Data;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _context;
    private User? _currentUser;

    public UserService(AppDbContext context)
    {
        _context = context;
        EnsureGuestUser();
    }

    private void EnsureGuestUser()
    {
        _currentUser = _context.Users.FirstOrDefault(u => u.IsGuest);
        if (_currentUser == null)
        {
            _currentUser = new User
            {
                DisplayName = "Guest",
                IsGuest = true,
            };
            _context.Users.Add(_currentUser);
            _context.SaveChanges();
        }
    }

    public User GetCurrentUser() => _currentUser!;

    public Guid GetCurrentUserId() => _currentUser!.Id;

    public int ComputeStreak()
    {
        var today = Today();

        var activityDates = _context.DailyActivities
            .Where(a => a.UserId == _currentUser!.Id && a.MinutesLearned > 0)
            .Select(a => a.Date)
            .AsEnumerable()
            .Select(d => new DateTime(d.Year, d.Month, d.Day))
            .ToHashSet();

        if (activityDates.Count == 0)
        {
            return 0;
        }

        if (!activityDates.Contains(today) && !activityDates.Contains(today.AddDays(-1)))
        {
            return 0;
        }

        var streak = 0;
        var checkDate = activityDates.Contains(today) ? today : today.AddDays(-1);

        while (activityDates.Contains(checkDate))
        {
            streak++;
            checkDate = checkDate.AddDays(-1);
        }

        return streak;
    }

    private static DateTime Today() => new(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day);

    public async Task RecordTimeAsync(int minutes)
    {
        if (minutes <= 0)
        {
            return;
        }

        var today = Today();
        var activity = _context.DailyActivities
            .FirstOrDefault(a => a.UserId == _currentUser!.Id && a.Date == today);

        if (activity == null)
        {
            activity = new DailyActivity
            {
                UserId = _currentUser!.Id,
                Date = today,
                MinutesLearned = minutes,
            };
            _context.DailyActivities.Add(activity);
        }
        else
        {
            activity.MinutesLearned += minutes;
        }

        await _context.SaveChangesAsync().ConfigureAwait(false);
    }

    public (int minutes, int pages, int decks, int streak) GetTodayStats()
    {
        var today = Today();
        var activity = _context.DailyActivities
            .FirstOrDefault(a => a.UserId == _currentUser!.Id && a.Date == today);
        var decksToday = _context.UserProgress
            .Count(up => up.UserId == _currentUser!.Id
                      && up.LastAccessedAt.Date == today);

        return (
            activity?.MinutesLearned ?? 0,
            activity?.PagesViewed ?? 0,
            decksToday,
            ComputeStreak());
    }

    public async Task RecordPageViewAsync(int pagesViewed = 1)
    {
        var today = Today();

        var activity = _context.DailyActivities
            .FirstOrDefault(a => a.UserId == _currentUser!.Id && a.Date == today);

        if (activity == null)
        {
            activity = new DailyActivity
            {
                UserId = _currentUser!.Id,
                Date = today,
                PagesViewed = pagesViewed,
            };
            _context.DailyActivities.Add(activity);
        }
        else
        {
            activity.PagesViewed += pagesViewed;
        }

        await _context.SaveChangesAsync().ConfigureAwait(false);
    }

    public List<DailyActivity> GetActivityHistory(int days = 365)
    {
        var startDate = Today().AddDays(-days);
        return _context.DailyActivities
            .Where(a => a.UserId == _currentUser!.Id && a.Date >= startDate)
            .OrderBy(a => a.Date)
            .ToList();
    }

    public int GetDecksCompleted()
    {
        return _context.UserProgress
            .Count(up => up.UserId == _currentUser!.Id && up.IsCompleted);
    }
}
