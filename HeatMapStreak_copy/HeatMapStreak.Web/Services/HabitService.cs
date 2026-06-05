using HeatMapStreak.Web.Data;
using HeatMapStreak.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace HeatMapStreak.Web.Services;

public class HabitService(ApplicationDbContext db) : IHabitService
{
    public async Task<List<Habit>> GetUserHabitsAsync(string userId)
    {
        return await db.Habits
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync();
    }

    public async Task<Habit?> GetHabitByIdAsync(int id, string userId)
    {
        return await db.Habits
            .Include(h => h.DayEntries)
            .FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);
    }

    public async Task<Habit> CreateHabitAsync(Habit habit)
    {
        db.Habits.Add(habit);
        await db.SaveChangesAsync();
        return habit;
    }

    public async Task<Habit> UpdateHabitAsync(Habit habit)
    {
        db.Habits.Update(habit);
        await db.SaveChangesAsync();
        return habit;
    }

    public async Task DeleteHabitAsync(int id, string userId)
    {
        var habit = await db.Habits.FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);
        if (habit is not null)
        {
            db.Habits.Remove(habit);
            await db.SaveChangesAsync();
        }
    }

    public async Task<bool> ToggleDayAsync(int habitId, DateOnly date, string userId)
    {
        var habit = await db.Habits.FirstOrDefaultAsync(h => h.Id == habitId && h.UserId == userId);
        if (habit is null) return false;

        var entry = await db.DayEntries.FindAsync(habitId, date);
        if (entry is null)
        {
            db.DayEntries.Add(new DayEntry
            {
                HabitId = habitId,
                Date = date,
                IsCompleted = true
            });
        }
        else
        {
            entry.IsCompleted = !entry.IsCompleted;
        }

        await db.SaveChangesAsync();
        return true;
    }

    public async Task<Dictionary<DateOnly, bool>> GetYearEntriesAsync(int habitId, int year, string userId)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);

        return await db.DayEntries
            .Where(d => d.HabitId == habitId && d.Habit.UserId == userId
                        && d.Date >= start && d.Date <= end)
            .ToDictionaryAsync(d => d.Date, d => d.IsCompleted);
    }

    public async Task<Dictionary<DateOnly, int>> GetAggregatedYearDataAsync(string userId, int year)
    {
        var start = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);

        return await db.DayEntries
            .Where(d => d.Habit.UserId == userId && d.IsCompleted
                        && d.Date >= start && d.Date <= end)
            .GroupBy(d => d.Date)
            .ToDictionaryAsync(g => g.Key, g => g.Count());
    }
}
