using System.Collections.Concurrent;
using Serilog;
using DotaDraftAdvisor.Models;

namespace DotaDraftAdvisor.Services;

public class LiveDraftWatcher
{
    private readonly OpenDotaService _openDota;
    private readonly DraftScorer _draftScorer;
    private int _draftsFound;

    public record DraftMatchInfo(
        int Index,
        LiveMatch Match,
        int[] RadiantPicks,
        int[] DirePicks,
        DateTime DiscoveredAt);

    private readonly ConcurrentQueue<DraftMatchInfo> _matchBuffer = new();

    public LiveDraftWatcher(OpenDotaService openDota, DraftScorer draftScorer)
    {
        _openDota = openDota;
        _draftScorer = draftScorer;
    }

    public int DraftsFound => _draftsFound;

    public List<DraftMatchInfo> GetBufferedMatches()
    {
        return [.. _matchBuffer];
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var heroes = await _openDota.GetHeroesAsync();
        Log.Information("LiveDraftWatcher started with {Count} heroes loaded", heroes.Count);

        while (!cancellationToken.IsCancellationRequested)
        {
            Log.Debug("LiveDraftWatcher polling /api/live");

            try
            {
                var matches = await _openDota.GetLiveMatchesAsync();

                var draftMatches = matches
                    .Where(m => m.GameTime < 60
                        && (m.GameMode == 2 || m.GameMode == 22)
                        && m.Players.Any(p => p.HeroId != 0))
                    .ToList();

                foreach (var match in draftMatches)
                {
                    var idx = Interlocked.Increment(ref _draftsFound);

                    var radiantPicks = match.Players
                        .Where(p => p.Team == 0 && p.HeroId != 0)
                        .Select(p => p.HeroId)
                        .ToArray();
                    var direPicks = match.Players
                        .Where(p => p.Team == 1 && p.HeroId != 0)
                        .Select(p => p.HeroId)
                        .ToArray();

                    _matchBuffer.Enqueue(new DraftMatchInfo(
                        idx, match, radiantPicks, direPicks, DateTime.Now));

                    Log.Information("Live match #{Idx} {MatchId}: {Radiant}({RPicks}) vs {Dire}({DPicks}) GameMode={Mode}",
                        idx, match.MatchId, match.TeamNameRadiant, string.Join(",", radiantPicks),
                        match.TeamNameDire, string.Join(",", direPicks), match.GameMode);
                }

                if (cancellationToken.IsCancellationRequested) break;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "LiveDraftWatcher poll failed");
            }

            try
            {
                await Task.Delay(5_000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        Log.Information("LiveDraftWatcher stopped. Total drafts found: {Count}", _draftsFound);
    }
}
