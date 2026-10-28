using System.Diagnostics;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation;

/// <summary>Everything one seeded run simulates.</summary>
public sealed class World
{
    private readonly Dictionary<ThingId, Thing> _thingsById;
    private readonly Dictionary<PersonId, Person> _peopleById;

    public World(int seed, Random random, IReadOnlyList<Thing> things, IReadOnlyList<Person> people)
    {
        Seed = seed;
        Random = random;
        Things = things;
        People = people;
        _thingsById = things.ToDictionary(thing => thing.Id);
        _peopleById = people.ToDictionary(person => person.Id);

        Debug.Assert(
            people.All(person => _thingsById.TryGetValue(person.Place, out Thing? place) && place.CanHoldPeople),
            "Every person is in a place or ship of this world");

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

    public IReadOnlyList<Thing> Things { get; }
    public IReadOnlyList<Person> People { get; }

    public Thing Thing(ThingId id)
    {
        Debug.Assert(_thingsById.ContainsKey(id), $"Thing {id.Value} is not in this world");
        return _thingsById[id];
    }

    public Person Person(PersonId id)
    {
        Debug.Assert(_peopleById.ContainsKey(id), $"Person {id.Value} is not in this world");
        return _peopleById[id];
    }

    /// <summary>Advances the world by one hour.</summary>
    public void StepHour(SimulationLog log)
    {
        Hour++;
        Telling.StepHour(this, log);
        TieFormation.StepHour(this, log);
    }
}
