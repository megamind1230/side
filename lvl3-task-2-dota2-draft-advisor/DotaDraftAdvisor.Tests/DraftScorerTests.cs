using DotaDraftAdvisor.Models;
using DotaDraftAdvisor.Services;
using FluentAssertions;

namespace DotaDraftAdvisor.Tests;

public class DraftScorerTests
{
    private readonly FakeOpenDotaService _fakeApi = new();
    private readonly DraftScorer _sut;

    public DraftScorerTests()
    {
        _sut = new DraftScorer(_fakeApi);
    }

    [Fact]
    public async Task ScoreAsync_orders_by_score_descending()
    {
        var input = new DraftInput
        {
            AllyTeam = [1, 2, 3, 4],
            EnemyTeam = [100, 101, 102, 103, 104],
        };

        var results = await _sut.ScoreAsync(input);

        results.Should().NotBeEmpty();
        results.Should().BeInDescendingOrder(r => r.Score);
    }

    [Fact]
    public async Task ScoreAsync_excludes_already_picked_allies()
    {
        var input = new DraftInput
        {
            AllyTeam = [1, 2, 3, 4],
            EnemyTeam = [100, 101, 102, 103, 104],
        };

        var results = await _sut.ScoreAsync(input);

        results.Should().NotContain(r => r.HeroId == 1);
        results.Should().NotContain(r => r.HeroId == 4);
    }

    [Fact]
    public async Task ScoreAsync_excludes_already_picked_enemies()
    {
        var input = new DraftInput
        {
            AllyTeam = [1, 2, 3, 4],
            EnemyTeam = [100, 101, 102, 103, 104],
        };

        var results = await _sut.ScoreAsync(input);

        results.Should().NotContain(r => r.HeroId == 100);
        results.Should().NotContain(r => r.HeroId == 104);
    }

    [Fact]
    public async Task ScoreAsync_returns_all_candidates()
    {
        var input = new DraftInput
        {
            AllyTeam = [1, 2, 3, 4],
            EnemyTeam = [100, 101, 102, 103, 104],
        };

        var results = await _sut.ScoreAsync(input);

        int pickedCount = 4 + 5;
        int totalHeroes = 20 + 5;
        results.Should().HaveCount(totalHeroes - pickedCount);
    }

    [Fact]
    public async Task Bayesian_shrinkage_keeps_scores_between_0_and_100()
    {
        var input = new DraftInput
        {
            AllyTeam = [1, 2, 3, 4],
            EnemyTeam = [100, 101, 102, 103, 104],
        };

        var results = await _sut.ScoreAsync(input);

        results.Should().OnlyContain(r => r.Score >= 0 && r.Score <= 100);
    }

    [Fact]
    public async Task ScoreAsync_contains_HeroName_and_Reasoning()
    {
        var input = new DraftInput
        {
            AllyTeam = [1, 2, 3, 4],
            EnemyTeam = [100, 101, 102, 103, 104],
        };

        var results = await _sut.ScoreAsync(input);

        foreach (var r in results)
        {
            r.HeroName.Should().NotBeNullOrEmpty();
            r.Reasoning.Should().NotBeEmpty();
        }
    }
}
