using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public class TalkScheduleTests
{
    private static readonly Thing _tavern = new(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);
    private static readonly Thing _ship = new(new ThingId(1), "Saltwren", ThingKind.Ship, 0.5);

    [Fact]
    public void Step_AStoryHeard_CanBeRetoldWithinTheSamePeriod()
    {
        Person mara = Someone(0, 0.6);
        Person pell = Someone(1, 0);
        Person bram = Someone(2, 0);
        Belief story = mara.Believe(SaltwrenSank(), confidence: 0.9, importance: 0.5);

        // Every draw says yes: Mara tells Pell, then Bram, and Pell, free again, tells Bram what he heard.
        var world = new World(seed: 0, new FixedRandom(0), [_tavern, _ship], [mara, pell, bram]);
        ConversationTrace[] traces = Run(world, World.PeriodTicks);

        ConversationTrace heard = traces[0];
        (heard.TellerBefore.Id, heard.ListenerBefore.Id, heard.Hearing).ShouldBe((mara.Id, pell.Id, Hearing.Believed));
        traces.ShouldContain(retold => retold.TellerBefore.Id == pell.Id && retold.Held.Story == story.Claim.Story && retold.EndTick < World.PeriodTicks);
    }

    [Fact]
    public void Step_NobodyIsInTwoConversationsAtOnce()
    {
        Person[] people = [Someone(0, 0.6), Someone(1, 0.6), Someone(2, 0.6)];
        foreach (Person person in people)
        {
            person.Believe(SaltwrenSank(), confidence: 0.9, importance: 0.5);
        }

        // Every draw says yes, so all three want to talk at once, and the third finds the other two busy.
        var world = new World(seed: 0, new FixedRandom(0), [_tavern, _ship], people);
        ConversationTrace[] traces = Run(world, World.PeriodTicks);

        traces.ShouldNotBeEmpty();
        foreach (Person person in people)
        {
            ConversationTrace[] theirs =
                [.. traces.Where(trace => trace.TellerBefore.Id == person.Id || trace.ListenerBefore.Id == person.Id).OrderBy(trace => trace.StartTick)];
            for (int i = 1; i < theirs.Length; i++)
            {
                theirs[i].StartTick.ShouldBeGreaterThanOrEqualTo(theirs[i - 1].EndTick, $"{person.Id} talks twice at once");
            }
        }
    }

    [Fact]
    public void Step_TheFirstMoments_GoInAShuffledOrder_NotById()
    {
        Person mara = Someone(0, 0.6);
        Person pell = Someone(1, 0.6);
        mara.Believe(SaltwrenSank(), confidence: 0.9, importance: 0.5);
        pell.Believe(SaltwrenSank(), confidence: 0.9, importance: 0.5);

        // The shuffle's one draw swaps them; every draw after it says yes and gives both their first moment at once.
        var world = new World(seed: 0, new ScriptedThenFixedRandom(0, 0.99), [_tavern, _ship], [mara, pell]);
        ConversationTrace[] traces = Run(world, World.PeriodTicks);

        traces[0].TellerBefore.Id.ShouldBe(pell.Id);
    }

    [Fact]
    public void Step_Traced_ChangesNothing_AndTracesEachConversationOnItsOwnLine()
    {
        (World plain, RecordedLog plainLog) = Run(traces: null);
        var traces = new List<ConversationTrace>();
        (World traced, RecordedLog tracedLog) = Run(traces);

        tracedLog.Records.ShouldBe(plainLog.Records);
        State(traced).ShouldBe(State(plain));
        traces.ShouldNotBeEmpty();
        traces.ShouldAllBe(trace => tracedLog.Records.Count(record => ReferenceEquals(record, trace.Ended)) == 1);

        // A seeded tavern, so the next draw shows whether tracing drew anything.
        static (World, RecordedLog) Run(List<ConversationTrace>? traces)
        {
            var log = new RecordedLog();
            World world = WorldBuilder.Tavern(seed: 0, 8, log);
            if (traces is not null)
            {
                world.TraceConversations(traces.Add);
            }

            world.Step(World.PeriodTicks, log);
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
    public void Kill_MidConversation_TheDeadNeverTalkListenOrChangeAgain_AndTheLivingTalkOn_AndTheCutOnesAreLoggedCutShort()
    {
        Person mara = Someone(0, 0.6);
        Person pell = Someone(1, 0.6);
        Person bram = Someone(2, 0.6);
        Person jory = Someone(3, 0.6);
        Person[] people = [mara, pell, bram, jory];
        foreach (Person person in people)
        {
            person.Believe(SaltwrenSank(), confidence: 0.9, importance: 0.5);
        }

        // Every draw says yes: Mara talks to Pell and Bram to Jory, both from tick 0.
        var world = new World(seed: 0, new FixedRandom(0), [_tavern, _ship], people);
        var log = new RecordedLog();
        var traces = new List<ConversationTrace>();
        world.TraceConversations(traces.Add);
        world.Step(1, log);
        log.Records.OfType<ConversationStarted>().ShouldBe([new ConversationStarted(0, mara.Id, pell.Id), new ConversationStarted(0, bram.Id, jory.Id)]);

        // One conversation loses its listener and the other its teller.
        world.Kill(pell.Id);
        world.Kill(bram.Id);
        string[] atDeath = [Describe(pell), Describe(bram)];
        world.Step(World.PeriodTicks - 1, log);

        // A conversation writes its end and its trace together as it ends, so no trace means the cut ones did nothing.
        PersonId[] dead = [pell.Id, bram.Id];
        traces.ShouldAllBe(trace => !dead.Contains(trace.TellerBefore.Id) && !dead.Contains(trace.ListenerBefore.Id), "the dead talk or listen");
        new[] { Describe(pell), Describe(bram) }.ShouldBe(atDeath, "the dead changed");
        traces.ShouldContain(trace => trace.TellerBefore.Id == mara.Id && trace.ListenerBefore.Id == jory.Id, "the survivors never talk again");
        log.Records.OfType<ConversationCutShort>()
            .ShouldBe([new ConversationCutShort(TalkSchedule.ConversationTicks, mara.Id, pell.Id, 0), new ConversationCutShort(TalkSchedule.ConversationTicks, bram.Id, jory.Id, 0)], ignoreOrder: true);
    }

    [Fact]
    public void Kill_SomeoneWaitingToTalk_NeverApproachesAnyone()
    {
        Person mara = Someone(0, 0.6);
        Person pell = Someone(1, 0);
        mara.Believe(SaltwrenSank(), confidence: 0.9, importance: 0.5);
        var world = new World(seed: 0, new FixedRandom(0), [_tavern, _ship], [mara, pell]);

        // Every draw says yes, so a living Mara would approach the stranger Pell at once.
        world.Kill(mara.Id);
        world.Step(World.PeriodTicks, new RecordedLog());

        pell.RelationTo(mara.Id).ShouldBeNull();
    }

    [Fact]
    public void Step_SomeoneWhoKeepsQuiet_ConsidersAgainLater()
    {
        Person mara = Someone(0, 0.6);
        Person pell = Someone(1, 0);
        mara.Meet(pell.Id, opinion: 0.4, trust: 0.6);
        pell.Meet(mara.Id, opinion: 0.4, trust: 0.6);
        mara.Believe(SaltwrenSank(), confidence: 0.9, importance: 0.5);

        // Mara's moment comes first. She keeps quiet at it, then considers again later, and every later draw says yes:
        // she tells Pell.
        var world = new World(seed: 0, new ScriptedThenFixedRandom(0, 0, 0, 0.5, 0.99), [_tavern, _ship], [mara, pell]);
        ConversationTrace first = Run(world, World.PeriodTicks)[0];
        (first.TellerBefore.Id, first.ListenerBefore.Id).ShouldBe((mara.Id, pell.Id));
        first.StartTick.ShouldBeGreaterThan(0);
    }

    // Steps the world and returns the conversations it traced.
    private static ConversationTrace[] Run(World world, long ticks)
    {
        var traces = new List<ConversationTrace>();
        world.TraceConversations(traces.Add);
        world.Step(ticks, new RecordedLog());
        return [.. traces];
    }

    // A story that matters to anyone who hears it, so a listener has it to tell on.
    private static Claim SaltwrenSank() =>
        new(PersonOrThing.Unknown, Act.Sank, PersonOrThing.Of(_ship.Id), _ship.Id, tick: -5 * 3_600, severity: 0.6, blame: 0, witness: null);

    // Everything a person holds that could change.
    private static string Describe(Person person) =>
        string.Join("; ", person.Beliefs.Select(belief => $"{belief.Claim} {belief.Confidence} {belief.Relevance} {belief.HeardFrom.Count} {belief.ToldTo.Count}")
            .Concat(person.Relations.Select(relation =>
                $"{relation.Other} {relation.Opinion} {relation.Trust} {relation.Ties} {relation.FriendChangeTicks} {relation.RivalChangeTicks}")));

    private static Person Someone(int id, double talkative) =>
        new(new PersonId(id), $"Person {id}", new TraitProfile(new Dictionary<TraitAxis, double> { [TraitAxis.Talkative] = talkative }), _tavern.Id);
}
