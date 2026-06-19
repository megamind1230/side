using DotaDraftAdvisor.Models;
using DotaDraftAdvisor.Services;
using FluentAssertions;

namespace DotaDraftAdvisor.Tests;

public class IntegrationTests
{
    [Fact]
    public async Task FakeOpenDotaService_returns_heroes()
    {
        var fake = new FakeOpenDotaService();
        var heroes = await fake.GetHeroesAsync();

        heroes.Should().NotBeEmpty();
        heroes.Should().AllSatisfy(h => h.Id.Should().BeGreaterThan(0));
    }

    [Fact]
    public async Task FakeOpenDotaService_returns_matchups()
    {
        var fake = new FakeOpenDotaService();
        var heroes = await fake.GetHeroesAsync();
        var firstCandidate = heroes.First(h => h.Id is >= 5 and <= 20);

        var matchups = await fake.GetMatchupsAsync(firstCandidate.Id);

        matchups.Should().NotBeEmpty();
        matchups.Should().AllSatisfy(m =>
        {
            m.HeroId.Should().BeGreaterThan(0);
            m.GamesPlayed.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void LiveMatch_deserialization_has_required_fields()
    {
        var match = new LiveMatch
        {
            MatchId = 12345,
            GameTime = 0,
            TeamNameRadiant = "Team A",
            TeamNameDire = "Team B",
            GameMode = 2,
            Players =
            [
                new LivePlayer { AccountId = 1001, HeroId = 44, Team = 0 },
                new LivePlayer { AccountId = 1002, HeroId = 1, Team = 0 },
                new LivePlayer { AccountId = 2001, HeroId = 74, Team = 1 },
            ],
        };

        match.MatchId.Should().Be(12345);
        match.GameTime.Should().Be(0);
        match.GameMode.Should().Be(2);
        match.Players.Should().HaveCount(3);

        var radiantPicks = match.Players
            .Where(p => p.Team == 0 && p.HeroId != 0)
            .Select(p => p.HeroId)
            .ToArray();

        radiantPicks.Should().BeEquivalentTo([44, 1]);
    }

    [Fact]
    public async Task Full_pipeline_fake_produces_results()
    {
        var fake = new FakeOpenDotaService();
        var scorer = new DraftScorer(fake);

        var input = new DraftInput
        {
            AllyTeam = [1, 2, 3, 4],
            EnemyTeam = [100, 101, 102, 103, 104],
        };

        var results = await scorer.ScoreAsync(input);

        results.Should().NotBeEmpty();
        results.Should().BeInDescendingOrder(r => r.Score);
        results.Should().OnlyContain(r => r.Score >= 0 && r.Score <= 100);
        results.First().HeroName.Should().NotBeNullOrEmpty();
        results.First().Reasoning.Should().NotBeEmpty();
    }
}
