using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NextLearn.Desktop.Data;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public class UserService
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

    public async Task UpdateStreakAsync()
    {
        var today = DateTime.UtcNow.Date;
        var lastActive = _currentUser!.LastActiveDate.Date;

        if (lastActive == today)
        {
            return;
        }

        var daysDiff = (today - lastActive).Days;

        if (daysDiff == 1)
        {
            _currentUser.CurrentStreak++;
        }
        else if (daysDiff > 1)
        {
            _currentUser.CurrentStreak = 1;
        }

        _currentUser.LastActiveDate = today;

        var activity = _context.DailyActivities
            .FirstOrDefault(a => a.UserId == _currentUser.Id && a.Date == today);

        if (activity == null)
        {
            activity = new DailyActivity
            {
                UserId = _currentUser.Id,
                Date = today,
            };
            _context.DailyActivities.Add(activity);
        }

        await _context.SaveChangesAsync().ConfigureAwait(false);
    }

    public async Task RecordPageViewAsync(int pagesViewed = 1)
    {
        var today = DateTime.UtcNow.Date;

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
        await UpdateStreakAsync().ConfigureAwait(false);
    }

    public List<DailyActivity> GetActivityHistory(int days = 365)
    {
        var startDate = DateTime.UtcNow.Date.AddDays(-days);
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
