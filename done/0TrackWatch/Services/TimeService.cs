using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrackWatch.Models;

namespace TrackWatch.Services;

public static class TimeService
{
    private static readonly string FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "trackwatch.log");

    public static List<TimeEntry> LoadEntries()
    {
        var entries = new List<TimeEntry>();
        if (!File.Exists(FileName))
            return entries;

        foreach (var line in File.ReadAllLines(FileName))
        {
            var parts = line.Split(',');
            if (parts.Length == 3 && int.TryParse(parts[1], out var minutes) && int.TryParse(parts[2], out var seconds))
            {
                entries.Add(new TimeEntry { Tag = parts[0], Minutes = minutes, Seconds = seconds });
            }
        }
        return entries;
    }

    public static void SaveEntry(string tag, int minutes, int seconds)
    {
        using var writer = new StreamWriter(FileName, append: true);
        writer.WriteLine($"{tag},{minutes},{seconds}");
    }

    public static void ResetAll()
    {
        if (File.Exists(FileName))
            File.Delete(FileName);
    }

    public static void DeleteEntry(string tag)
    {
        if (!File.Exists(FileName))
            return;

        var lines = File.ReadAllLines(FileName)
            .Where(line => !line.StartsWith(tag + ",")).ToArray();
        File.WriteAllLines(FileName, lines);
    }

    public static Dictionary<string, (int count, int totalMinutes, int totalSeconds)> GetStats()
    {
        var entries = LoadEntries();
        var stats = new Dictionary<string, (int, int, int)>();

        foreach (var entry in entries)
        {
            if (!stats.ContainsKey(entry.Tag))
                stats[entry.Tag] = (0, 0, 0);

            var (count, totalMin, totalSec) = stats[entry.Tag];
            stats[entry.Tag] = (count + 1, totalMin + entry.Minutes, totalSec + entry.Seconds);
        }

        return stats;
    }
}
