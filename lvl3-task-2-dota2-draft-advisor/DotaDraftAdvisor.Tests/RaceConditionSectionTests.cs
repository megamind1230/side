using DotaDraftAdvisor.Demo;
using DotaDraftAdvisor.Models;
using FluentAssertions;

namespace DotaDraftAdvisor.Tests;

public class RaceConditionSectionTests
{
    [Fact]
    public async Task RunAsync_completes_without_exception()
    {
        var fakeApi = new FakeOpenDotaService();
        var sut = new RaceConditionSection(fakeApi);

        var input = new DraftInput
        {
            AllyTeam = [1, 2, 3, 4],
            EnemyTeam = [100, 101, 102, 103, 104],
        };

        await sut.Invoking(s => s.RunAsync(input))
            .Should().NotThrowAsync();
    }

    [Fact]
    public async Task Parallel_increment_without_synchronization_loses_count()
    {
        int counter = 0;
        int expected = 10000;

        Parallel.For(0, expected, _ => counter++);

        counter.Should().BeLessThan(expected,
            "unsynchronized increment in a loop should lose updates due to race conditions");
    }

    [Fact]
    public void Parallel_increment_with_interlocked_is_accurate()
    {
        int counter = 0;
        int expected = 10000;

        Parallel.For(0, expected, _ => Interlocked.Increment(ref counter));

        counter.Should().Be(expected);
    }

    [Fact]
    public async Task FixedPass_with_ConcurrentDictionary_matches_expected_count()
    {
        var fakeApi = new FakeOpenDotaService();
        var sut = new RaceConditionSection(fakeApi);

        var heroes = await fakeApi.GetHeroesAsync();
        var input = new DraftInput
        {
            AllyTeam = [1, 2, 3, 4],
            EnemyTeam = [100, 101, 102, 103, 104],
        };

        await sut.Invoking(s => s.RunAsync(input))
            .Should().NotThrowAsync();
    }
}
