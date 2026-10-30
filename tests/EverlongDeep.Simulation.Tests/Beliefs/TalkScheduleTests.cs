using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public class TalkScheduleTests
{
    private const int _periods = 12;

    // A seeded tavern's talk over twelve periods, each conversation traced.
    private static (SimulationLog Log, ConversationTrace[] Traces)[] Runs() =>
    [
        .. Enumerable.Range(0, 20).Select(seed =>
        {
            var log = new SimulationLog();
            var traces = new List<ConversationTrace>();
            World world = WorldBuilder.Tavern(seed, 8, log);
            world.TraceConversations(traces.Add);
            world.Step(_periods * World.PeriodTicks, log);
            return (log, traces.ToArray());
        }),
    ];

    [Fact]
    public void Step_AStoryHeard_CanBeRetoldWithinTheSamePeriod()
    {
        bool retoldWithinThePeriod = Runs().Any(run => run.Traces.Any(heard => heard.Hearing == Hearing.Believed && run.Traces.Any(retold =>
            retold.TellerBefore.Id == heard.ListenerBefore.Id && retold.Held.Story == heard.Told.Story &&
            retold.StartTick >= heard.EndTick && retold.EndTick / World.PeriodTicks == heard.EndTick / World.PeriodTicks)));

        retoldWithinThePeriod.ShouldBeTrue();
    }

    [Fact]
    public void Step_NobodyIsInTwoConversationsAtOnce()
    {
        foreach ((_, ConversationTrace[] traces) in Runs())
        {
            foreach (PersonId person in traces.SelectMany(trace => new[] { trace.TellerBefore.Id, trace.ListenerBefore.Id }).Distinct())
            {
                ConversationTrace[] theirs =
                    [.. traces.Where(trace => trace.TellerBefore.Id == person || trace.ListenerBefore.Id == person).OrderBy(trace => trace.StartTick)];
                for (int i = 1; i < theirs.Length; i++)
                {
                    theirs[i].StartTick.ShouldBeGreaterThanOrEqualTo(theirs[i - 1].EndTick, $"{person} talks twice at once");
                }
            }
        }
    }

    [Fact]
    public void Step_AcrossSeeds_TheFirstToTalk_IsNotAlwaysTheLowestId()
    {
        int[] firstTellers = [.. Runs().Where(run => run.Traces.Length > 0).Select(run => run.Traces[0].TellerBefore.Id.Value)];

        firstTellers.Length.ShouldBeGreaterThan(10);
        firstTellers.Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void Step_SameSeed_GivesTheSameTalk()
    {
        (SimulationLog Log, ConversationTrace[] Traces)[] runs = Runs();

        runs.ShouldAllBe(run => run.Traces.Length > 0);
        runs.SelectMany(run => run.Traces).ShouldContain(trace => trace.ApproachStranger != null && trace.ApproachStranger.Value.Yes);
        Runs().Select(run => run.Log.Records).ShouldBe(runs.Select(run => run.Log.Records));
    }

    [Fact]
    public void Step_Traced_ChangesNothing_AndTracesEachConversationOnItsOwnLine()
    {
        foreach (int seed in Enumerable.Range(0, 20))
        {
            (World plain, SimulationLog plainLog) = Run(seed, traces: null);
            var traces = new List<ConversationTrace>();
            (World traced, SimulationLog tracedLog) = Run(seed, traces);

            tracedLog.Records.ShouldBe(plainLog.Records);
            State(traced).ShouldBe(State(plain));
            traces.ShouldNotBeEmpty();
            traces.ShouldAllBe(trace => tracedLog.Records.Count(record => ReferenceEquals(record, trace.Ended)) == 1);
        }

        static (World, SimulationLog) Run(int seed, List<ConversationTrace>? traces)
        {
            var log = new SimulationLog();
            World world = WorldBuilder.Tavern(seed, 8, log);
            if (traces is not null)
            {
                world.TraceConversations(traces.Add);
            }

            world.Step(_periods * World.PeriodTicks, log);
            return (world, log);
        }

        // Everything a conversation can change, and the next draw, which shows no extra draw was made.
        static string[] State(World world) =>
        [
            .. world.People.SelectMany(person =>
                person.Beliefs.Select(belief => $"{person.Id} {belief.Claim} {belief.Confidence}")
                    .Concat(person.Relations.Select(relation => $"{person.Id} {relation.Other} {relation.Opinion} {relation.Trust} {relation.Ties}"))),
            $"next {world.Random.NextDouble()}",
        ];
    }

    [Fact]
    public void Kill_MidConversationOrWaiting_TheDeadNeverTalkListenOrChangeAgain_AndTheLivingTalkOn_AndTheCutOnesAreLoggedCutShort()
    {
        // A run left alive shows two conversations open at the death tick, and someone only waiting for a moment.
        ConversationTrace[] alive = RunWithDeaths(out _, out _, [], onDeath: null);
        ConversationTrace[] open = [.. alive.Where(trace => trace.StartTick < _deathTick && trace.EndTick > _deathTick)];
        open.Length.ShouldBeGreaterThanOrEqualTo(2, "the seed needs two conversations open at the death tick");
        PersonId[] busy = [.. open.SelectMany(trace => new[] { trace.TellerBefore.Id, trace.ListenerBefore.Id })];
        PersonId waiting = alive.First(trace => trace.StartTick >= _deathTick && !busy.Contains(trace.TellerBefore.Id)).TellerBefore.Id;

        // One conversation loses its listener and the other its teller.
        PersonId[] dead = [open[0].ListenerBefore.Id, open[1].TellerBefore.Id, waiting];
        PersonId[] survivors = [open[0].TellerBefore.Id, open[1].ListenerBefore.Id];
        string[] atDeath = [];
        ConversationTrace[] after =
        [
            .. RunWithDeaths(out World world, out SimulationLog log, dead, onDeath: died => atDeath = [.. dead.Select(id => Describe(died.Person(id)))])
                .Where(trace => trace.EndTick > _deathTick),
        ];

        // A conversation writes its end and its trace together as it ends, so no trace means the cut ones did nothing.
        after.ShouldAllBe(trace => !dead.Contains(trace.TellerBefore.Id) && !dead.Contains(trace.ListenerBefore.Id), "the dead talk or listen");
        dead.Select(id => Describe(world.Person(id))).ShouldBe(atDeath, "the dead changed");
        survivors.ShouldAllBe(survivor => after.Any(trace => trace.TellerBefore.Id == survivor || trace.ListenerBefore.Id == survivor), "a survivor never talks again");

        // Every end follows its start, and the cut ones are logged cut short, not ended; only conversations begun after
        // the deaths are still open as the run ends.
        StillOpen(log).Values.ShouldAllBe(start => start >= _deathTick);
        log.Records.OfType<ConversationCutShort>().Select(cut => (cut.Teller, cut.Listener))
            .ShouldBe(open[..2].Select(trace => (trace.TellerBefore.Id, trace.ListenerBefore.Id)), ignoreOrder: true);
    }

    // The conversations a log started and never ended or cut short, by pair, with when each began. Nobody is in two
    // conversations at once, so a pair has one open at most.
    private static Dictionary<(PersonId Teller, PersonId Listener), long> StillOpen(SimulationLog log)
    {
        var open = new Dictionary<(PersonId Teller, PersonId Listener), long>();
        foreach (LogRecord record in log.Records)
        {
            switch (record)
            {
                case ConversationStarted started:
                    open.Add((started.Teller, started.Listener), started.Tick);
                    break;
                case ConversationEnded ended:
                    Close(ended, ended.Teller, ended.Listener, ended.StartTick);
                    break;
                case ConversationCutShort cut:
                    Close(cut, cut.Teller, cut.Listener, cut.StartTick);
                    break;
            }
        }

        return open;

        void Close(LogRecord record, PersonId teller, PersonId listener, long startTick)
        {
            open.Remove((teller, listener), out long start).ShouldBeTrue($"{record} has no start");
            startTick.ShouldBe(start);
            start.ShouldBeLessThanOrEqualTo(record.Tick);
        }
    }

    [Fact]
    public void Kill_SomeoneWaitingToTalk_NeverApproachesAnyone()
    {
        var tavern = new Thing(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);
        Person mara = Someone(0, 0.6);
        Person pell = Someone(1, 0);
        mara.Believe(
            new Claim(PersonOrThing.Unknown, Act.WasSeenAt, PersonOrThing.Of(tavern.Id), tavern.Id, tick: -5 * 3_600, severity: 0, blame: 0, witness: null),
            confidence: 0.9, importance: 0.5);
        var world = new World(seed: 0, new FixedRandom(0), [tavern], [mara, pell]);

        // Every draw says yes, so a living Mara would approach the stranger Pell at once.
        world.Kill(mara.Id);
        world.Step(World.PeriodTicks, new SimulationLog());

        pell.RelationTo(mara.Id).ShouldBeNull();
    }

    [Fact]
    public void Step_SomeoneWhoKeepsQuiet_ConsidersAgainLater()
    {
        var tavern = new Thing(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);
        Person mara = Someone(0, 0.6);
        Person pell = Someone(1, 0);
        mara.Meet(pell.Id, opinion: 0.4, trust: 0.6);
        pell.Meet(mara.Id, opinion: 0.4, trust: 0.6);
        mara.Believe(
            new Claim(PersonOrThing.Unknown, Act.WasSeenAt, PersonOrThing.Of(tavern.Id), tavern.Id, tick: -5 * 3_600, severity: 0, blame: 0, witness: null),
            confidence: 0.9, importance: 0.5);

        // Mara's moment comes first. She keeps quiet at it, then considers again later, and every later draw says yes:
        // she tells Pell.
        var world = new World(seed: 0, new ScriptedThenFixedRandom(0, 0, 0, 0.5, 0.99), [tavern], [mara, pell]);
        var traces = new List<ConversationTrace>();
        world.TraceConversations(traces.Add);
        world.Step(World.PeriodTicks, new SimulationLog());

        ConversationTrace first = traces[0];
        (first.TellerBefore.Id, first.ListenerBefore.Id).ShouldBe((mara.Id, pell.Id));
        first.StartTick.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Length_GrowsWithHowTalkativeThePairIs_AndLastsAtLeastTheShortest()
    {
        int[] lengths = [.. new[] { -1, -0.5, 0, 0.5, 1 }.Select(talkative => TalkSchedule.Length(Someone(0, talkative), Someone(1, talkative)))];

        lengths.ShouldBe(lengths.Order());
        lengths[^1].ShouldBeGreaterThan(lengths[0]);
        lengths[0].ShouldBe(TalkSchedule.ShortestTicks);
    }

    private const int _deathTick = World.PeriodTicks;

    // A full seeded tavern traced for twelve periods, with each of the dead killed at the death tick.
    private static ConversationTrace[] RunWithDeaths(out World world, out SimulationLog log, PersonId[] dead, Action<World>? onDeath)
    {
        log = new SimulationLog();
        world = WorldBuilder.Tavern(seed: 0, WorldBuilder.MaxPeople, log);
        var traces = new List<ConversationTrace>();
        world.TraceConversations(traces.Add);
        world.Step(_deathTick, log);
        foreach (PersonId id in dead)
        {
            world.Kill(id);
        }

        onDeath?.Invoke(world);
        world.Step(_periods * World.PeriodTicks, log);
        return [.. traces];
    }

    // Everything a person holds that could change.
    private static string Describe(Person person) =>
        string.Join("; ", person.Beliefs.Select(belief => $"{belief.Claim} {belief.Confidence} {belief.Relevance} {belief.HeardFrom.Count} {belief.ToldTo.Count}")
            .Concat(person.Relations.Select(relation =>
                $"{relation.Other} {relation.Opinion} {relation.Trust} {relation.Ties} {relation.FriendChangeTicks} {relation.RivalChangeTicks}")));

    private static Person Someone(int id, double talkative) =>
        new(new PersonId(id), $"Person {id}", new TraitProfile(new Dictionary<TraitAxis, double> { [TraitAxis.Talkative] = talkative }), new ThingId(0));
}
