using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// When people talk: each free person has a next moment to consider it, and a conversation keeps both people busy
/// until it ends (<c>docs/08 Social Model.md</c> → "Telling"). Events run by minute, and events at the same minute
/// in the order they were scheduled.
/// </summary>
internal sealed class TalkSchedule
{
    /// <summary>How long someone who kept quiet, or just finished talking, waits before considering it again.</summary>
    public static readonly (int Min, int Max) WaitMinutes = (5, 20);

    // A conversation lasts longer the more talkative the pair: 1 to 7 minutes.
    public const double BaseMinutes = 3;
    public const double TalkativeMinutes = 2;
    public const int ShortestMinutes = 1;

    private readonly PriorityQueue<Event, (int Minute, long Order)> _events = new();
    private readonly HashSet<PersonId> _busy = [];

    // A person's consider event counts only while its number is their latest, so each has one moment pending.
    private readonly Dictionary<PersonId, int> _considerNumbers = [];
    private long _scheduled;
    private bool _started;

    // People never change place, so they are grouped once, in id order.
    private ILookup<ThingId, Person> _byPlace = Enumerable.Empty<Person>().ToLookup(person => person.Place);

    private abstract record Event;

    private sealed record Consider(Person Person, int Number) : Event;

    private sealed record End(Conversation Conversation) : Event;

    /// <summary>The minutes a conversation between <paramref name="teller"/> and <paramref name="listener"/> lasts.</summary>
    public static int Length(Person teller, Person listener)
    {
        double minutes = BaseMinutes + (TalkativeMinutes * (teller.Traits[TraitAxis.Talkative] + listener.Traits[TraitAxis.Talkative]));
        return Math.Max(ShortestMinutes, (int)Math.Round(minutes));
    }

    /// <summary>Runs every event before <paramref name="endMinute"/>, moving the world's clock to each.</summary>
    public void RunUntil(World world, int endMinute, SimulationLog log)
    {
        if (!_started)
        {
            Start(world);
        }

        while (_events.TryPeek(out Event? next, out (int Minute, long Order) at) && at.Minute < endMinute)
        {
            _events.Dequeue();
            world.AdvanceTo(at.Minute, log);
            switch (next)
            {
                // A listener's own moment that falls in a conversation lapses; its end schedules the next.
                case Consider consider when _considerNumbers[consider.Person.Id] == consider.Number && !_busy.Contains(consider.Person.Id):
                    Run(world, consider.Person);
                    break;
                case End end:
                    Finish(world, end.Conversation, log);
                    break;
            }
        }
    }

    // Each person's first moment falls within the first hour, drawn in shuffled order so no id comes first.
    private void Start(World world)
    {
        _started = true;
        _byPlace = world.People.OrderBy(person => person.Id.Value).ToLookup(person => person.Place);
        Person[] people = [.. world.People];
        world.Random.Shuffle(people);
        foreach (Person person in people)
        {
            ScheduleConsider(person, world.Minute + world.Random.Next(World.MinutesPerHour));
        }
    }

    private void Run(World world, Person teller)
    {
        if (teller.Beliefs.Count == 0 ||
            Telling.Begin(world, teller, _byPlace[teller.Place].Where(person => person != teller && !_busy.Contains(person.Id)))
                is not { } conversation)
        {
            ScheduleConsider(teller, world.Minute + Wait(world.Random));
            return;
        }

        _busy.Add(teller.Id);
        _busy.Add(conversation.Listener.Id);
        Schedule(new End(conversation), world.Minute + Length(teller, conversation.Listener));
    }

    private void Finish(World world, Conversation conversation, SimulationLog log)
    {
        Telling.End(world, conversation, log);
        foreach (Person person in new[] { conversation.Teller, conversation.Listener })
        {
            _busy.Remove(person.Id);
            ScheduleConsider(person, world.Minute + Wait(world.Random));
        }
    }

    private void ScheduleConsider(Person person, int minute)
    {
        int number = _considerNumbers.GetValueOrDefault(person.Id) + 1;
        _considerNumbers[person.Id] = number;
        Schedule(new Consider(person, number), minute);
    }

    private void Schedule(Event happening, int minute) => _events.Enqueue(happening, (minute, _scheduled++));

    private static int Wait(Random random) => random.Next(WaitMinutes.Min, WaitMinutes.Max + 1);
}
