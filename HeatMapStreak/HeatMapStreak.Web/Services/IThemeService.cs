using Microsoft.AspNetCore.Components;

namespace HeatMapStreak.Web.Services;

public interface IThemeService
{
    Task InitializeAsync();
    Task SetThemeAsync(bool isDark);
    bool IsDark { get; }
    event Action? OnThemeChanged;
}