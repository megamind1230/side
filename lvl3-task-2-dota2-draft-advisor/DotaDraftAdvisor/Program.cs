using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Spectre.Console;
using DotaDraftAdvisor.Models;
using DotaDraftAdvisor.Services;
using DotaDraftAdvisor.Demo;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "magnus", "DotaDraftAdvisor", "logs", "app-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .WriteTo.File(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "magnus", "DotaDraftAdvisor", "logs", "watcher-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);
    builder.Services.AddHttpClient();
    builder.Services.AddSingleton<AliasService>();
    builder.Services.AddSingleton<OpenDotaService>();
    builder.Services.AddSingleton<DraftScorer>();
    builder.Services.AddSingleton<RaceConditionSection>();
    builder.Services.AddSingleton<LiveDraftWatcher>();
    builder.Services.AddSingleton<DeadlockSection>();
    var host = builder.Build();

    var aliasService = host.Services.GetRequiredService<AliasService>();
    var openDotaService = host.Services.GetRequiredService<OpenDotaService>();
    var draftScorer = host.Services.GetRequiredService<DraftScorer>();
    var raceCondition = host.Services.GetRequiredService<RaceConditionSection>();
    var liveWatcher = host.Services.GetRequiredService<LiveDraftWatcher>();
    var deadlockDemo = host.Services.GetRequiredService<DeadlockSection>();

    if (args.Length > 1 && args[0] == "--demo" && args[1] == "deadlock")
    {
        deadlockDemo.Run();
        return;
    }

    Log.Information("DotaDraftAdvisor starting up");
    var heroes = await openDotaService.GetHeroesAsync();

    while (true)
    {
        AnsiConsole.Clear();
        AnsiConsole.Write(new FigletText("DotaDraftAdvisor").Color(Color.Blue));
        AnsiConsole.MarkupLine("[dim]Pick your draft. See the score. Counter-pick like a pro.[/]");
        AnsiConsole.WriteLine();

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to do?")
                .PageSize(10)
                .AddChoices([
                    "Analyze a draft",
                    "Race condition demo",
                    "Live draft watcher",
                    "Deadlock demo",
                    "Exit"
                ]));

        switch (choice)
        {
            case "Analyze a draft":
                await AnalyzeDraftAsync(heroes, aliasService, openDotaService, draftScorer);
                break;
            case "Race condition demo":
                await RunRaceConditionDemoAsync(heroes, aliasService, raceCondition);
                break;
            case "Live draft watcher":
                await RunLiveWatcherAsync(heroes, openDotaService, draftScorer, liveWatcher);
                break;
            case "Deadlock demo":
                deadlockDemo.Run();
                PromptBack();
                break;
            case "Exit":
                Log.Information("DotaDraftAdvisor shutting down");
                return;
        }
    }
}
catch (Exception ex)
{
    Log.Fatal(ex, "DotaDraftAdvisor terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static void PromptBack()
{
    AnsiConsole.Markup("\nPress any key to return...");
    Console.ReadKey(true);
}

static int[] PromptHeroTeam(string prompt, List<Hero> heroes, AliasService aliasService, int expectedCount)
{
    var sortedHeroes = heroes.OrderBy(h => h.LocalizedName, StringComparer.OrdinalIgnoreCase).ToList();

    // Show a compact 3-column hero reference table
    var heroTable = new Table().Border(TableBorder.Minimal).HideHeaders();
    heroTable.AddColumn("Col1");
    heroTable.AddColumn("Col2");
    heroTable.AddColumn("Col3");

    int cols = 3;
    int rows = (int)Math.Ceiling((double)sortedHeroes.Count / cols);
    for (int r = 0; r < rows; r++)
    {
        var c1 = $"{r + 1,3}. {sortedHeroes[r].LocalizedName.EscapeMarkup()}";
        var c2 = r + rows < sortedHeroes.Count
            ? $"{r + rows + 1,3}. {sortedHeroes[r + rows].LocalizedName.EscapeMarkup()}"
            : "";
        var c3 = r + rows * 2 < sortedHeroes.Count
            ? $"{r + rows * 2 + 1,3}. {sortedHeroes[r + rows * 2].LocalizedName.EscapeMarkup()}"
            : "";
        heroTable.AddRow(c1, c2, c3);
    }

    AnsiConsole.Write(heroTable);
    AnsiConsole.WriteLine();

    while (true)
    {
        var input = AnsiConsole.Ask<string>($"[bold]{prompt} (numbers or names)[/]");
        var tokens = input.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]Please enter at least one hero number or name.[/]");
            continue;
        }

        var resolvedIds = new List<int>();
        var hadError = false;

        foreach (var token in tokens)
        {
            // Try number first
            if (int.TryParse(token, out var num) && num >= 1 && num <= sortedHeroes.Count)
            {
                resolvedIds.Add(sortedHeroes[num - 1].Id);
                continue;
            }

            // Fall back to alias resolution
            var ids = aliasService.Resolve(token, heroes);
            if (ids.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]Could not resolve '{token}' to any hero.[/]");
                hadError = true;
                break;
            }

            if (ids.Count > 1)
            {
                var heroNames = ids
                    .Select(id => heroes.First(h => h.Id == id).LocalizedName)
                    .ToList();
                var picked = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title($"Multiple matches for '[yellow]{token}[/]' — pick one:")
                        .PageSize(10)
                        .AddChoices(heroNames));
                ids = ids
                    .Where(id => heroes.First(h => h.Id == id).LocalizedName == picked)
                    .ToList();
            }

            resolvedIds.Add(ids[0]);
        }

        if (hadError) continue;

        var distinct = resolvedIds.Distinct().ToList();
        if (distinct.Count != resolvedIds.Count)
        {
            AnsiConsole.MarkupLine("[red]Duplicate heroes within the same team.[/]");
            continue;
        }

        if (distinct.Count != expectedCount)
        {
            AnsiConsole.MarkupLine($"[red]Expected {expectedCount} heroes, but entered {distinct.Count}.[/]");
            continue;
        }

        var names = distinct
            .Select(id => heroes.First(h => h.Id == id).LocalizedName.EscapeMarkup());
        AnsiConsole.MarkupLine($"[cyan]{string.Join("[/], [cyan]", names)}[/]");

        if (AnsiConsole.Confirm("Is this correct?", true))
            return distinct.ToArray();
    }
}

