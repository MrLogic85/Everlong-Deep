using System.Diagnostics;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Places;

namespace EverlongDeep.Simulation;

/// <summary>Everything one seeded run simulates.</summary>
public sealed class World
{
    public World(int seed, Random random, IReadOnlyList<Place> places, IReadOnlyList<Person> people)
    {
        Debug.Assert(people.All(person => places.Any(place => place.Id == person.Place)), "Every person is in a place of this world");

        Seed = seed;
        Random = random;
        Places = places;
        People = people;
    }

    public int Seed { get; }

    /// <summary>The run's only source of randomness (<c>docs/CODE_DESIGN_PRINCIPLES.md</c> §4).</summary>
    public Random Random { get; }

    public IReadOnlyList<Place> Places { get; }
    public IReadOnlyList<Person> People { get; }
}
