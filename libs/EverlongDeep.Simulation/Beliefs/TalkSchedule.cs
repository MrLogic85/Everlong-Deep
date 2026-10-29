using System.Diagnostics;
using System.Runtime.InteropServices;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// When people talk: each free person has a next moment to consider it, and a conversation keeps both people busy
/// until it ends (<c>docs/08 Social Model.md</c> → "Telling"). Its steps run by minute, and steps at the same minute
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

    private readonly PriorityQueue<Step, (int Minute, long Order)> _steps = new();
    private readonly HashSet<PersonId> _busy = [];

    // A person's consider step counts only while its number is their latest, so each has one moment pending.
    private readonly Dictionary<PersonId, int> _considerNumbers = [];
    private long _scheduled;
    private bool _started;

    // People never change place, so they are grouped once, in id order, and a newcomer joins their place's group.
    private Dictionary<ThingId, List<Person>> _byPlace = [];

    private abstract record Step;

    private sealed record Consider(Person Person, int Number) : Step;

    private sealed record End(Conversation Conversation) : Step;

    /// <summary>The minutes a conversation between <paramref name="teller"/> and <paramref name="listener"/> lasts.</summary>
    public static int Length(Person teller, Person listener)
    {
        double minutes = BaseMinutes + (TalkativeMinutes * (teller.Traits[TraitAxis.Talkative] + listener.Traits[TraitAxis.Talkative]));
        return Math.Max(ShortestMinutes, (int)Math.Round(minutes));
    }

    /// <summary>Runs every step before <paramref name="endMinute"/>, moving the world's clock to each.</summary>
    public void RunUntil(World world, int endMinute, SimulationLog log)
    {
        if (!_started)
        {
            Start(world);
        }

        while (_steps.TryPeek(out Step? next, out (int Minute, long Order) at) && at.Minute < endMinute)
        {
            _steps.Dequeue();
            world.AdvanceTo(at.Minute, log);
            switch (next)
            {
                // A listener's own moment that falls in a conversation lapses; its end schedules the next. A dead
                // person's moment lapses for good.
                case Consider consider when _considerNumbers[consider.Person.Id] == consider.Number && !_busy.Contains(consider.Person.Id)
                    && !consider.Person.IsDead:
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
        _byPlace = world.People.OrderBy(person => person.Id.Value).GroupBy(person => person.Place).ToDictionary(group => group.Key, group => group.ToList());
        Person[] people = [.. world.People];
        world.Random.Shuffle(people);
        foreach (Person person in people)
        {
            ScheduleConsider(person, world.Minute + world.Random.Next(World.MinutesPerHour));
        }
    }

    /// <summary>
    /// Lets <paramref name="person"/>, who entered the world mid-run, talk: they wait for their first moment as
    /// after a conversation. Someone who enters before the talk starts gets their first moment from the start.
    /// </summary>
    public void Enter(World world, Person person)
    {
        if (!_started)
        {
            return;
        }

        List<Person> here = CollectionsMarshal.GetValueRefOrAddDefault(_byPlace, person.Place, out _) ??= [];
        Debug.Assert(here.All(other => other.Id.Value < person.Id.Value), "A newcomer has the highest id, so each place stays in id order");
        here.Add(person);
        ScheduleConsider(person, world.Minute + Wait(world.Random));
    }

    private void Run(World world, Person teller)
    {
        if (teller.Beliefs.Count == 0 ||
            Telling.Begin(world, teller, _byPlace[teller.Place].Where(person => person != teller && !_busy.Contains(person.Id) && !person.IsDead))
                is not { } conversation)
        {
            ScheduleConsider(teller, world.Minute + Wait(world.Random));
            return;
        }

        _busy.Add(teller.Id);
        _busy.Add(conversation.Listener.Id);
        Schedule(new End(conversation), world.Minute + Length(teller, conversation.Listener));
    }

    // A conversation that a death cut short ends without effect.
    private void Finish(World world, Conversation conversation, SimulationLog log)
    {
        if (!conversation.Teller.IsDead && !conversation.Listener.IsDead)
        {
            Telling.End(world, conversation, log);
        }

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

    private void Schedule(Step step, int minute) => _steps.Enqueue(step, (minute, _scheduled++));

    private static int Wait(Random random) => random.Next(WaitMinutes.Min, WaitMinutes.Max + 1);
}