static async Task AnalyzeDraftAsync(
    List<Hero> heroes,
    AliasService aliasService,
    OpenDotaService openDotaService,
    DraftScorer draftScorer)
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold yellow]═══ DRAFT ANALYSIS ═══[/]");
    AnsiConsole.WriteLine();

    var allyIds = PromptHeroTeam("Enter your team (4 heroes, comma-separated):", heroes, aliasService, 4);
    var enemyIds = PromptHeroTeam("Enter enemy team (5 heroes, comma-separated):", heroes, aliasService, 5);

    var overlap = allyIds.Intersect(enemyIds).ToList();
    if (overlap.Count > 0)
    {
        var names = overlap
            .Select(id => heroes.First(h => h.Id == id).LocalizedName.EscapeMarkup());
        AnsiConsole.MarkupLine($"[red]Heroes cannot be in both teams: {string.Join(", ", names)}[/]");
        PromptBack();
        return;
    }

    var input = new DraftInput
    {
        AllyTeam = allyIds,
        EnemyTeam = enemyIds,
    };

    var allyNames = allyIds.Select(id => heroes.First(h => h.Id == id).LocalizedName);
    var enemyNames = enemyIds.Select(id => heroes.First(h => h.Id == id).LocalizedName);
    Log.Information("Analyzing draft: Ally=[{Ally}] Enemy=[{Enemy}]",
        string.Join(",", allyNames), string.Join(",", enemyNames));

    AnsiConsole.MarkupLine("\n[bold green]Fetching matchup data and scoring candidates...[/]");

    var results = await draftScorer.ScoreAsync(input);

    if (results.Count == 0)
    {
        AnsiConsole.MarkupLine("[red]No results returned. The API may be unavailable.[/]");
        PromptBack();
        return;
    }

    AnsiConsole.WriteLine();
    DisplayCandidateTable(results, heroes, input);

    AnsiConsole.WriteLine();
    var alreadyCovered = GetCoveredRoles(heroes.Where(h => allyIds.Contains(h.Id)).ToList());
    string[] roleCategories =
        ["Carry", "Support", "Initiator", "Nuker", "Durable", "Disabler", "Escape", "Pusher"];
    var missingRoles = roleCategories.Where(r => !alreadyCovered.Contains(r)).ToList();
    if (missingRoles.Count > 0)
    {
        var missingEscaped = missingRoles.Select(r => r.EscapeMarkup());
        AnsiConsole.MarkupLine($"[yellow]Your team lacks:[/] [bold]{string.Join(", ", missingEscaped)}[/]");

        var fillers = results
            .Where(r => r.Reasoning.Any(rs => rs.StartsWith("Fills missing role")))
            .Take(3)
            .Select(r => r.HeroName.EscapeMarkup());
        if (fillers.Any())
        {
            AnsiConsole.MarkupLine($"[cyan]{string.Join("[/], [cyan]", fillers)}[/]");
        }
    }

    await ShowDeepDiveAsync(results, heroes, openDotaService);

    PromptBack();
}

