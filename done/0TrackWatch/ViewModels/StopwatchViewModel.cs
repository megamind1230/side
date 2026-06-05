using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackWatch.Services;

namespace TrackWatch.ViewModels;

public partial class StopwatchViewModel : ObservableObject
{
    [ObservableProperty]
    private string _taskName = "";

    [ObservableProperty]
    private string _elapsedTime = "00:00:00";

    [ObservableProperty]
    private bool _isRunning;

    private System.Timers.Timer? _timer;
    private DateTime _startTime;

    [RelayCommand]
    private void StartStopwatch()
    {
        if (string.IsNullOrWhiteSpace(TaskName))
            return;

        IsRunning = true;
        _startTime = DateTime.Now;

        _timer = new System.Timers.Timer(100);
        _timer.Elapsed += (s, e) => UpdateElapsed();
        _timer.Start();
    }

    private void UpdateElapsed()
    {
        var elapsed = DateTime.Now - _startTime;
        ElapsedTime = elapsed.ToString(@"hh\:mm\:ss");
    }

    [RelayCommand]
    private void StopAndSave()
    {
        _timer?.Stop();
        IsRunning = false;

        var elapsed = DateTime.Now - _startTime;
        var minutes = (int)elapsed.TotalMinutes;
        var seconds = elapsed.Seconds;

        TimeService.SaveEntry(TaskName, minutes, seconds);
    }

    [RelayCommand]
    private void Reset()
    {
        _timer?.Stop();
        IsRunning = false;
        ElapsedTime = "00:00:00";
    }
}
