using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

public interface IUserService
{
    User GetCurrentUser();

    Guid GetCurrentUserId();

    int ComputeStreak();

    Task RecordPageViewAsync(int pagesViewed = 1);

    Task RecordTimeAsync(int minutes);

    (int minutes, int pages, int decks, int streak) GetTodayStats();

    List<DailyActivity> GetActivityHistory(int days = 365);

    int GetDecksCompleted();
}
