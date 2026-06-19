using Serilog;

namespace DotaDraftAdvisor.Services;

public class AliasService
{
    private static readonly Dictionary<string, string> AliasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "am", "Anti-Mage" },
        { "antimage", "Anti-Mage" },
        { "pa", "Phantom Assassin" },
        { "sf", "Shadow Fiend" },
        { "nevermore", "Shadow Fiend" },
        { "lc", "Legion Commander" },
        { "wk", "Wraith King" },
        { "wraith", "Wraith King" },
        { "skeleton king", "Wraith King" },
        { "cm", "Crystal Maiden" },
        { "crystal", "Crystal Maiden" },
        { "inv", "Invoker" },
        { "voker", "Invoker" },
        { "pudge", "Pudge" },
        { "es", "Earthshaker" },
        { "shaker", "Earthshaker" },
        { "sk", "Sand King" },
        { "mk", "Monkey King" },
        { "tb", "Terrorblade" },
        { "od", "Outworld Destroyer" },
        { "np", "Nature's Prophet" },
        { "furion", "Nature's Prophet" },
        { "void", "Faceless Void" },
        { "sven", "Sven" },
        { "dk", "Dragon Knight" },
        { "dp", "Death Prophet" },
        { "qop", "Queen of Pain" },
        { "bm", "Beastmaster" },
        { "bb", "Bristleback" },
        { "ck", "Chaos Knight" },
        { "tinker", "Tinker" },
        { "naix", "Lifestealer" },
        { "lifestealer", "Lifestealer" },
        { "pl", "Phantom Lancer" },
        { "naga", "Naga Siren" },
        { "gyro", "Gyrocopter" },
        { "sniper", "Sniper" },
        { "drow", "Drow Ranger" },
        { "ursa", "Ursa" },
        { "slark", "Slark" },
        { "slardar", "Slardar" },
        { "tide", "Tidehunter" },
        { "tiny", "Tiny" },
        { "techies", "Techies" },
        { "puck", "Puck" },
        { "storm", "Storm Spirit" },
        { "ember", "Ember Spirit" },
        { "void spirit", "Void Spirit" },
        { "lina", "Lina" },
        { "zeus", "Zeus" },
        { "zuus", "Zeus" },
        { "bristle", "Bristleback" },
        { "axe", "Axe" },
        { "blood", "Bloodseeker" },
        { "bs", "Bloodseeker" },
        { "clinkz", "Clinkz" },
        { "weaver", "Weaver" },
        { "brood", "Broodmother" },
        { "meepo", "Meepo" },
        { "rubick", "Rubick" },
        { "disruptor", "Disruptor" },
        { "silencer", "Silencer" },
        { "oracle", "Oracle" },
        { "ww", "Winter Wyvern" },
        { "winter", "Winter Wyvern" },
        { "io", "Io" },
        { "wisp", "Io" },
        { "chen", "Chen" },
        { "enchant", "Enchantress" },
        { "kotl", "Keeper of the Light" },
        { "keeper", "Keeper of the Light" },
        { "snap", "Snapfire" },
        { "hoodwink", "Hoodwink" },
        { "marci", "Marci" },
        { "dawn", "Dawnbreaker" },
        { "primal", "Primal Beast" },
        { "ringmaster", "Ringmaster" },
        { "muerta", "Muerta" },
        { "kez", "Kez" },
        { "largo", "Largo" },
    };

    public List<int> Resolve(string input, List<Models.Hero> heroes)
    {
        var normalized = input.Trim().ToLowerInvariant();
        var results = new List<int>();

        if (AliasMap.TryGetValue(normalized, out var localizedName))
        {
            results.AddRange(heroes
                .Where(h => h.LocalizedName.Equals(localizedName, StringComparison.OrdinalIgnoreCase))
                .Select(h => h.Id));
        }

        if (results.Count == 0)
        {
            results.AddRange(heroes
                .Where(h => h.LocalizedName.StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
                .Select(h => h.Id));
        }

        if (results.Count == 0)
        {
            results.AddRange(heroes
                .Where(h => h.LocalizedName.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                .Select(h => h.Id));
        }

        Log.Debug("AliasService.Resolve({Input}) → {Count} results: {Ids}",
            input, results.Count, string.Join(",", results));

        return results;
    }
}
