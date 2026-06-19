using System.Collections.Concurrent;
using DotaDraftAdvisor.Models;
using DotaDraftAdvisor.Services;
using Serilog;
using Spectre.Console;

namespace DotaDraftAdvisor.Demo;

public class RaceConditionSection
{
    private readonly OpenDotaService _openDota;

    public RaceConditionSection(OpenDotaService openDota)
    {
        _openDota = openDota;
    }

    public async Task RunAsync(DraftInput input)
    {
        var heroes = await _openDota.GetHeroesAsync();
        var pickedIds = new HashSet<int>(input.AllyTeam.Concat(input.EnemyTeam));
        var candidates = heroes.Where(h => !pickedIds.Contains(h.Id)).ToList();

        Console.WriteLine();
        AnsiConsole.MarkupLine("[bold yellow]═══ RACE CONDITION DEMO ═══[/]");
        AnsiConsole.MarkupLine("We'll score the same candidates twice:");
        AnsiConsole.MarkupLine("  [red]First pass:[/] plain Dictionary + no locks → [bold]BROKEN[/]");
        AnsiConsole.MarkupLine("  [green]Second pass:[/] ConcurrentDictionary + Interlocked → [bold]FIXED[/]");
        Console.WriteLine();

        BrokenPass(candidates);
        FixedPass(candidates);

        AnsiConsole.MarkupLine("[bold yellow]═══ END RACE CONDITION DEMO ═══[/]");
        Console.WriteLine();
    }

    private void BrokenPass(List<Models.Hero> candidates)
    {
        AnsiConsole.MarkupLine("[red]--- Broken pass (no synchronization) ---[/]");

        int totalGamesBroken = 0;
        var brokenDict = new Dictionary<int, DraftScore>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            Parallel.ForEach(candidates, candidate =>
            {
                totalGamesBroken += 100;

                var score = new DraftScore
                {
                    HeroId = candidate.Id,
                    HeroName = candidate.LocalizedName,
                    Score = 50,
                    TotalGamesAnalyzed = 100,
                };

                lock (brokenDict)
                {
                    brokenDict[candidate.Id] = score;
                }
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Race condition demo — broken pass threw exception");
            AnsiConsole.MarkupLine($"[red]  EXCEPTION: {ex.GetType().Name}[/]");
        }

        sw.Stop();

        int expected = candidates.Count;

        AnsiConsole.MarkupLine($"  Expected count: [cyan]{expected}[/]");
        AnsiConsole.MarkupLine($"  Actual count:   [cyan]{brokenDict.Count}[/]");

        if (brokenDict.Count != expected)
        {
            AnsiConsole.MarkupLine($"  [red]  ❌ Count mismatch! Lost {expected - brokenDict.Count} entries[/]");
        }

        long expectedGames = (long)candidates.Count * 100;
        AnsiConsole.MarkupLine($"  Expected games: [cyan]{expectedGames}[/]");
        AnsiConsole.MarkupLine($"  Actual games:   [cyan]{totalGamesBroken}[/]");

        if (totalGamesBroken != expectedGames)
        {
            AnsiConsole.MarkupLine($"  [red]  ❌ Game count mismatch! Lost {expectedGames - totalGamesBroken} games[/]");
        }

        AnsiConsole.MarkupLine($"  Elapsed: {sw.ElapsedMilliseconds}ms\n");
    }

    private void FixedPass(List<Models.Hero> candidates)
    {
        AnsiConsole.MarkupLine("[green]--- Fixed pass (ConcurrentDictionary + Interlocked) ---[/]");

        int totalGamesFixed = 0;
        var fixedDict = new ConcurrentDictionary<int, DraftScore>();

        var sw = System.Diagnostics.Stopwatch.StartNew();

        Parallel.ForEach(candidates, candidate =>
        {
            Interlocked.Add(ref totalGamesFixed, 100);

            fixedDict[candidate.Id] = new DraftScore
            {
                HeroId = candidate.Id,
                HeroName = candidate.LocalizedName,
                Score = 50,
                TotalGamesAnalyzed = 100,
            };
        });

        sw.Stop();

        int expected = candidates.Count;
        long expectedGames = (long)candidates.Count * 100;

        AnsiConsole.MarkupLine($"  Expected count: [cyan]{expected}[/]");
        AnsiConsole.MarkupLine($"  Actual count:   [cyan]{fixedDict.Count}[/]");
        AnsiConsole.MarkupLine($"  Expected games: [cyan]{expectedGames}[/]");
        AnsiConsole.MarkupLine($"  Actual games:   [cyan]{totalGamesFixed}[/]");

        if (fixedDict.Count == expected && totalGamesFixed == expectedGames)
        {
            AnsiConsole.MarkupLine("  [green]  ✅ All counts match![/]");
        }

        AnsiConsole.MarkupLine($"  Elapsed: {sw.ElapsedMilliseconds}ms\n");

        AnsiConsole.MarkupLine("[italic]Why? ConcurrentDictionary uses fine-grained locking so multiple[/]");
        AnsiConsole.MarkupLine("[italic]threads can read/write different buckets simultaneously.[/]");
        AnsiConsole.MarkupLine("[italic]Interlocked.Add is atomic at the CPU level — no torn reads/writes.[/]");
        Console.WriteLine();
    }
}
