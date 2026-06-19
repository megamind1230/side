using DotaDraftAdvisor.Models;
using DotaDraftAdvisor.Services;
using FluentAssertions;

namespace DotaDraftAdvisor.Tests;

public class AliasServiceTests
{
    private readonly AliasService _sut = new();
    private readonly List<Hero> _heroes;

    public AliasServiceTests()
    {
        _heroes =
        [
            new() { Id = 1, LocalizedName = "Anti-Mage" },
            new() { Id = 2, LocalizedName = "Crystal Maiden" },
            new() { Id = 25, LocalizedName = "Lina" },
            new() { Id = 44, LocalizedName = "Phantom Assassin" },
            new() { Id = 74, LocalizedName = "Invoker" },
            new() { Id = 114, LocalizedName = "Monkey King" },
            new() { Id = 126, LocalizedName = "Void Spirit" },
            new() { Id = 41, LocalizedName = "Faceless Void" },
        ];
    }

    [Fact]
    public void Resolve_pa_returns_PhantomAssassin()
    {
        var result = _sut.Resolve("pa", _heroes);
        result.Should().ContainSingle().Which.Should().Be(44);
    }

    [Fact]
    public void Resolve_invoker_returns_Invoker()
    {
        var result = _sut.Resolve("invoker", _heroes);
        result.Should().ContainSingle().Which.Should().Be(74);
    }

    [Fact]
    public void Resolve_lina_returns_Lina()
    {
        var result = _sut.Resolve("lina", _heroes);
        result.Should().ContainSingle().Which.Should().Be(25);
    }

    [Fact]
    public void Resolve_voker_prefix_match_returns_Invoker()
    {
        var result = _sut.Resolve("voker", _heroes);
        result.Should().ContainSingle().Which.Should().Be(74);
    }

    [Fact]
    public void Resolve_unknown_returns_empty()
    {
        var result = _sut.Resolve("unknownhero", _heroes);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Resolve_am_returns_AntiMage()
    {
        var result = _sut.Resolve("am", _heroes);
        result.Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public void Resolve_cm_returns_CrystalMaiden()
    {
        var result = _sut.Resolve("cm", _heroes);
        result.Should().ContainSingle().Which.Should().Be(2);
    }

    [Fact]
    public void Resolve_is_case_insensitive()
    {
        var result = _sut.Resolve("PA", _heroes);
        result.Should().ContainSingle().Which.Should().Be(44);
    }

    [Fact]
    public void Resolve_antimage_prefix_match_returns_AntiMage()
    {
        var result = _sut.Resolve("antimage", _heroes);
        result.Should().ContainSingle().Which.Should().Be(1);
    }
}
