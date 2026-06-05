using HeatMapStreak.Web.Models;

namespace HeatMapStreak.Web.Services;

public interface IHabitService
{
    Task<List<Habit>> GetUserHabitsAsync(string userId);
    Task<Habit?> GetHabitByIdAsync(int id, string userId);
    Task<Habit> CreateHabitAsync(Habit habit);
    Task<Habit> UpdateHabitAsync(Habit habit);
    Task DeleteHabitAsync(int id, string userId);
    Task<bool> ToggleDayAsync(int habitId, DateOnly date, string userId);
    Task<Dictionary<DateOnly, bool>> GetYearEntriesAsync(int habitId, int year, string userId);
    Task<Dictionary<DateOnly, int>> GetAggregatedYearDataAsync(string userId, int year);
}
