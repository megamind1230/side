namespace HeatMapStreak.Web.Services;

public class StreakInfo
{
    public int CurrentStreak { get; set; }
    public int LongestStreak { get; set; }
}

public interface IStreakService
{
    Task<StreakInfo> GetStreakAsync(int habitId, string userId);
}
