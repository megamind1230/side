using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NextLearn.Desktop.Models;

namespace NextLearn.Desktop.Services;

/// <summary>Manages the current user's identity, activity tracking, and streak computation.</summary>
public interface IUserService
{
    /// <summary>Returns the current (guest) user.</summary>
    /// <returns>The current user.</returns>
    User GetCurrentUser();

    /// <summary>Returns the current user's ID.</summary>
    /// <returns>The user's GUID.</returns>
    Guid GetCurrentUserId();

    /// <summary>Computes the study streak based on consecutive days with minutes learned.</summary>
    /// <returns>The current streak count.</returns>
    int ComputeStreak();

    /// <summary>Records a page view for today's activity.</summary>
    /// <param name="pagesViewed">Number of pages viewed (default 1).</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RecordPageViewAsync(int pagesViewed = 1);

    /// <summary>Records learned minutes for today's activity.</summary>
    /// <param name="minutes">Minutes to record.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RecordTimeAsync(int minutes);

    /// <summary>Returns today's stats: minutes, pages, decks studied, and streak.</summary>
    /// <returns>A tuple of (minutes, pages, decks, streak).</returns>
    (int minutes, int pages, int decks, int streak) GetTodayStats();

    /// <summary>Returns daily activity records within the given lookback window.</summary>
    /// <param name="days">Number of days to look back (default 365).</param>
    /// <returns>Ordered list of daily activity records.</returns>
    List<DailyActivity> GetActivityHistory(int days = 365);

    /// <summary>Returns the count of completed decks for the current user.</summary>
    /// <returns>Number of completed decks.</returns>
    int GetDecksCompleted();
}