static void DisplayCandidateTable(List<DraftScore> results, List<Hero> heroes, DraftInput input)
{
    var table = new Table().Border(TableBorder.Rounded);
    table.AddColumn(new TableColumn("#").Centered());
    table.AddColumn("Hero");
    table.AddColumn(new TableColumn("Score").RightAligned());
    table.AddColumn(new TableColumn("Win Rate").RightAligned());
    table.AddColumn(new TableColumn("Games").RightAligned());
    table.AddColumn(new TableColumn("Reasoning").Width(50));

    var top10 = results.Take(10).ToList();

    for (int i = 0; i < top10.Count; i++)
    {
        var r = top10[i];
        var color = r.Score >= 80 ? "green" : r.Score >= 60 ? "yellow" : "red";
        var reasonText = string.Join("\n", r.Reasoning.Take(3));

        table.AddRow(
            new Markup($"#{i + 1}"),
            new Markup($"[{color}]{r.HeroName.EscapeMarkup()}[/]"),
            new Markup($"[{color}]{r.Score:F1}[/]"),
            new Markup($"{r.AvgWinRate:P1}"),
            new Markup($"{r.TotalGamesAnalyzed:N0}"),
            new Markup(reasonText.EscapeMarkup() ?? "")
        );
    }

    AnsiConsole.Write(table);
}

static async Task ShowDeepDiveAsync(
    List<DraftScore> results,
    List<Hero> heroes,
    OpenDotaService openDotaService)
{
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Enter a hero number for detailed analysis, or 0 to go back:[/]");

    var input = AnsiConsole.Ask<string>("> ");

    if (!int.TryParse(input, out var num) || num < 1 || num > results.Count)
        return;

    var selected = results[num - 1];
    var hero = heroes.FirstOrDefault(h => h.Id == selected.HeroId);

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[bold yellow]═══ DEEP DIVE: {selected.HeroName.EscapeMarkup()} ═══[/]");

    var infoTable = new Table().Border(TableBorder.Minimal);
    infoTable.AddColumn("Property");
    infoTable.AddColumn("Value");
    infoTable.AddRow("Hero", selected.HeroName);
    infoTable.AddRow("Score", $"{selected.Score:F1}/100");
    infoTable.AddRow("Avg Win Rate", $"{selected.AvgWinRate:P1}");
    infoTable.AddRow("Total Games Analyzed", $"{selected.TotalGamesAnalyzed:N0}");
    infoTable.AddRow("Roles", hero != null ? string.Join(", ", hero.Roles) : "N/A");
    infoTable.AddRow("Primary Attribute", hero?.PrimaryAttr ?? "N/A");
    infoTable.AddRow("Attack Type", hero?.AttackType ?? "N/A");
    AnsiConsole.Write(infoTable);

    var matchups = await openDotaService.GetMatchupsAsync(selected.HeroId);
    var validMatchups = matchups
        .Where(m => m.GamesPlayed > 0)
        .OrderByDescending(m => m.WinRate)
        .ToList();

    if (validMatchups.Count > 0)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Top Matchups:[/]");

        var muTable = new Table().Border(TableBorder.Minimal);
        muTable.AddColumn("vs Enemy");
        muTable.AddColumn(new TableColumn("Win Rate").RightAligned());
        muTable.AddColumn(new TableColumn("Wins").RightAligned());
        muTable.AddColumn(new TableColumn("Games").RightAligned());

        foreach (var mu in validMatchups.Take(10))
        {
            var enemyName = heroes.FirstOrDefault(h => h.Id == mu.HeroId)?.LocalizedName ?? $"#{mu.HeroId}";
            var wrColor = mu.WinRate >= 0.55 ? "green" : mu.WinRate >= 0.45 ? "yellow" : "red";
            muTable.AddRow(
                new Text(enemyName),
                new Markup($"[{wrColor}]{mu.WinRate:P1}[/]"),
                new Markup($"{mu.Wins}"),
                new Markup($"{mu.GamesPlayed:N0}"));
        }

        AnsiConsole.Write(muTable);
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Reasoning:[/]");
    foreach (var reason in selected.Reasoning)
    {
        AnsiConsole.MarkupLine($"  • {reason.EscapeMarkup()}");
    }
}

