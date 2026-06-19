using System.Collections.Concurrent;
using DotaDraftAdvisor.Models;
using Serilog;

namespace DotaDraftAdvisor.Services;

public class DraftScorer
{
    private readonly OpenDotaService _openDota;

    private const int MaxCandidates = 25;

    private static readonly string[] RoleCategories =
        ["Carry", "Support", "Initiator", "Nuker", "Durable", "Disabler", "Escape", "Pusher"];

    private static readonly Dictionary<string, string> RoleToLane = new()
    {
        { "Carry", "safe" },
        { "Support", "support" },
        { "Initiator", "off" },
        { "Nuker", "mid" },
        { "Durable", "off" },
        { "Disabler", "support" },
        { "Escape", "mid" },
        { "Pusher", "safe" },
    };

    public DraftScorer(OpenDotaService openDota)
    {
        _openDota = openDota;
    }

    public async Task<List<DraftScore>> ScoreAsync(DraftInput input)
    {
        var heroes = await _openDota.GetHeroesAsync();
        var pickedIds = new HashSet<int>(input.AllyTeam.Concat(input.EnemyTeam));
        var candidates = heroes.Where(h => !pickedIds.Contains(h.Id))
            .Take(MaxCandidates)
            .ToList();

        Log.Information("DraftScorer: {Total} candidates from {All} heroes (9 picked)",
            candidates.Count, heroes.Count);

        var allyRoles = GetCoveredRoles(heroes.Where(h => input.AllyTeam.Contains(h.Id)).ToList());
        var allyLanes = GetCoveredLanes(heroes.Where(h => input.AllyTeam.Contains(h.Id)).ToList());

        var pickedHeroes = heroes.Where(h => pickedIds.Contains(h.Id)).ToList();
        var matchups = await PrefetchMatchupsAsync(pickedHeroes.Concat(candidates).ToList());

        var results = new ConcurrentDictionary<int, DraftScore>();
        int totalGamesAnalyzed = 0;
        int processedCount = 0;

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        Parallel.ForEach(candidates, candidate =>
        {
            var candidateMatchups = matchups.TryGetValue(candidate.Id, out var m) ? m : [];
            var score = ScoreCandidate(candidate, input, heroes, allyRoles, allyLanes, candidateMatchups);
            results[candidate.Id] = score;

            Interlocked.Add(ref totalGamesAnalyzed, score.TotalGamesAnalyzed);
            Interlocked.Increment(ref processedCount);
        });

        stopwatch.Stop();

        Log.Information("DraftScorer: scored {Count} heroes on {Cores} cores in {Elapsed}ms | total games analyzed: {Games}",
            processedCount, Environment.ProcessorCount, stopwatch.ElapsedMilliseconds, totalGamesAnalyzed);

        var sorted = results.Values
            .AsParallel()
            .OrderByDescending(s => s.Score)
            .ToList();

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                RunDeepDive(sorted.Take(5).ToList(), heroes);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Deep-dive analysis failed");
            }
        });

        return sorted;
    }

    private async Task<Dictionary<int, List<HeroMatchup>>> PrefetchMatchupsAsync(List<Models.Hero> candidates)
    {
        var matchups = new Dictionary<int, List<HeroMatchup>>();
        int total = candidates.Count;
        int fetched = 0;

        Log.Information("DraftScorer: pre-fetching matchups for {Count} heroes", total);

        using var semaphore = new SemaphoreSlim(1, 1);
        using var cts = new CancellationTokenSource();

        var tasks = candidates.Select(async candidate =>
        {
            await semaphore.WaitAsync(cts.Token);
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var data = await _openDota.GetMatchupsAsync(candidate.Id);
                sw.Stop();

                lock (matchups)
                {
                    matchups[candidate.Id] = data;
                    fetched++;
                }

                    if (fetched < total && sw.ElapsedMilliseconds > 100)
                        await Task.Delay(200, cts.Token);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(120), cts.Token);
        var allTasks = Task.WhenAll(tasks);
        var completed = await Task.WhenAny(allTasks, timeoutTask);

        if (completed == timeoutTask)
        {
            Log.Warning("DraftScorer: matchup pre-fetch timed out after 120s");
        }

        Log.Information("DraftScorer: pre-fetched {Count}/{Total} matchup sets", fetched, total);
        return matchups;
    }

    private static DraftScore ScoreCandidate(
        Models.Hero candidate,
        DraftInput input,
        List<Models.Hero> allHeroes,
        HashSet<string> allyRoles,
        HashSet<string> allyLanes,
        List<HeroMatchup> matchups)
    {
        double totalWinRate = 0;
        int totalGames = 0;
        var reasoning = new List<string>();

        foreach (var enemyId in input.EnemyTeam)
        {
            var vs = matchups.FirstOrDefault(m => m.HeroId == enemyId);
            if (vs != null && vs.GamesPlayed > 0)
            {
                double shrunk = (vs.Wins + 50.0) / (vs.GamesPlayed + 100.0);
                totalWinRate += shrunk;
                totalGames += vs.GamesPlayed;

                var enemyName = allHeroes.FirstOrDefault(h => h.Id == enemyId)?.LocalizedName ?? $"#{enemyId}";
                reasoning.Add($"vs {enemyName}: {(shrunk * 100):F1}% WR ({vs.Wins}/{vs.GamesPlayed} games)");
            }
        }

        double avgWinRate = input.EnemyTeam.Length > 0 ? totalWinRate / input.EnemyTeam.Length : 0;

        double roleScore = ComputeRoleCoverage(candidate, allyRoles, reasoning);
        double laneScore = ComputeLaneFit(candidate, allyLanes, reasoning);

        double finalScore = (avgWinRate * 0.55 + roleScore * 0.25 + laneScore * 0.20) * 100;

        return new DraftScore
        {
            HeroId = candidate.Id,
            HeroName = candidate.LocalizedName,
            Score = Math.Clamp(finalScore, 0, 100),
            AvgWinRate = avgWinRate,
            TotalGamesAnalyzed = totalGames,
            Reasoning = reasoning.ToArray(),
        };
    }

    private static double ComputeRoleCoverage(Models.Hero candidate, HashSet<string> allyRoles, List<string> reasoning)
    {
        double bonus = 0;
        var missingRoles = RoleCategories.Where(r => !allyRoles.Contains(r)).ToList();

        foreach (var needed in missingRoles)
        {
            if (candidate.Roles.Any(r => r.Equals(needed, StringComparison.OrdinalIgnoreCase)))
            {
                bonus += 0.15;
                reasoning.Add($"Fills missing role: {needed}");
            }
        }

        return Math.Min(bonus, 0.6);
    }

    private static double ComputeLaneFit(Models.Hero candidate, HashSet<string> allyLanes, List<string> reasoning)
    {
        var primaryLanes = candidate.Roles
            .Where(r => RoleToLane.ContainsKey(r))
            .Select(r => RoleToLane[r])
            .Distinct()
            .ToList();

        foreach (var lane in primaryLanes)
        {
            if (!allyLanes.Contains(lane))
            {
                reasoning.Add($"Fits open lane: {lane}");
                return 0.10;
            }
        }

        return 0;
    }

    private static HashSet<string> GetCoveredRoles(List<Models.Hero> allies)
    {
        var roles = new HashSet<string>();
        foreach (var ally in allies)
            foreach (var role in ally.Roles)
                roles.Add(role);
        return roles;
    }

    private static HashSet<string> GetCoveredLanes(List<Models.Hero> allies)
    {
        var lanes = new HashSet<string>();
        foreach (var ally in allies)
            foreach (var role in ally.Roles)
                if (RoleToLane.TryGetValue(role, out var lane))
                    lanes.Add(lane);
        return lanes;
    }

    private void RunDeepDive(List<DraftScore> top5, List<Models.Hero> allHeroes)
    {
        var logsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "magnus", "DotaDraftAdvisor", "logs");

        foreach (var score in top5)
        {
            var hero = allHeroes.FirstOrDefault(h => h.Id == score.HeroId);
            var fileName = Path.Combine(logsDir, $"deepdive-{hero?.LocalizedName ?? score.HeroName}.txt");
            File.WriteAllText(fileName,
                $"=== Deep Dive: {score.HeroName} ===\n" +
                $"Score: {score.Score:F1}/100\n" +
                $"Avg Win Rate: {score.AvgWinRate:P1}\n" +
                $"Total Games Analyzed: {score.TotalGamesAnalyzed:N0}\n" +
                $"Roles: {string.Join(", ", hero?.Roles ?? [])}\n" +
                $"Reasoning:\n  {string.Join("\n  ", score.Reasoning)}\n");
            Log.Information("Deep-dive written: {File}", fileName);
        }
    }
}
