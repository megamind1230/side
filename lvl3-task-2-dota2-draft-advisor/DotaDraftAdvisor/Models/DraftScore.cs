namespace DotaDraftAdvisor.Models;

public class DraftScore
{
    public int HeroId { get; set; }
    public string HeroName { get; set; } = "";
    public double Score { get; set; }
    public double AvgWinRate { get; set; }
    public int TotalGamesAnalyzed { get; set; }
    public string[] Reasoning { get; set; } = [];
}
