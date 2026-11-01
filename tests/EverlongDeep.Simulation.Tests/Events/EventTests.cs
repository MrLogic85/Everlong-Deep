using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.Events;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Events;

public class EventTests
{
    private static readonly Thing _tavern = new(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);

    [Fact]
    public void Schedule_DrawsNothingBeforeTheEventHappens()
    {
        var log = new RecordedLog();
        World world = WorldBuilder.Tavern(seed: 0, 8, log);
        var plainLog = new RecordedLog();
        World plain = WorldBuilder.Tavern(seed: 0, 8, plainLog);
        world.Schedule(Scenarios.StrangerKills(world) with { Tick = World.PeriodTicks }, log);

        // Up to its tick, the run is the one without it.
        world.Step(World.PeriodTicks, log);
        plain.Step(World.PeriodTicks, plainLog);
        log.Records.ShouldBe(plainLog.Records);
        world.Random.NextDouble().ShouldBe(plain.Random.NextDouble());

        // At its tick, it happens.
        world.Step(1, log);
        log.Records.OfType<EventHappened>().ShouldHaveSingleItem().Tick.ShouldBe(World.PeriodTicks);
    }

    // Every draw at 0 says each witness saw it and who did it; between a key witness's chances to see who and to see
    // it, the key witnesses saw it but not who, and the bystanders whatever their chances give.
    [Theory]
    [InlineData(0)]
    [InlineData((Event.KeyWitnessSawWhoChance + Event.KeyWitnessSawChance) / 2)]
    public void Happen_StrangerKills_TheNewcomerIsAStranger_TheVictimDies_AndEachOneThereHoldsTheStoryByTheirRole(double sample)
    {
        bool keyWitnessesSawWho = sample < Event.KeyWitnessSawWhoChance;
        bool bystandersSaw = sample < Event.BystanderSawChance;
        bool bystandersSawWho = sample < Event.BystanderSawWhoChance;
        World world = Tavern(new FixedRandom(sample));
        var log = new RecordedLog();
        Event happened = Scenarios.StrangerKills(world) with { Tick = 0 };

        happened.Happen(world, log);

        Person newcomer = world.Person(happened.Actor);
        newcomer.ShouldBeSameAs(happened.Newcomer);
        newcomer.Relations.ShouldBeEmpty();
        world.People.ShouldAllBe(person => person.RelationTo(newcomer.Id) == null);
        world.Person(happened.Target).IsDead.ShouldBeTrue();

        // The actor did it; a witness either saw it, knowing who did it or not, or only caught the commotion.
        Story story = new(Act.Killed, PersonOrThing.Of(happened.Target));
        Belief[] held = [.. world.People.SelectMany(person => person.Beliefs).Where(belief => belief.Claim.Story == story)];
        held.Select(belief => belief.Holder).ShouldBe([happened.Actor, .. happened.KeyWitnesses, .. happened.Bystanders], ignoreOrder: true);
        Claim actors = held.Single(belief => belief.Holder == happened.Actor).Claim;
        (actors.Actor, actors.Witness).ShouldBe((PersonOrThing.Of(happened.Actor), happened.Actor));
        held.Select(belief => belief.Claim.Severity).Distinct().ShouldHaveSingleItem();
        Belief[] key = [.. held.Where(belief => happened.KeyWitnesses.Contains(belief.Holder))];
        key.ShouldAllBe(belief => belief.Claim.Witness == belief.Holder && belief.Claim.Actor.IsUnknown != keyWitnessesSawWho);
        Belief[] bystanders = [.. held.Where(belief => happened.Bystanders.Contains(belief.Holder))];
        if (bystandersSaw)
        {
            bystanders.ShouldAllBe(belief => belief.Claim.Witness == belief.Holder && belief.Claim.Actor.IsUnknown != bystandersSawWho);
        }
        else
        {
            bystanders.ShouldAllBe(belief => belief.Claim.Witness == null && belief.Claim.Actor.IsUnknown);
            bystanders.Max(belief => belief.Confidence).ShouldBeLessThan(key.Min(belief => belief.Confidence));
        }

        // The log tells who came in, and who saw it or only caught the commotion, as their beliefs say.
        EventHappened logged = log.Records.ShouldHaveSingleItem().ShouldBeOfType<EventHappened>();
        (logged.Newcomer, logged.Seen.Actor, logged.Seen.Witness).ShouldBe((newcomer.Id, PersonOrThing.Of(happened.Actor), null));
        logged.Saw.ShouldBe(bystandersSaw ? [.. happened.KeyWitnesses, .. happened.Bystanders] : happened.KeyWitnesses);
        logged.CaughtCommotion.ShouldBe(bystandersSaw ? [] : happened.Bystanders);
    }

    // Mara, left with nobody else, tells the stranger what she holds; holding nothing, she is told what he did.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Happen_TheNewcomer_TalksLikeAnyoneElse_BeingTold_OrOpeningAConversation(bool maraHoldsAStory)
    {
        Person mara = Someone(0);
        Person pell = Someone(1);
        if (maraHoldsAStory)
        {
            mara.Believe(
                new Claim(PersonOrThing.Unknown, Act.WasSeenAt, PersonOrThing.Of(_tavern.Id), _tavern.Id, tick: -5 * 3_600, severity: 0, blame: 0, witness: null),
                confidence: 0.9, importance: 0.5);
        }

        // Every draw says yes.
        var world = new World(seed: 0, new FixedRandom(0), [_tavern], [mara, pell]);
        Person rook = Someone(2);
        var traces = new List<ConversationTrace>();
        world.TraceConversations(traces.Add);
        var log = new RecordedLog();
        world.Schedule(new Event(Act.Killed, rook.Id, pell.Id, _tavern.Id, Tick: 0, KeyWitnesses: [], Bystanders: [], rook), log);
        world.Step(World.PeriodTicks, log);

        ConversationTrace first = traces[0];
        (first.TellerBefore.Id, first.ListenerBefore.Id).ShouldBe(maraHoldsAStory ? (mara.Id, rook.Id) : (rook.Id, mara.Id));
    }

    // The scenario's six people, p0 to p5, in the tavern, strangers to each other and holding nothing.
    private static World Tavern(Random random) =>
        new(seed: 0, random, [_tavern], [.. Enumerable.Range(0, Scenarios.StrangerKillsPeople).Select(Someone)]);

    private static Person Someone(int id) =>
        new(new PersonId(id), $"Person {id}", new TraitProfile(new Dictionary<TraitAxis, double>()), _tavern.Id);
}
