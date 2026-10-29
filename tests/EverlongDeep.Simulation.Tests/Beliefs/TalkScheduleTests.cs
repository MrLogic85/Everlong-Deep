using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public class TalkScheduleTests
{
    private const int _hours = 12;

    // A seeded tavern's talk over half a day, each conversation traced.
    private static (SimulationLog Log, ConversationTrace[] Traces)[] Runs() =>
    [
        .. Enumerable.Range(0, 20).Select(seed =>
        {
            var log = new SimulationLog();
            var traces = new List<ConversationTrace>();
            World world = WorldBuilder.Tavern(seed, 8, log);
            world.TraceConversations(traces.Add);
            for (int hour = 0; hour < _hours; hour++)
            {
                world.StepHour(log);
            }

            return (log, traces.ToArray());
        }),
    ];

    [Fact]
    public void StepHour_AStoryHeard_CanBeRetoldWithinTheSameHour()
    {
        bool retoldWithinTheHour = Runs().Any(run => run.Traces.Any(heard => heard.Hearing == Hearing.Believed && run.Traces.Any(retold =>
            retold.TellerBefore.Id == heard.ListenerBefore.Id && retold.Held.Story == heard.Told.Story &&
            retold.StartMinute >= heard.EndMinute && retold.EndMinute / 60 == heard.EndMinute / 60)));

        retoldWithinTheHour.ShouldBeTrue();
    }

    [Fact]
    public void StepHour_NobodyIsInTwoConversationsAtOnce()
    {
        foreach ((_, ConversationTrace[] traces) in Runs())
        {
            foreach (PersonId person in traces.SelectMany(trace => new[] { trace.TellerBefore.Id, trace.ListenerBefore.Id }).Distinct())
            {
                ConversationTrace[] theirs =
                    [.. traces.Where(trace => trace.TellerBefore.Id == person || trace.ListenerBefore.Id == person).OrderBy(trace => trace.StartMinute)];
                for (int i = 1; i < theirs.Length; i++)
                {
                    theirs[i].StartMinute.ShouldBeGreaterThanOrEqualTo(theirs[i - 1].EndMinute, $"{person} talks twice at once");
                }
            }
        }
    }

    [Fact]
    public void StepHour_AcrossSeeds_TheFirstToTalk_IsNotAlwaysTheLowestId()
    {
        int[] firstTellers = [.. Runs().Where(run => run.Traces.Length > 0).Select(run => run.Traces[0].TellerBefore.Id.Value)];

        firstTellers.Length.ShouldBeGreaterThan(10);
        firstTellers.Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void StepHour_SameSeed_GivesTheSameTalk()
    {
        (SimulationLog Log, ConversationTrace[] Traces)[] runs = Runs();

        runs.ShouldAllBe(run => run.Traces.Length > 0);
        runs.SelectMany(run => run.Traces).ShouldContain(trace => trace.ApproachStranger != null && trace.ApproachStranger.Value.Yes);
        Runs().Select(run => run.Log.Lines).ShouldBe(runs.Select(run => run.Log.Lines));
    }

    [Fact]
    public void StepHour_Traced_ChangesNothing_AndTracesEachConversationOnItsOwnLine()
    {
        foreach (int seed in Enumerable.Range(0, 20))
        {
            (World plain, SimulationLog plainLog) = Run(seed, traces: null);
            var traces = new List<ConversationTrace>();
            (World traced, SimulationLog tracedLog) = Run(seed, traces);

            tracedLog.Lines.ShouldBe(plainLog.Lines);
            State(traced).ShouldBe(State(plain));
            traces.ShouldNotBeEmpty();
            traces.Select(trace => trace.LogLine).ShouldBeUnique();
        }

        static (World, SimulationLog) Run(int seed, List<ConversationTrace>? traces)
        {
            var log = new SimulationLog();
            World world = WorldBuilder.Tavern(seed, 8, log);
            if (traces is not null)
            {
                world.TraceConversations(traces.Add);
            }

            for (int hour = 0; hour < _hours; hour++)
            {
                world.StepHour(log);
            }

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
    public void StepHour_SomeoneWhoKeepsQuiet_ConsidersAgainLater()
    {
        var tavern = new Thing(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);
        Person mara = Someone(0, 0.6);
        Person pell = Someone(1, 0);
        mara.Meet(pell.Id, opinion: 0.4, trust: 0.6);
        pell.Meet(mara.Id, opinion: 0.4, trust: 0.6);
        mara.Believe(
            new Claim(PersonOrThing.Unknown, Act.WasSeenAt, PersonOrThing.Of(tavern.Id), tavern.Id, hour: -5, severity: 0, blame: 0, witness: null),
            confidence: 0.9, importance: 0.5);

        // Mara's moment comes first. She keeps quiet at it, then considers again later, and every later draw says yes:
        // she tells Pell.
        var world = new World(seed: 0, new ScriptedThenFixedRandom(0, 0, 0, 0.5, 0.99), [tavern], [mara, pell]);
        var traces = new List<ConversationTrace>();
        world.TraceConversations(traces.Add);
        world.StepHour(new SimulationLog());

        ConversationTrace first = traces[0];
        (first.TellerBefore.Id, first.ListenerBefore.Id).ShouldBe((mara.Id, pell.Id));
        first.StartMinute.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Length_GrowsWithHowTalkativeThePairIs_AndLastsAtLeastAMinute()
    {
        int[] lengths = [.. new[] { -1, -0.5, 0, 0.5, 1 }.Select(talkative => TalkSchedule.Length(Someone(0, talkative), Someone(1, talkative)))];

        lengths.ShouldBe(lengths.Order());
        lengths[^1].ShouldBeGreaterThan(lengths[0]);
        lengths[0].ShouldBe(1);
    }

    private static Person Someone(int id, double talkative) =>
        new(new PersonId(id), $"Person {id}", new TraitProfile(new Dictionary<TraitAxis, double> { [TraitAxis.Talkative] = talkative }), new ThingId(0));
}
