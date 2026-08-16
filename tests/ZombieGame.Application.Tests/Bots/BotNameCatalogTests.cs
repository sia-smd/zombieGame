namespace ZombieGame.Application.Tests.Bots;

using ZombieGame.Application.Bots;

public class BotNameCatalogTests
{
    [Fact]
    public void Names_AreUnique_IgnoringCase()
    {
        var distinct = BotNameCatalog.Names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        Assert.Equal(BotNameCatalog.Names.Count, distinct);
        Assert.True(BotNameCatalog.Names.Count >= 100);
    }

    [Fact]
    public void NextUnique_AvoidsDuplicatesWithinMatch()
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var picked = new List<string>();
        for (var i = 0; i < 16; i++)
            picked.Add(BotNameCatalog.NextUnique(reserved, new Random(42)));

        Assert.Equal(picked.Count, picked.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(picked, n => Assert.False(n.StartsWith("Bot_", StringComparison.Ordinal)));
    }
}
