namespace ZombieGame.Application.Bots;

/// <summary>
/// Natural first names used as bot <c>Username</c> (lobby/display). Bot identity remains
/// <c>IsBot</c> and <c>PasswordHash == "BOT"</c>, not a <c>Bot_</c> prefix.
/// </summary>
public static class BotNameCatalog
{
    public static IReadOnlyList<string> Names { get; } = DistinctPreserveOrder(
    [
        "Oliver", "Liam", "Noah", "Ethan", "Lucas", "Mason", "Logan", "James",
        "Henry", "Alexander", "Benjamin", "Daniel", "Michael", "William", "Jack",
        "Samuel", "Leo", "Arthur", "Oscar", "Theodore", "Elijah", "Charlie",
        "Thomas", "George", "Edward", "Harry", "Jacob", "Frederick", "Archie",
        "Joshua", "Max", "Sebastian", "Finley", "Alfie", "Adam", "Joseph",
        "Isaac", "Theo", "Teddy", "Dylan", "Harrison", "Arlo",
        "Hunter", "Jasper", "Tommy", "Charles", "Connor", "Elliot", "Riley",

        "Jameson", "Wyatt", "Carter", "Grayson", "Jackson", "Aiden", "Owen",
        "Luke", "Gabriel", "Julian", "Matthew", "David", "John",
        "Andrew", "Nathan", "Caleb", "Ryan", "Isaiah", "Levi", "Nathaniel",
        "Christopher", "Anthony", "Lincoln", "Asher", "Ezra",
        "Hudson", "Nolan", "Miles", "Eli", "Aaron", "Roman", "Adrian",
        "Cameron", "Colton", "Jordan", "Dominic", "Austin", "Jaxon",
        "Cooper", "Easton", "Xavier", "Parker", "Wesley", "Kai", "Silas",

        "Emma", "Olivia", "Ava", "Sophia", "Isabella", "Mia", "Charlotte",
        "Amelia", "Harper", "Evelyn", "Abigail", "Emily", "Ella", "Elizabeth",
        "Camila", "Luna", "Sofia", "Avery", "Mila", "Aria", "Scarlett",
        "Penelope", "Layla", "Chloe", "Victoria", "Madison", "Eleanor",
        "Grace", "Nora", "Zoey", "Hannah", "Hazel", "Lily",
        "Ellie", "Violet", "Lillian", "Zoe", "Stella", "Aurora", "Natalie",
        "Emilia", "Everly", "Leah", "Aubrey", "Willow", "Addison", "Lucy",
        "Audrey", "Bella",

        "Sophie", "Alice", "Claire", "Ruby", "Ivy", "Eva", "Elena", "Naomi",
        "Maya", "Lydia", "Clara", "Eliza", "Rose", "Julia", "Anna", "Sarah",
        "Jasmine", "Iris", "Cora", "Lena", "Molly", "Freya", "Phoebe",
        "Sadie", "Isla", "Rosie", "Millie", "Poppy", "Maisie", "Daisy",
        "Georgia", "Elsie", "Sienna", "Matilda", "Amelie", "Florence",
        "Esme", "Thea", "Maisy", "Amber", "Vera",
        "Nina", "Lola", "Skye"
    ]);

    /// <summary>
    /// Picks a catalog name not in <paramref name="reserved"/> (case-insensitive).
    /// Falls back to <c>Bot_{id}</c> only when the catalog is exhausted or every name is reserved.
    /// </summary>
    public static string NextUnique(
        ISet<string> reserved,
        Random random,
        Func<string, bool>? isUnavailable = null)
    {
        var candidates = Names.Where(n => !Contains(reserved, n)).ToList();
        Shuffle(candidates, random);

        foreach (var name in candidates)
        {
            if (isUnavailable?.Invoke(name) == true)
                continue;

            reserved.Add(name);
            return name;
        }

        var fallback = $"Bot_{Guid.NewGuid().ToString("N")[..8]}";
        reserved.Add(fallback);
        return fallback;
    }

    private static bool Contains(ISet<string> reserved, string name)
    {
        if (reserved is HashSet<string> hash)
            return hash.Contains(name);

        return reserved.Any(r => string.Equals(r, name, StringComparison.OrdinalIgnoreCase));
    }

    private static void Shuffle(List<string> list, Random random)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static IReadOnlyList<string> DistinctPreserveOrder(IEnumerable<string> names)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var name in names)
        {
            if (seen.Add(name))
                list.Add(name);
        }

        return list;
    }
}
