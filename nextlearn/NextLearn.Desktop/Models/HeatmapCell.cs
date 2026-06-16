using System;
using System.ComponentModel;

namespace NextLearn.Desktop.Models;

public class HeatmapCell : INotifyPropertyChanged
{
    private int _count;

    public DateTime Date { get; init; }

    public int Count
    {
        get => _count;
        set
        {
            if (_count != value)
            {
                _count = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Count)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Color)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Tooltip)));
            }
        }
    }

    public int Row { get; init; }

    public int Col { get; init; }

    public string Color => Count switch
    {
        0 => "#1E293B",
        <= 5 => "#FED7AA",
        <= 15 => "#FDBA74",
        <= 30 => "#FB923C",
        <= 60 => "#EA580C",
        _ => "#C2410C",
    };

    public string Tooltip
    {
        get
        {
            var dateStr = Date.ToString("ddd, MMM d, yyyy");
            return Count == 0
                ? dateStr
                : $"{dateStr}: {Count}m";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