static async Task RunRaceConditionDemoAsync(
    List<Hero> heroes,
    AliasService aliasService,
    RaceConditionSection raceCondition)
{
    var allyIds = PromptHeroTeam("Enter your team (4 heroes, comma-separated):", heroes, aliasService, 4);
    var enemyIds = PromptHeroTeam("Enter enemy team (5 heroes, comma-separated):", heroes, aliasService, 5);

    var input = new DraftInput { AllyTeam = allyIds, EnemyTeam = enemyIds };
    await raceCondition.RunAsync(input);
    PromptBack();
}

static async Task RunLiveWatcherAsync(
    List<Hero> heroes,
    OpenDotaService openDotaService,
    DraftScorer draftScorer,
    LiveDraftWatcher watcher)
{
    AnsiConsole.Clear();
    AnsiConsole.MarkupLine("[bold yellow]═══ LIVE DRAFT WATCHER ═══[/]");
    AnsiConsole.MarkupLine("[dim]Polling for live draft matches every 5s. Press 0 to stop and pick a match.[/]");
    AnsiConsole.WriteLine();

    using var cts = new CancellationTokenSource();
    var watcherTask = watcher.RunAsync(cts.Token);

    while (!cts.IsCancellationRequested)
    {
        var matches = watcher.GetBufferedMatches();

        if (matches.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold underline]Available matches:[/]");
            foreach (var m in matches)
            {
                var rNames = m.RadiantPicks
                    .Select(id => heroes.FirstOrDefault(h => h.Id == id)?.LocalizedName ?? $"#{id}");
                var dNames = m.DirePicks
                    .Select(id => heroes.FirstOrDefault(h => h.Id == id)?.LocalizedName ?? $"#{id}");
                AnsiConsole.MarkupLine(
                    $"  [cyan]#{m.Index}[/] [yellow]{m.Match.TeamNameRadiant.EscapeMarkup()}[/] ({string.Join(", ", rNames)}) vs [yellow]{m.Match.TeamNameDire.EscapeMarkup()}[/] ({string.Join(", ", dNames)})");
            }

            AnsiConsole.WriteLine();
        }

        AnsiConsole.Markup("[bold]Enter match # to analyze, or [dim]0[/] to stop & pick, or [dim]q[/] to quit: [/]");

        var inputTask = Task.Run(() => Console.ReadLine()?.Trim() ?? "");
        var timeoutTask = Task.Delay(10_000, cts.Token);
        var completed = await Task.WhenAny(inputTask, timeoutTask);

        if (completed == timeoutTask || cts.IsCancellationRequested)
            continue;

        var input = await inputTask;

        if (input == "0")
        {
            cts.Cancel();
            try { await watcherTask; }
            catch (OperationCanceledException) { }

            var buffered = watcher.GetBufferedMatches();
            if (buffered.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]No matches found during this session.[/]");
                break;
            }

            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold yellow]═══ BUFFERED MATCHES ═══[/]");
            foreach (var m in buffered)
            {
                var rNames = m.RadiantPicks
                    .Select(id => heroes.FirstOrDefault(h => h.Id == id)?.LocalizedName ?? $"#{id}");
                var dNames = m.DirePicks
                    .Select(id => heroes.FirstOrDefault(h => h.Id == id)?.LocalizedName ?? $"#{id}");
                AnsiConsole.MarkupLine(
                    $"  [cyan]#{m.Index}[/] [yellow]{m.Match.TeamNameRadiant.EscapeMarkup()}[/] ({string.Join(", ", rNames)}) vs [yellow]{m.Match.TeamNameDire.EscapeMarkup()}[/] ({string.Join(", ", dNames)})");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Markup("[bold]Enter match # to analyze, or 0 to go back: [/]");
            var pick = Console.ReadLine()?.Trim() ?? "";
            if (int.TryParse(pick, out var pickNum) && pickNum > 0)
            {
                var selected = buffered.FirstOrDefault(m => m.Index == pickNum);
                if (selected != null)
                {
                    AnsiConsole.MarkupLine("\n[cyan]Fetching matchup data and scoring (first analysis may take ~10s)...[/]");
                    await ShowTeamEdgeAnalysis(selected, heroes, openDotaService, draftScorer);
                }
            }
            break;
        }

        if (input.ToLower() == "q")
        {
            cts.Cancel();
            break;
        }

        if (int.TryParse(input, out var matchNum) && matchNum > 0)
        {
            var selected = matches.FirstOrDefault(m => m.Index == matchNum);
            if (selected != null)
            {
                await ShowTeamEdgeAnalysis(selected, heroes, openDotaService, draftScorer);
                AnsiConsole.Clear();
                AnsiConsole.MarkupLine("[bold yellow]═══ LIVE DRAFT WATCHER ═══[/]");
                AnsiConsole.WriteLine();
            }
            else
            {
                AnsiConsole.MarkupLine("[red]Invalid match number.[/]");
            }
        }
    }

    try { await watcherTask; }
    catch (OperationCanceledException) { }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[green]Live watcher stopped. Drafts found: {watcher.DraftsFound}[/]");
    PromptBack();
}

