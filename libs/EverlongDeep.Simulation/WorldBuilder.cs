using System.Diagnostics;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation;

/// <summary>Builds seeded worlds; the same seed builds the same world.</summary>
public static class WorldBuilder
{
    private static readonly string[] _names =
    [
        "Mara", "Pell", "Anouk", "Bram", "Corin", "Dagny", "Esker", "Fen",
        "Greta", "Hollis", "Ilse", "Jory", "Kestrel", "Lio", "Morwen", "Nils",
    ];

    private const string _tavernName = "The Drowned Lantern";
    private const double _tavernImportance = 0.5;

    private static readonly string[] _shipNames =
    [
        "Saltwren", "Grey Heron", "Mercy of Tides", "Black Gull", "Stormpetrel", "Widow's Lark",
    ];

    private static readonly string[] _itemNames =
    [
        "brass watch", "silver locket", "banana", "spyglass", "cutlass", "tobacco tin", "logbook", "compass",
    ];

    private static readonly (int Min, int Max) _shipCount = (2, 3);
    private static readonly (int Min, int Max) _itemCount = (3, 5);
    private static readonly (double Min, double Max) _shipImportance = (0.3, 0.9);
    private static readonly (double Min, double Max) _itemImportance = (0.05, 0.8);

    private static readonly (int Min, int Max) _labelledTraits = (2, 4);
    private const double _unremarkableTrait = 0.3;

    private const double _kinShare = 0.4;
    private const double _acquaintedShare = 0.4;
    private static readonly (double Min, double Max) _kinOpinion = (-0.3, 0.9);
    private static readonly (double Min, double Max) _kinTrust = (0.4, 0.9);
    private static readonly (double Min, double Max) _acquaintanceOpinion = (-0.8, 0.8);
    private static readonly (double Min, double Max) _acquaintanceTrust = (0.1, 0.7);

    public static int MaxPeople => _names.Length;

    /// <summary>
    /// A world of one tavern with <paramref name="peopleCount"/> people in it, a few ships and items, and a past.
    /// </summary>
    public static World Tavern(int seed, int peopleCount, SimulationLog log)
    {
        Debug.Assert(peopleCount >= 0 && peopleCount <= MaxPeople, $"peopleCount must be 0 to {MaxPeople}");
        log.Write(0, $"Seed: {seed}");
        int clampedCount = Math.Clamp(peopleCount, 0, MaxPeople);
        if (clampedCount != peopleCount)
        {
            log.Write(0, $"People: {peopleCount} asked, clamped to {clampedCount}");
            peopleCount = clampedCount;
        }
        var random = new Random(seed);
        var tavern = new Thing(new ThingId(0), _tavernName, ThingKind.Place, _tavernImportance);

        string[] names = (string[])_names.Clone();
        random.Shuffle(names);

        var people = new List<Person>(peopleCount);
        for (int i = 0; i < peopleCount; i++)
        {
            people.Add(new Person(new PersonId(i), names[i], RandomTraits(random), tavern.Id));
        }

        AddKin(people, random);
        AddAcquaintances(people, random);

        // Drawn after the people, so a seed's people don't depend on its things.
        List<Thing> things = [tavern];
        AddThings(things, ThingKind.Ship, _shipNames, _shipCount, _shipImportance, random);
        AddThings(things, ThingKind.Item, _itemNames, _itemCount, _itemImportance, random);

        var world = new World(seed, random, things, people);
        SeededPast.Draw(world, tavern.Id, log);
        return world;
    }

    private static void AddThings(
        List<Thing> things, ThingKind kind, string[] names, (int Min, int Max) count, (double Min, double Max) importance, Random random)
    {
        string[] shuffled = (string[])names.Clone();
        random.Shuffle(shuffled);
        int drawn = random.Next(count.Min, count.Max + 1);
        foreach (string name in shuffled[..drawn])
        {
            things.Add(new Thing(new ThingId(things.Count), name, kind, random.Between(importance)));
        }
    }

    // Neighbours in the shuffled list form families of two, so kin is random but never overlaps.
    private static void AddKin(List<Person> people, Random random)
    {
        for (int i = 0; i + 1 < people.Count; i++)
        {
            double roll = random.NextDouble();
            if (roll >= _kinShare)
            {
                continue;
            }

            (Ties first, Ties second) = roll < _kinShare / 2 ? (Ties.Sibling, Ties.Sibling) : (Ties.Child, Ties.Parent);
            (Relation aSide, Relation bSide) = MeetBothWays(people[i], people[i + 1], random, _kinOpinion, _kinTrust);
            aSide.Ties = first;
            bSide.Ties = second;
            TraitImpressions.GrewUpWith(aSide, people[i + 1], random);
            TraitImpressions.GrewUpWith(bSide, people[i], random);
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
                if (people[i].RelationTo(people[j].Id) is null && random.NextDouble() < _acquaintedShare)
                {
                    (Relation aSide, Relation bSide) =
                        MeetBothWays(people[i], people[j], random, _acquaintanceOpinion, _acquaintanceTrust);
                    TraitImpressions.FirstMeeting(aSide, people[j], random);
                    TraitImpressions.FirstMeeting(bSide, people[i], random);
                }
            }
        }
    }

    // Each side draws its own feelings.
    private static (Relation ASide, Relation BSide) MeetBothWays(
        Person a, Person b, Random random, (double Min, double Max) opinion, (double Min, double Max) trust)
    {
        Relation bSide = b.Meet(a.Id, random.Between(opinion), random.Between(trust));
        Relation aSide = a.Meet(b.Id, random.Between(opinion), random.Between(trust));
        return (aSide, bSide);
    }

    // A few traits that show make each person someone; the rest stay unremarkable.
    private static TraitProfile RandomTraits(Random random)
    {
        TraitAxis[] axes = Enum.GetValues<TraitAxis>();
        random.Shuffle(axes);
        HashSet<TraitAxis> labelled = [.. axes[..random.Next(_labelledTraits.Min, _labelledTraits.Max + 1)]];

        var values = new Dictionary<TraitAxis, double>();
        foreach (TraitAxis axis in Enum.GetValues<TraitAxis>())
        {
            if (!labelled.Contains(axis))
            {
                values[axis] = random.Between((-_unremarkableTrait, _unremarkableTrait));
                continue;
            }

            // Strictly past the mark, so the trait always shows.
            double strength = 1 - (random.NextDouble() * (1 - TraitProfile.LabelMark));
            values[axis] = random.Next(2) == 0 ? -strength : strength;
        }

        return new TraitProfile(values);
    }
}
