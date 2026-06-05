using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace HeatMapStreak.Web.Services;

public class ThemeService : IThemeService
{
    private readonly IJSRuntime _js;
    private bool _isDark = true;
    private bool _initialized;

    public bool IsDark => _isDark;
    public event Action? OnThemeChanged;

    public ThemeService(IJSRuntime js)
    {
        _js = js;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        try
        {
            var theme = await _js.InvokeAsync<string>("getTheme");
            _isDark = theme == "dark";
            _initialized = true;
        }
        catch
        {
            _isDark = true;
        }
    }

    public async Task SetThemeAsync(bool isDark)
    {
        _isDark = isDark;
        try
        {
            await _js.InvokeVoidAsync("applyTheme", isDark ? "dark" : "light");
            OnThemeChanged?.Invoke();
        }
        catch
        {
        }
    }
}