static async Task<double> ComputeTeamWinRateVsAsync(
    int[] teamPicks, int[] opponentPicks, OpenDotaService openDotaService)
{
    double totalWr = 0;
    int pairCount = 0;

    foreach (var heroId in teamPicks)
    {
        var matchups = await openDotaService.GetMatchupsAsync(heroId);
        foreach (var oppId in opponentPicks)
        {
            var mu = matchups.FirstOrDefault(m => m.HeroId == oppId);
            if (mu != null && mu.GamesPlayed > 0)
            {
                double shrunk = (mu.Wins + 50.0) / (mu.GamesPlayed + 100.0);
                totalWr += shrunk;
                pairCount++;
            }
        }
    }

    return pairCount > 0 ? totalWr / pairCount : 0.5;
}

static async Task ShowTeamEdgeAnalysis(
    LiveDraftWatcher.DraftMatchInfo matchInfo,
    List<Hero> heroes,
    OpenDotaService openDotaService,
    DraftScorer draftScorer)
{
    AnsiConsole.Clear();
    AnsiConsole.MarkupLine($"[bold yellow]═══ MATCH #{matchInfo.Index} — TEAM EDGE ANALYSIS ═══[/]");
    AnsiConsole.MarkupLine(
        $"[yellow]{matchInfo.Match.TeamNameRadiant.EscapeMarkup()}[/] ({matchInfo.RadiantPicks.Length}/5) vs " +
        $"[yellow]{matchInfo.Match.TeamNameDire.EscapeMarkup()}[/] ({matchInfo.DirePicks.Length}/5) " +
        $"[dim]Game Mode {matchInfo.Match.GameMode}[/]");
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine("[dim]Fetching matchup data and scoring...[/]");
    AnsiConsole.WriteLine();

    var radiantRecs = await draftScorer.ScoreAsync(new DraftInput
    {
        AllyTeam = matchInfo.RadiantPicks,
        EnemyTeam = matchInfo.DirePicks
    });

    var direRecs = await draftScorer.ScoreAsync(new DraftInput
    {
        AllyTeam = matchInfo.DirePicks,
        EnemyTeam = matchInfo.RadiantPicks
    });

    var radiantWr = await ComputeTeamWinRateVsAsync(
        matchInfo.RadiantPicks, matchInfo.DirePicks, openDotaService);
    var direWr = await ComputeTeamWinRateVsAsync(
        matchInfo.DirePicks, matchInfo.RadiantPicks, openDotaService);

    var radiantRoles = GetCoveredRoles(
        heroes.Where(h => matchInfo.RadiantPicks.Contains(h.Id)).ToList());
    var direRoles = GetCoveredRoles(
        heroes.Where(h => matchInfo.DirePicks.Contains(h.Id)).ToList());

    string[] roleCategories =
        ["Carry", "Support", "Initiator", "Nuker", "Durable", "Disabler", "Escape", "Pusher"];

    var table = new Table().Border(TableBorder.Rounded);
    table.AddColumn(new TableColumn("Criteria").Width(30));
    table.AddColumn(new TableColumn($"Radiant ({matchInfo.RadiantPicks.Length}/5)").Centered());
    table.AddColumn(new TableColumn($"Dire ({matchInfo.DirePicks.Length}/5)").Centered());
    table.AddColumn(new TableColumn("Edge").Centered());

    var wrColor = Math.Abs(radiantWr - direWr) < 0.01 ? "dim" : radiantWr > direWr ? "green" : "red";
    table.AddRow(
        "Avg Win Rate vs Opponent",
        $"{radiantWr:P1}",
        $"{direWr:P1}",
        $"[{wrColor}]{(radiantWr > direWr ? "Radiant" : direWr > radiantWr ? "Dire" : "Tied")}[/]");

    var radiantRoleStr = radiantRoles.Count > 0
        ? string.Join(", ", radiantRoles.Select(r => r.EscapeMarkup()))
        : "[dim]none[/]";
    var direRoleStr = direRoles.Count > 0
        ? string.Join(", ", direRoles.Select(r => r.EscapeMarkup()))
        : "[dim]none[/]";
    var roleEdge = radiantRoles.Count > direRoles.Count ? "[green]Radiant[/]"
        : direRoles.Count > radiantRoles.Count ? "[green]Dire[/]"
        : "[dim]Tied[/]";
    table.AddRow("Role Coverage", radiantRoleStr, direRoleStr, roleEdge);

    var radiantMissing = roleCategories.Where(r => !radiantRoles.Contains(r)).ToList();
    var direMissing = roleCategories.Where(r => !direRoles.Contains(r)).ToList();
    table.AddRow(
        "Missing Roles",
        radiantMissing.Count > 0 ? string.Join(", ", radiantMissing) : "[green]none[/]",
        direMissing.Count > 0 ? string.Join(", ", direMissing) : "[green]none[/]",
        "");

    var radiantTop3 = radiantRecs.Take(3).Select(r => $"{r.HeroName.EscapeMarkup()} ({r.Score:F1})").ToList();
    var direTop3 = direRecs.Take(3).Select(r => $"{r.HeroName.EscapeMarkup()} ({r.Score:F1})").ToList();

    table.AddRow("Recommended #1",
        radiantTop3.Count > 0 ? radiantTop3[0] : "[dim]N/A[/]",
        direTop3.Count > 0 ? direTop3[0] : "[dim]N/A[/]",
        "");
    table.AddRow("Recommended #2",
        radiantTop3.Count > 1 ? radiantTop3[1] : "[dim]N/A[/]",
        direTop3.Count > 1 ? direTop3[1] : "[dim]N/A[/]",
        "");
    table.AddRow("Recommended #3",
        radiantTop3.Count > 2 ? radiantTop3[2] : "[dim]N/A[/]",
        direTop3.Count > 2 ? direTop3[2] : "[dim]N/A[/]",
        "");

    AnsiConsole.Write(table);

    AnsiConsole.WriteLine();
    if (Math.Abs(radiantWr - direWr) >= 0.01)
    {
        var leader = radiantWr > direWr
            ? matchInfo.Match.TeamNameRadiant
            : matchInfo.Match.TeamNameDire;
        var diff = Math.Abs(radiantWr - direWr);
        AnsiConsole.MarkupLine(
            $"[bold green]{leader.EscapeMarkup()}[/] has the edge " +
            $"(+{diff:P0} avg win rate, {(radiantRoles.Count > direRoles.Count ? "+" : "")}{radiantRoles.Count - direRoles.Count} roles)");
    }
    else
    {
        AnsiConsole.MarkupLine("[dim]Teams are evenly matched.[/]");
    }

    AnsiConsole.WriteLine();
    if (AnsiConsole.Confirm("Deep dive into a recommended hero?", false))
    {
        var allRecs = radiantRecs.Concat(direRecs)
            .GroupBy(r => r.HeroId)
            .Select(g => g.First())
            .OrderByDescending(r => r.Score)
            .ToList();
        await ShowDeepDiveAsync(allRecs, heroes, openDotaService);
    }
}

static HashSet<string> GetCoveredRoles(List<Hero> allies)
{
    var roles = new HashSet<string>();
    foreach (var ally in allies)
        foreach (var role in ally.Roles)
            roles.Add(role);
    return roles;
}

