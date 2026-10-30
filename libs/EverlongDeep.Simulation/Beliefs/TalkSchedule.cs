using System.Diagnostics;
using System.Runtime.InteropServices;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// When people talk: each free person has a next moment to consider it, and a conversation keeps both people busy
/// until it ends (<c>docs/08 Social Model.md</c> → "Telling"). Its steps run by tick, and steps at the same tick in
/// the order they were scheduled.
/// </summary>
internal sealed class TalkSchedule
{
    /// <summary>
    /// How long someone waits before considering talking: at the talk's start, after keeping quiet, or after a
    /// conversation.
    /// </summary>
    public static readonly (int Min, int Max) WaitTicks = (80, 320);

    /// <summary>How long a conversation keeps both people busy.</summary>
    public const int ConversationTicks = 30;

    private readonly PriorityQueue<Step, (long Tick, long Order)> _steps = new();
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

    /// <summary>Runs every step before <paramref name="endTick"/>, moving the world's clock to each.</summary>
    public void RunUntil(World world, long endTick, SimulationLog log)
    {
        if (!_started)
        {
            Start(world);
        }

        while (_steps.TryPeek(out Step? next, out (long Tick, long Order) at) && at.Tick < endTick)
        {
            _steps.Dequeue();
            world.AdvanceTo(at.Tick, log);
            switch (next)
            {
                // A listener's own moment that falls in a conversation lapses; its end schedules the next. A dead
                // person's moment lapses for good.
                case Consider consider when _considerNumbers[consider.Person.Id] == consider.Number && !_busy.Contains(consider.Person.Id)
                    && !consider.Person.IsDead:
                    Run(world, consider.Person, log);
                    break;
                case End end:
                    Finish(world, end.Conversation, log);
                    break;
            }
        }
    }

    // Each person's first moment falls within the first wait, drawn in shuffled order so no id comes first.
    private void Start(World world)
    {
        _started = true;
        _byPlace = world.People.OrderBy(person => person.Id.Value).GroupBy(person => person.Place).ToDictionary(group => group.Key, group => group.ToList());
        Person[] people = [.. world.People];
        world.Random.Shuffle(people);
        foreach (Person person in people)
        {
            ScheduleConsider(person, world.Tick + world.Random.Next(WaitTicks.Max));
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
        ScheduleConsider(person, world.Tick + Wait(world.Random));
    }

    private void Run(World world, Person teller, SimulationLog log)
    {
        if (teller.Beliefs.Count == 0 ||
            Telling.Begin(world, teller, _byPlace[teller.Place].Where(person => person != teller && !_busy.Contains(person.Id) && !person.IsDead), log)
                is not { } conversation)
        {
            ScheduleConsider(teller, world.Tick + Wait(world.Random));
            return;
        }

        _busy.Add(teller.Id);
        _busy.Add(conversation.Listener.Id);
        Schedule(new End(conversation), world.Tick + ConversationTicks);
    }

    // A conversation that a death cut short ends without effect.
    private void Finish(World world, Conversation conversation, SimulationLog log)
    {
        if (!conversation.Teller.IsDead && !conversation.Listener.IsDead)
        {
            Telling.End(world, conversation, log);
        }
        else
        {
            log.Write(new ConversationCutShort(world.Tick, conversation.Teller.Id, conversation.Listener.Id, conversation.StartTick));
        }

        foreach (Person person in new[] { conversation.Teller, conversation.Listener })
        {
            _busy.Remove(person.Id);
            ScheduleConsider(person, world.Tick + Wait(world.Random));
        }
    }

    private void ScheduleConsider(Person person, long tick)
    {
        int number = _considerNumbers.GetValueOrDefault(person.Id) + 1;
        _considerNumbers[person.Id] = number;
        Schedule(new Consider(person, number), tick);
    }

    private void Schedule(Step step, long tick) => _steps.Enqueue(step, (tick, _scheduled++));

    private static int Wait(Random random) => random.Next(WaitTicks.Min, WaitTicks.Max + 1);
}
