using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackWatch.Services;

namespace TrackWatch.ViewModels;

public partial class TimerViewModel : ObservableObject
{
    [ObservableProperty]
    private string _taskName = "";

    [ObservableProperty]
    private int _hours;

    [ObservableProperty]
    private int _minutes;

    [ObservableProperty]
    private int _seconds;

    [ObservableProperty]
    private string _remainingTime = "00:00:00";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isFinished;

    private System.Timers.Timer? _timer;
    private DateTime _endTime;

    partial void OnTaskNameChanged(string value)
    {
        IsFinished = false;
    }

    [RelayCommand]
    private void StartTimer()
    {
        if (string.IsNullOrWhiteSpace(TaskName))
            return;

        var totalSeconds = Hours * 3600 + Minutes * 60 + Seconds;
        if (totalSeconds <= 0)
            return;

        IsRunning = true;
        IsFinished = false;
        _endTime = DateTime.Now.AddSeconds(totalSeconds);

        _timer = new System.Timers.Timer(100);
        _timer.Elapsed += (s, e) => UpdateRemaining();
        _timer.Start();
    }

    private void UpdateRemaining()
    {
        var remaining = _endTime - DateTime.Now;
        if (remaining.TotalSeconds <= 0)
        {
            RemainingTime = "00:00:00";
            _timer?.Stop();
            IsRunning = false;
            IsFinished = true;

            var totalMin = Hours * 60 + Minutes;
            TimeService.SaveEntry(TaskName, totalMin, Seconds);
        }
        else
        {
            RemainingTime = remaining.ToString(@"hh\:mm\:ss");
        }
    }

    [RelayCommand]
    private void StopTimer()
    {
        _timer?.Stop();
        IsRunning = false;

        var elapsed = _endTime - DateTime.Now;
        var totalSec = (int)Math.Max(0, (Hours * 3600 + Minutes * 60 + Seconds) - elapsed.TotalSeconds);
        var min = totalSec / 60;
        var sec = totalSec % 60;

        TimeService.SaveEntry(TaskName, min, sec);
    }

    [RelayCommand]
    private void Reset()
    {
        _timer?.Stop();
        IsRunning = false;
        IsFinished = false;
        RemainingTime = "00:00:00";
        Hours = 0;
        Minutes = 0;
        Seconds = 0;
    }
}
