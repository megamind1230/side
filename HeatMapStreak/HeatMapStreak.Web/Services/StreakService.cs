using HeatMapStreak.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace HeatMapStreak.Web.Services;

public class StreakService(ApplicationDbContext db) : IStreakService
{
    public async Task<StreakInfo> GetStreakAsync(int habitId, string userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var habitExists = await db.Habits.AnyAsync(h => h.Id == habitId && h.UserId == userId);
        if (!habitExists) return new StreakInfo();

        var completedDates = await db.DayEntries
            .Where(d => d.HabitId == habitId && d.IsCompleted)
            .Select(d => d.Date)
            .OrderByDescending(d => d)
            .ToListAsync();

        if (completedDates.Count == 0) return new StreakInfo();

        var currentStreak = 0;
        var checkDate = today;

        if (completedDates.Contains(today) || completedDates.Contains(today.AddDays(-1)))
        {
            if (!completedDates.Contains(today))
                checkDate = today.AddDays(-1);

            while (completedDates.Contains(checkDate))
            {
                currentStreak++;
                checkDate = checkDate.AddDays(-1);
            }
        }

        var longestStreak = 0;
        var tempStreak = 1;
        var sorted = completedDates.OrderBy(d => d).ToList();

        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].DayNumber == sorted[i - 1].DayNumber + 1)
            {
                tempStreak++;
            }
            else
            {
                longestStreak = Math.Max(longestStreak, tempStreak);
                tempStreak = 1;
            }
        }
        longestStreak = Math.Max(longestStreak, tempStreak);

        return new StreakInfo
        {
            CurrentStreak = currentStreak,
            LongestStreak = longestStreak
        };
    }
}
