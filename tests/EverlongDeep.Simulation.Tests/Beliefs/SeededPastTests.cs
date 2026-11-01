using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public class SeededPastTests
{
    private const double _tolerance = 1e-9;

    private static readonly Thing _tavern = new(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);
    private static readonly Thing _ship = new(new ThingId(1), "Saltwren", ThingKind.Ship, 0.5);
    private static readonly Thing _item = new(new ThingId(2), "brass watch", ThingKind.Item, 0.5);

    // The rest of the draws, all at 0 or all at 0.99, pick the actor and every other choice; at 0 a sinking's or a
    // robbery's actor is unknown, and at 0.99 known.
    [Theory]
    [InlineData(Act.Sank, 0)]
    [InlineData(Act.Sank, 0.99)]
    [InlineData(Act.Robbed, 0)]
    [InlineData(Act.Robbed, 0.99)]
    [InlineData(Act.Attacked, 0)]
    [InlineData(Act.Betrayed, 0)]
    [InlineData(Act.Helped, 0)]
    [InlineData(Act.WasSeenAt, 0)]
    public void Draw_EachAct_FitsTheActTable_AndNamesTheWorld(Act act, double rest)
    {
        World world = Drawn(ActThen(act, rest));

        Claim claim = Events(world).Single(story => story.Key.Act == act).Key;
        claim.Tick.ShouldBeInRange(-SeededPast.PastTicks, -1);
        claim.Actor.ShouldNotBe(claim.Target);
        claim.Actor.Thing.ShouldBeNull();
        claim.Severity.ShouldBeInRange(0, 1);
        bool blamed = Claim.IsHarmful(act) && !claim.Actor.IsUnknown;
        if (blamed)
        {
            claim.Blame.ShouldBeInRange(Claim.HarmfulBlame.Min, Claim.HarmfulBlame.Max);
        }
        else
        {
            claim.Blame.ShouldBe(0);
        }

        // Only a sinking or a robbery may leave its actor unknown.
        switch (act)
        {
            case Act.Sank:
                (claim.Target, claim.Place).ShouldBe((PersonOrThing.Of(_ship.Id), _ship.Id));
                claim.Actor.IsUnknown.ShouldBe(rest < SeededPast.UnknownActorShare);
                break;
            case Act.Robbed:
                (claim.Target, claim.Place).ShouldBe((PersonOrThing.Of(_item.Id), _tavern.Id));
                claim.Actor.IsUnknown.ShouldBe(rest < SeededPast.UnknownActorShare);
                break;
            case Act.Attacked or Act.Betrayed or Act.Helped:
                claim.Actor.IsUnknown.ShouldBeFalse();
                world.Person(claim.Target.Person.ShouldNotBeNull());
                claim.Place.ShouldBe(_tavern.Id);
                claim.Severity.ShouldBe(Claim.BaseSeverity(act));
                break;
            case Act.WasSeenAt:
                claim.Actor.IsUnknown.ShouldBeFalse();
                claim.Target.Thing.ShouldBeOneOf(_ship.Id, _tavern.Id);
                claim.Place.ShouldBe(claim.Target.Thing!.Value);
                claim.Severity.ShouldBe(0);
                break;
        }
    }

    // A robbery below the unknown actor's share has an unknown actor; an attack has a known one, and between the shares
    // hearsay keeps the witness, while above both it drops it.
    [Theory]
    [InlineData(Act.Robbed, SeededPast.UnknownActorShare - 0.05)]
    [InlineData(Act.Attacked, (SeededPast.UnknownActorShare + SeededPast.HearsayKeepsWitnessShare) / 2)]
    [InlineData(Act.Attacked, (1 + SeededPast.HearsayKeepsWitnessShare) / 2)]
    public void Draw_EachHolder_HoldsTheEventByTheirRole_AtTheirRolesConfidence_WithRelevanceAtImportance(Act act, double rest)
    {
        World world = Drawn(ActThen(act, rest));

        IGrouping<Claim, Belief> story = Events(world).Single(story => story.Key.Act == act);
        Claim claim = story.Key;
        Belief[] firstHand = [.. story.Where(belief => belief.IsFirstHand)];
        Belief[] hearsay = [.. story.Where(belief => !belief.IsFirstHand)];
        hearsay.ShouldNotBeEmpty();

        // A known actor holds what they did first-hand, once, and is the only one named as the witness of their own act.
        PersonId[] witnesses = [.. OtherWitnesses(story).Select(belief => belief.Holder)];
        witnesses.ShouldNotBeEmpty();
        if (claim.Actor.Person is { } actor)
        {
            Belief own = story.Where(belief => belief.Holder == actor).ShouldHaveSingleItem();
            (own.IsFirstHand, own.Claim).ShouldBe((true, claim.WithWitness(actor)));
        }

        firstHand.ShouldAllBe(belief => belief.Claim.WitnessDidIt == ((PersonId?)belief.Holder == claim.Actor.Person));
        firstHand.ShouldAllBe(belief => belief.Confidence >= SeededPast.FirstHandConfidence.Min && belief.Confidence <= SeededPast.FirstHandConfidence.Max);

        // Hearsay is the same claim, keeping one of the witnesses or none, and held less surely.
        bool keepsWitness = rest < SeededPast.HearsayKeepsWitnessShare;
        hearsay.ShouldAllBe(belief => belief.Claim.WithWitness(null) == claim);
        hearsay.ShouldAllBe(belief => keepsWitness ? witnesses.Contains(belief.Claim.Witness!.Value) : belief.Claim.Witness == null);
        hearsay.ShouldAllBe(belief => belief.Confidence >= SeededPast.HearsayConfidence.Min && belief.Confidence <= SeededPast.HearsayConfidence.Max);
        hearsay.Max(belief => belief.Confidence).ShouldBeLessThan(firstHand.Min(belief => belief.Confidence));

        // The past is told before any coloring, so its severity is the table's; ties and opinions only add.
        foreach (Belief belief in story)
        {
            double ownStake = claim.Involves(belief.Holder) ? Importance.OwnStoryStake : 0;
            belief.Importance.ShouldBeInRange(Math.Min(1, claim.Severity + ownStake) - _tolerance, 1);
            belief.Relevance.ShouldBe(belief.Importance, _tolerance);
        }
    }

    [Fact]
    public void Draw_ThreePeople_NeverHaveEveryoneAsAWitness()
    {
        // Every draw at 0.99 takes the most witnesses it can.
        World world = Drawn(new FixedRandom(0.99), peopleCount: 3);

        int witnesses = OtherWitnesses(Events(world).ShouldHaveSingleItem()).Count();
        witnesses.ShouldBeGreaterThanOrEqualTo(1);
        witnesses.ShouldBeLessThan(3);
    }

    [Fact]
    public void Draw_AStoryAlreadyInThePast_IsLeftOut()
    {
        // Every draw gives the same claim, so the first event's story fills the past, though three events were due.
        World world = Drawn(new FixedRandom(0.4));

        Events(world).ShouldHaveSingleItem();
        world.People.ShouldAllBe(person => person.Beliefs.Count <= 1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Tavern_FewerThanTwoPeople_HaveNoPast(int peopleCount)
    {
        World world = WorldBuilder.Tavern(seed: 42, peopleCount, new RecordedLog());

        Beliefs(world).ShouldBeEmpty();
    }

    [Fact]
    public void Tavern_TwoPeople_EachEventIsSeenByThePersonWhoIsNotTheActor()
    {
        World world = WorldBuilder.Tavern(seed: 42, peopleCount: 2, new RecordedLog());

        IGrouping<Claim, Belief> story = Events(world).ShouldHaveSingleItem();
        Belief witness = OtherWitnesses(story).ShouldHaveSingleItem();
        ((PersonId?)witness.Holder).ShouldNotBe(story.Key.Actor.Person);
    }

    [Fact]
    public void Tavern_SameSeed_DrawsTheSamePast()
    {
        var firstLog = new RecordedLog();
        var secondLog = new RecordedLog();

        World first = WorldBuilder.Tavern(seed: 7, peopleCount: 12, firstLog);
        World second = WorldBuilder.Tavern(seed: 7, peopleCount: 12, secondLog);

        Beliefs(first).ShouldNotBeEmpty();
        Describe(second).ShouldBe(Describe(first));
        secondLog.Records.ShouldBe(firstLog.Records);
        Describe(WorldBuilder.Tavern(seed: 8, peopleCount: 12, new RecordedLog())).ShouldNotBe(Describe(first));

        // The log opens with the setup, then each past event with its witnesses.
        firstLog.Records[0].ShouldBe(new WorldBuilt(Tick: 0, Seed: 7, PeopleAsked: 12, People: 12));
        PastEvent[] past = [.. firstLog.Records.OfType<PastEvent>()];
        past.Select(logged => logged.Claim).ShouldBe(Events(first).Select(story => story.Key), ignoreOrder: true);
        foreach (PastEvent logged in past)
        {
            logged.Witnesses.ShouldBe(
                OtherWitnesses(Events(first).Single(story => story.Key == logged.Claim)).Select(belief => belief.Holder), ignoreOrder: true);
        }
    }

    // peopleCount people, nine by default, in the tavern, strangers to each other, with the past drawn by random.
    private static World Drawn(Random random, int peopleCount = 9)
    {
        Person[] people =
            [.. Enumerable.Range(0, peopleCount).Select(id => new Person(new PersonId(id), $"Person {id}", new TraitProfile(new Dictionary<TraitAxis, double>()), _tavern.Id))];
        var world = new World(seed: 0, random, [_tavern, _ship, _item], people);
        SeededPast.Draw(world, _tavern.Id, new RecordedLog());
        return world;
    }

    // The first draw picks act, at the middle of its share; every draw after it is rest.
    private static Random ActThen(Act act, double rest) =>
        new ScriptedThenFixedRandom(rest, (SeededPast.DrawnActs.ToList().IndexOf(act) + 0.5) / SeededPast.DrawnActs.Count);

    private static IEnumerable<Belief> Beliefs(World world) => world.People.SelectMany(person => person.Beliefs);

    // An event's claims differ only in their witness.
    private static IGrouping<Claim, Belief>[] Events(World world) =>
        [.. Beliefs(world).GroupBy(belief => belief.Claim.WithWitness(null))];

    // The witnesses other than the actor, whom the log names as having seen it.
    private static IEnumerable<Belief> OtherWitnesses(IGrouping<Claim, Belief> story) =>
        story.Where(belief => belief.IsFirstHand && (PersonId?)belief.Holder != story.Key.Actor.Person);

    private static string[] Describe(World world) =>
        [
            .. Beliefs(world).Select(belief =>
                $"{belief.Holder.Value} {belief.Claim} {belief.Confidence} {belief.Relevance}"),
        ];
}
