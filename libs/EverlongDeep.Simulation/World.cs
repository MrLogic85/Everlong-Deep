using System.Diagnostics;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Places;

namespace EverlongDeep.Simulation;

/// <summary>Everything one seeded run simulates.</summary>
public sealed class World
{
    private readonly Dictionary<PersonId, Person> _peopleById;

    public World(int seed, Random random, IReadOnlyList<Place> places, IReadOnlyList<Person> people)
    {
        Debug.Assert(people.All(person => places.Any(place => place.Id == person.Place)), "Every person is in a place of this world");

        Seed = seed;
        Random = random;
        Places = places;
        People = people;
        _peopleById = people.ToDictionary(person => person.Id);

        Debug.Assert(
            people.All(person => person.Relations.All(relation => _peopleById.ContainsKey(relation.Other))),
            "Every relation is to a person of this world");
    }

    public int Seed { get; }

    /// <summary>The run's only source of randomness (<c>docs/CODE_DESIGN_PRINCIPLES.md</c> §4).</summary>
    public Random Random { get; }

    /// <summary>
    /// Simulation time in hours since the run began. The hour is the simulation's one unit of time:
    /// every rate and duration counts in it, and <see cref="StepHour"/> advances one.
    /// </summary>
    public int Hour { get; private set; }

    public IReadOnlyList<Place> Places { get; }
    public IReadOnlyList<Person> People { get; }

    public Person Person(PersonId id)
    {
        Debug.Assert(_peopleById.ContainsKey(id), $"Person {id.Value} is not in this world");
        return _peopleById[id];
    }

    /// <summary>Advances the world by one hour.</summary>
    public void StepHour(SimulationLog log)
    {
        Hour++;
        TieFormation.StepHour(this, log);
    }
}
