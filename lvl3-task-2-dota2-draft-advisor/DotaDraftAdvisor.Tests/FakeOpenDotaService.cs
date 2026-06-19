using DotaDraftAdvisor.Models;
using DotaDraftAdvisor.Services;

namespace DotaDraftAdvisor.Tests;

public class FakeOpenDotaService : OpenDotaService
{
    private readonly List<Hero> _heroes;
    private readonly Dictionary<int, List<HeroMatchup>> _matchups;

    public FakeOpenDotaService() : base(null!)
    {
        _heroes = GenerateHeroes();
        _matchups = GenerateMatchups();
    }

    public override Task<List<Hero>> GetHeroesAsync()
        => Task.FromResult(_heroes.ToList());

    public override Task<List<HeroMatchup>> GetMatchupsAsync(int heroId)
        => Task.FromResult(_matchups.GetValueOrDefault(heroId, [])!);

    private static List<Hero> GenerateHeroes()
    {
        var heroes = new List<Hero>();

        for (int i = 1; i <= 20; i++)
        {
            heroes.Add(new Hero
            {
                Id = i,
                Name = $"npc_dota_hero_{i}",
                LocalizedName = $"Hero{i}",
                PrimaryAttr = i % 2 == 0 ? "agi" : "str",
                AttackType = i % 2 == 0 ? "Ranged" : "Melee",
                Roles = [i % 2 == 0 ? "Support" : "Carry"],
            });
        }

        heroes.Add(new Hero { Id = 100, LocalizedName = "Enemy1", Roles = ["Carry"] });
        heroes.Add(new Hero { Id = 101, LocalizedName = "Enemy2", Roles = ["Support"] });
        heroes.Add(new Hero { Id = 102, LocalizedName = "Enemy3", Roles = ["Initiator"] });
        heroes.Add(new Hero { Id = 103, LocalizedName = "Enemy4", Roles = ["Nuker"] });
        heroes.Add(new Hero { Id = 104, LocalizedName = "Enemy5", Roles = ["Durable"] });

        return heroes;
    }

    private static Dictionary<int, List<HeroMatchup>> GenerateMatchups()
    {
        var dict = new Dictionary<int, List<HeroMatchup>>();

        for (int heroId = 5; heroId <= 20; heroId++)
        {
            var matchups = new List<HeroMatchup>();
            foreach (var enemyId in new[] { 100, 101, 102, 103, 104 })
            {
                matchups.Add(new HeroMatchup
                {
                    HeroId = enemyId,
                    GamesPlayed = heroId * 10,
                    Wins = (int)(heroId * 10 * (0.4 + heroId * 0.01)),
                });
            }
            dict[heroId] = matchups;
        }

        return dict;
    }
}
