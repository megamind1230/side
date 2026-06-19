using System.Text.Json.Serialization;

namespace DotaDraftAdvisor.Models;

public class HeroMatchup
{
    [JsonPropertyName("hero_id")]
    public int HeroId { get; set; }

    [JsonPropertyName("games_played")]
    public int GamesPlayed { get; set; }

    [JsonPropertyName("wins")]
    public int Wins { get; set; }

    public double WinRate => GamesPlayed > 0 ? (double)Wins / GamesPlayed : 0;
}
