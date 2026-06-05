using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrackWatch.Services;

namespace TrackWatch.ViewModels;

public partial class StatsViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<StatItem> _stats = new();

    public StatsViewModel()
    {
        LoadStats();
    }

    [RelayCommand]
    private void LoadStats()
    {
        Stats.Clear();
        var data = TimeService.GetStats();

        foreach (var kvp in data)
        {
            var totalSec = kvp.Value.totalMinutes * 60 + kvp.Value.totalSeconds;
            var hours = totalSec / 3600;
            var minutes = (totalSec % 3600) / 60;
            var seconds = totalSec % 60;

            Stats.Add(new StatItem
            {
                Tag = kvp.Key,
                Count = kvp.Value.count,
                TotalTime = $"{hours:D2}:{minutes:D2}:{seconds:D2}"
            });
        }
    }

    [RelayCommand]
    private void ResetStats()
    {
        TimeService.ResetAll();
        LoadStats();
    }

    [RelayCommand]
    private void DeleteEntry(string tag)
    {
        TimeService.DeleteEntry(tag);
        LoadStats();
    }
}

public class StatItem
{
    public string Tag { get; set; } = "";
    public int Count { get; set; }
    public string TotalTime { get; set; } = "";
}
