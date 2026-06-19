using System.Text.Json.Serialization;

namespace DotaDraftAdvisor.Models;

public class LiveMatch
{
    [JsonPropertyName("match_id")]
    public long MatchId { get; set; }

    [JsonPropertyName("game_time")]
    public int GameTime { get; set; }

    [JsonPropertyName("team_name_radiant")]
    public string TeamNameRadiant { get; set; } = "";

    [JsonPropertyName("team_name_dire")]
    public string TeamNameDire { get; set; } = "";

    [JsonPropertyName("league_id")]
    public int LeagueId { get; set; }

    [JsonPropertyName("average_mmr")]
    public int AverageMmr { get; set; }

    [JsonPropertyName("game_mode")]
    public int GameMode { get; set; }

    [JsonPropertyName("players")]
    public List<LivePlayer> Players { get; set; } = new();
}

public class LivePlayer
{
    [JsonPropertyName("account_id")]
    public long AccountId { get; set; }

    [JsonPropertyName("hero_id")]
    public int HeroId { get; set; }

    [JsonPropertyName("team")]
    public int Team { get; set; }
}
