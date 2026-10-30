using System.Diagnostics;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.Events;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation;

/// <summary>Everything one seeded run simulates.</summary>
public sealed class World
{
    private readonly Dictionary<ThingId, Thing> _thingsById;
    private readonly List<Person> _people;
    private readonly Dictionary<PersonId, Person> _peopleById;

    public World(int seed, Random random, IReadOnlyList<Thing> things, IReadOnlyList<Person> people)
    {
        Seed = seed;
        Random = random;
        Things = things;
        _people = [.. people];
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

    /// <summary>The ticks between the periodic steps (fading and tie formation).</summary>
    public const int PeriodTicks = 3_600;

    private readonly TalkSchedule _talk = new();

    // Events waiting for their tick, in tick order and, at one tick, in the order they were scheduled.
    private readonly PriorityQueue<Event, (long Tick, long Order)> _events = new();
    private long _eventsScheduled;

    /// <summary>
    /// Simulation time in ticks since the run began; the past is below 0. The simulation never says how long a tick
    /// is: the game decides how many pass per frame, so it can speed the world up or slow it down. The CLI and the
    /// tuning assume 1 tick = 1 second.
    /// </summary>
    public long Tick { get; private set; }

    public IReadOnlyList<Thing> Things { get; }
    /// <summary>Everyone in the world, the dead included, in the order they came into it.</summary>
    public IReadOnlyList<Person> People => _people;

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

    /// <summary>
    /// Brings <paramref name="person"/> into the world now, in their place; they talk from their next moment on, like
    /// anyone else.
    /// </summary>
    internal void Enter(Person person)
    {
        Debug.Assert(!_peopleById.ContainsKey(person.Id), "A person enters the world only once");
        Debug.Assert(_thingsById.TryGetValue(person.Place, out Thing? place) && place.CanHoldPeople, "A person enters a place or ship of this world");
        Debug.Assert(person.Relations.All(relation => _peopleById.ContainsKey(relation.Other)), "Every relation is to a person of this world");

        _people.Add(person);
        _peopleById.Add(person.Id, person);
        _talk.Enter(this, person);
    }

    /// <summary>Kills <paramref name="id"/> now; a conversation they are in ends without effect.</summary>
    public void Kill(PersonId id) => Person(id).Die();

    /// <summary>
    /// Keeps <paramref name="scheduled"/> until its tick, now or later, where it happens before that tick's talk, then
    /// drops it: nothing keeps an event after it happens.
    /// </summary>
    public void Schedule(Event scheduled, SimulationLog log)
    {
        EventDropped? dropped = scheduled.Tick < Tick
            ? new EventDropped(Tick, scheduled.Act, scheduled.Target, scheduled.Tick, DropReason.Past)
            : scheduled.Newcomer is { } newcomer && _events.UnorderedItems.Any(item => item.Element.Newcomer?.Id == newcomer.Id)
                ? new EventDropped(Tick, scheduled.Act, scheduled.Target, scheduled.Tick, DropReason.NewcomerScheduled, newcomer.Id)
                : null;
        Debug.Assert(dropped is null, dropped?.ToString());
        if (dropped is not null)
        {
            log.Write(dropped);
            return;
        }

        _events.Enqueue(scheduled, (scheduled.Tick, _eventsScheduled++));
    }

    /// <summary>
    /// Runs the next <paramref name="ticks"/> ticks. At each multiple of <see cref="PeriodTicks"/> it reaches, the talk
    /// before it runs first, then the periodic steps, then what is due from it on. Stepping N ticks at once or one at a
    /// time gives the same run.
    /// </summary>
    public void Step(long ticks, SimulationLog log)
    {
        Debug.Assert(ticks >= 0, "Time only moves forward");
        if (ticks < 0)
        {
            log.Write(new NegativeStep(Tick, ticks));
            return;
        }

        long end = Tick + ticks;
        while (Tick < end)
        {
            long boundary = ((Tick / PeriodTicks) + 1) * PeriodTicks;
            long until = Math.Min(boundary, end);
            RunUntil(until, log);
            AdvanceTo(until, log);
            if (until == boundary)
            {
                TieFormation.Step(this, PeriodTicks, log);
                Fading.Step(this, PeriodTicks, log);
            }
        }
    }

    // Runs the events and talk due before tick end; an event happens before the talk at its tick.
    private void RunUntil(long end, SimulationLog log)
    {
        while (_events.TryPeek(out Event? next, out (long Tick, long Order) at) && at.Tick < end)
        {
            _events.Dequeue();
            _talk.RunUntil(this, at.Tick, log);
            AdvanceTo(at.Tick, log);
            next.Happen(this, log);
        }

        _talk.RunUntil(this, end, log);
    }

    internal void AdvanceTo(long tick, SimulationLog log)
    {
        Debug.Assert(tick >= Tick, "Time only moves forward");
        if (tick < Tick)
        {
            log.Write(new TimeWentBack(Tick, tick));
            return;
        }

        Tick = tick;
    }
}
