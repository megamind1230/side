using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TrackWatch.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public TimerViewModel Timer { get; } = new();
    public StopwatchViewModel Stopwatch { get; } = new();
    public StatsViewModel Stats { get; } = new();

    [ObservableProperty]
    private object? _currentView;

    public MainWindowViewModel()
    {
        CurrentView = Timer;
    }

    [RelayCommand]
    private void ShowTimer() => CurrentView = Timer;

    [RelayCommand]
    private void ShowStopwatch() => CurrentView = Stopwatch;

    [RelayCommand]
    private void ShowStats() => CurrentView = Stats;
}
