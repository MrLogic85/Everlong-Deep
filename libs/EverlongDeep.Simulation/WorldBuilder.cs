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

    private const double KinShare = 0.4;
    private const double AcquaintedShare = 0.4;
    private static readonly (double Min, double Max) _kinOpinion = (-0.3, 0.9);
    private static readonly (double Min, double Max) _kinTrust = (0.4, 0.9);
    private static readonly (double Min, double Max) _acquaintanceOpinion = (-0.8, 0.8);
    private static readonly (double Min, double Max) _acquaintanceTrust = (0.1, 0.7);

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

        AddKin(people, random);
        AddAcquaintances(people, random);

        return new World(seed, random, [tavern], people);
    }

    // Neighbours in the shuffled list form families of two, so kin is random but never overlaps.
    private static void AddKin(List<Person> people, Random random)
    {
        for (int i = 0; i + 1 < people.Count; i++)
        {
            double roll = random.NextDouble();
            if (roll >= KinShare)
            {
                continue;
            }

            (Ties first, Ties second) = roll < KinShare / 2 ? (Ties.Sibling, Ties.Sibling) : (Ties.Child, Ties.Parent);
            (Relation aSide, Relation bSide) = MeetBothWays(people[i], people[i + 1], random, _kinOpinion, _kinTrust);
            aSide.Ties = first;
            bSide.Ties = second;
            i++;
        }
    }

    // Some pairs of non-kin know each other; the rest are strangers until something introduces them.
    private static void AddAcquaintances(List<Person> people, Random random)
    {
        for (int i = 0; i < people.Count; i++)
        {
            for (int j = i + 1; j < people.Count; j++)
            {
                if (people[i].RelationTo(people[j].Id) is null && random.NextDouble() < AcquaintedShare)
                {
                    MeetBothWays(people[i], people[j], random, _acquaintanceOpinion, _acquaintanceTrust);
                }
            }
        }
    }

    // Each side draws its own feelings.
    private static (Relation ASide, Relation BSide) MeetBothWays(
        Person a, Person b, Random random, (double Min, double Max) opinion, (double Min, double Max) trust)
    {
        Relation bSide = b.Meet(a.Id, Between(random, opinion), Between(random, trust));
        Relation aSide = a.Meet(b.Id, Between(random, opinion), Between(random, trust));
        return (aSide, bSide);
    }

    private static double Between(Random random, (double Min, double Max) range) =>
        range.Min + (random.NextDouble() * (range.Max - range.Min));

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
