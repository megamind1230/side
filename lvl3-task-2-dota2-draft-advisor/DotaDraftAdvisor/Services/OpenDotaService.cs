using System.Net.Http.Json;
using DotaDraftAdvisor.Models;
using Serilog;

namespace DotaDraftAdvisor.Services;

public class OpenDotaService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private List<Hero>? _cachedHeroes;
    private readonly Dictionary<int, List<HeroMatchup>> _matchupCache = new();
    private readonly SemaphoreSlim _heroCacheLock = new(1, 1);

    private const string BaseUrl = "https://api.opendota.com/api";

    public OpenDotaService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public virtual async Task<List<Hero>> GetHeroesAsync()
    {
        if (_cachedHeroes != null)
        {
            Log.Debug("OpenDotaService.GetHeroesAsync → returning {Count} cached heroes", _cachedHeroes.Count);
            return _cachedHeroes;
        }

        await _heroCacheLock.WaitAsync();
        try
        {
            if (_cachedHeroes != null) return _cachedHeroes;

            var client = _httpClientFactory.CreateClient();
            Log.Information("OpenDotaService.GetHeroesAsync → fetching from API");

            var heroes = await RetryAsync(() =>
                client.GetFromJsonAsync<List<Hero>>($"{BaseUrl}/heroes"));

            _cachedHeroes = heroes ?? [];
            Log.Information("OpenDotaService.GetHeroesAsync → fetched {Count} heroes", _cachedHeroes.Count);
            return _cachedHeroes;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpenDotaService.GetHeroesAsync → failed after retries");
            return [];
        }
        finally
        {
            _heroCacheLock.Release();
        }
    }

    public virtual async Task<List<HeroMatchup>> GetMatchupsAsync(int heroId)
    {
        if (_matchupCache.TryGetValue(heroId, out var cached))
        {
            Log.Debug("OpenDotaService.GetMatchupsAsync({HeroId}) → returning cached {Count} matchups",
                heroId, cached.Count);
            return cached;
        }

        var client = _httpClientFactory.CreateClient();
        Log.Information("OpenDotaService.GetMatchupsAsync({HeroId}) → fetching from API", heroId);

        try
        {
            var matchups = await RetryAsync(() =>
                client.GetFromJsonAsync<List<HeroMatchup>>($"{BaseUrl}/heroes/{heroId}/matchups"));

            var result = matchups ?? [];
            _matchupCache[heroId] = result;
            Log.Information("OpenDotaService.GetMatchupsAsync({HeroId}) → fetched {Count} matchups",
                heroId, result.Count);
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpenDotaService.GetMatchupsAsync({HeroId}) → failed after retries", heroId);
            return [];
        }
    }

    public virtual async Task<List<LiveMatch>> GetLiveMatchesAsync()
    {
        var client = _httpClientFactory.CreateClient();
        Log.Debug("OpenDotaService.GetLiveMatchesAsync → polling /api/live");

        try
        {
            var matches = await client.GetFromJsonAsync<List<LiveMatch>>($"{BaseUrl}/live");
            var result = matches ?? [];
            Log.Debug("OpenDotaService.GetLiveMatchesAsync → {Count} live matches", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OpenDotaService.GetLiveMatchesAsync → failed");
            return [];
        }
    }

    private static async Task<T?> RetryAsync<T>(Func<Task<T?>> operation, int maxRetries = 1)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                Log.Warning(ex, "HTTP request failed (attempt {Attempt}/{MaxRetries}), retrying in 2s",
                    attempt + 1, maxRetries);
                await Task.Delay(2000);
            }
        }
        return await operation();
    }

    public virtual void InvalidateCache()
    {
        _cachedHeroes = null;
        _matchupCache.Clear();
        Log.Information("OpenDotaService cache invalidated");
    }

    public virtual async Task PrefetchAllMatchupsAsync()
    {
        var heroes = await GetHeroesAsync();
        var uncached = heroes.Where(h => !_matchupCache.ContainsKey(h.Id)).ToList();
        if (uncached.Count == 0)
        {
            Log.Information("PrefetchAllMatchups: all {Count} heroes already cached", _matchupCache.Count);
            return;
        }

        Log.Information("PrefetchAllMatchups: fetching matchups for {Count} heroes (1.2s spacing)...", uncached.Count);

        using var semaphore = new SemaphoreSlim(1, 1);
        var tasks = uncached.Select(async hero =>
        {
            await semaphore.WaitAsync();
            try
            {
                await GetMatchupsAsync(hero.Id);
            }
            finally
            {
                semaphore.Release();
            }

            await Task.Delay(200);
        });

        await Task.WhenAll(tasks);
        Log.Information("PrefetchAllMatchups: {Count} hero matchups cached", _matchupCache.Count);
    }
}
