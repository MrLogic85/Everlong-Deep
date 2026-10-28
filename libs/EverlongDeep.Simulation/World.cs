using System.Diagnostics;
using System.Globalization;
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

    public const int MinutesPerHour = 60;

    private readonly TalkSchedule _talk = new();

    /// <summary>
    /// Simulation time in minutes since the run began. Hourly rates step every <see cref="MinutesPerHour"/> minutes,
    /// in <see cref="StepHour"/>.
    /// </summary>
    public int Minute { get; private set; }

    /// <summary>Whole hours since the run began.</summary>
    public int Hour => Minute / MinutesPerHour;

    /// <summary>The time as hours and minutes since the run began, such as "27:05", for log lines.</summary>
    public string Clock => ClockAt(Minute);

    /// <summary><paramref name="minute"/> as the clock shows it, "HH:MM".</summary>
    public static string ClockAt(int minute) =>
        string.Create(CultureInfo.InvariantCulture, $"{minute / MinutesPerHour:00}:{minute % MinutesPerHour:00}");

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

    internal Action<ConversationTrace>? OnTraced { get; private set; }

    /// <summary>
    /// Records every conversation from now on in full and hands each to <paramref name="onTraced"/> as it ends;
    /// tracing never changes what happens.
    /// </summary>
    public void TraceConversations(Action<ConversationTrace> onTraced) => OnTraced = onTraced;

    /// <summary>Runs the next hour's conversations minute by minute, then the hourly steps at its end.</summary>
    public void StepHour(SimulationLog log)
    {
        int end = ((Minute / MinutesPerHour) + 1) * MinutesPerHour;
        _talk.RunUntil(this, end, log);
        AdvanceTo(end, log);
        TieFormation.StepHour(this, log);
    }

    internal void AdvanceTo(int minute, SimulationLog log)
    {
        Debug.Assert(minute >= Minute, "Time only moves forward");
        if (minute < Minute)
        {
            log.Write($"{Clock} Time can't go back to minute {minute}; the clock stays");
            return;
        }

        Minute = minute;
    }
}
