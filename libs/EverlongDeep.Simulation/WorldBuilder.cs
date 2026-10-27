using System.Diagnostics;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Places;

namespace EverlongDeep.Simulation;

/// <summary>Builds seeded worlds; the same seed builds the same world.</summary>
public static class WorldBuilder
{
    private static readonly string[] _names =
    [
        "Mara", "Pell", "Anouk", "Bram", "Corin", "Dagny", "Esker", "Fen",
        "Greta", "Hollis", "Ilse", "Jory", "Kestrel", "Lio", "Morwen", "Nils",
    ];

    public static int MaxPeople => _names.Length;

    /// <summary>A world of one tavern with <paramref name="peopleCount"/> people in it.</summary>
    public static World Tavern(int seed, int peopleCount, SimulationLog log)
    {
        Debug.Assert(peopleCount >= 0 && peopleCount <= MaxPeople, $"peopleCount must be 0 to {MaxPeople}");
        log.Write($"Seed: {seed}");
        int clampedCount = Math.Clamp(peopleCount, 0, MaxPeople);
        if (clampedCount != peopleCount)
        {
            log.Write($"People: {peopleCount} asked, clamped to {clampedCount}");
            peopleCount = clampedCount;
        }
        var random = new Random(seed);
        var tavern = new Place(new PlaceId(0), "The Drowned Lantern");

        string[] names = (string[])_names.Clone();
        random.Shuffle(names);

        var people = new List<Person>(peopleCount);
        for (int i = 0; i < peopleCount; i++)
        {
            people.Add(new Person(new PersonId(i), names[i], RandomTraits(random), tavern.Id));
        }

        return new World(seed, random, [tavern], people);
    }

    // One or two distinct traits tell people apart without piling up contradictions.
    private static Trait[] RandomTraits(Random random)
    {
        Trait[] all = Enum.GetValues<Trait>();
        random.Shuffle(all);
        Trait[] chosen = all[..random.Next(1, 3)];
        Array.Sort(chosen);
        return chosen;
    }
}
