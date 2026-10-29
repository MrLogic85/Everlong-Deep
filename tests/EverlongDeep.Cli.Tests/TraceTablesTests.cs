using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Cli.Tests;

public class TraceTablesTests
{
    private static readonly Thing _tavern = new(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);
    private static readonly Thing _heron = new(new ThingId(1), "Grey Heron", ThingKind.Ship, 0.5);
    private static readonly Thing _logbook = new(new ThingId(2), "logbook", ThingKind.Item, 0.3);
    private static readonly PersonId _maraId = new(0);
    private static readonly PersonId _pellId = new(1);
    private static readonly PersonId _bramId = new(2);
    private static readonly PersonId _fenId = new(3);
    private static readonly PersonId _ilseId = new(4);

    private readonly World _world = new(
        seed: 0,
        new Random(0),
        [_tavern, _heron, _logbook],
        [Someone(_maraId, "Mara"), Someone(_pellId, "Pell"), Someone(_bramId, "Bram"), Someone(_fenId, "Fen"), Someone(_ilseId, "Ilse")]);

    // Bram sank the Grey Heron at the tavern, and Fen saw it.
    private static readonly Claim _sank = new(Of(_bramId), Act.Sank, Of(_heron.Id), _tavern.Id, hour: -9, severity: 0.6, blame: 0.4, _fenId);

    [Fact]
    public void Changes_ListEachNewBelief_MovedFeeling_ChangedImpression_AndNewRelation_ButNoTelling()
    {
        var seen = new Claim(Of(_bramId), Act.WasSeenAt, Of(_tavern.Id), _tavern.Id, hour: -5, severity: 0, blame: 0, _bramId);
        var helped = new Claim(Of(_maraId), Act.Helped, Of(_bramId), _tavern.Id, hour: -9, severity: 0.1, blame: 0, witness: null);
        BeliefSnapshot held = Snapshot(helped, heard: [new HeardEntry(_maraId, 10, helped)]);
        PersonSnapshot before = Pell([Relation(_maraId, 0.2, 0.6, (TraitAxis.Kind, 0.1))], [held]);
        PersonSnapshot after = Pell(
            [Relation(_maraId, 0.3, 0.5, (TraitAxis.Kind, 0.2)), Relation(_bramId, 0, 0.3)],
            [
                held with { ToldTo = [new ToldEntry(_bramId, 64, helped, helped)] },
                Snapshot(seen, heard: [new HeardEntry(_bramId, 70, seen)], confidence: 0.54),
            ]);

        TraceTables.Changes(_world, before, after).ShouldBe(
        [
            "p1 Pell now believes: p2 Bram was seen at t0 The Drowned Lantern, p2 Bram did it, hour -5 (Imp 0.10, Conf 0.54, Rel 0.10, Sev 0.00, Blame 0.00)",
            "p1 Pell's opinion of p0 Mara: 0.20 → 0.30",
            "p1 Pell's trust in p0 Mara: 0.60 → 0.50",
            "p1 Pell's impressions of p0 Mara: Kind +0.10 → Kind +0.20",
            "p1 Pell met p2 Bram: opinion 0.00, trust 0.30",
        ]);
    }

    [Fact]
    public void Changes_ListAHeldBeliefsNewVersion_Importance_Confidence_AndRelevance()
    {
        var byBram = new Claim(Of(_bramId), Act.Attacked, Of(_maraId), _tavern.Id, hour: -9, severity: 0.5, blame: 0, witness: null);
        Claim byPell = new(Of(_pellId), Act.Attacked, Of(_maraId), _tavern.Id, hour: -9, severity: 0.5, blame: 0, witness: null);
        BeliefSnapshot held = Snapshot(byBram);
        PersonSnapshot before = Pell([], [held]);

        TraceTables.Changes(_world, before, Pell([], [held with { Confidence = 0.5, Relevance = 0.2 }])).ShouldBe(
        [
            "p1 Pell on p2 Bram attacked p0 Mara at t0 The Drowned Lantern: confidence 0.40 → 0.50",
            "p1 Pell on p2 Bram attacked p0 Mara at t0 The Drowned Lantern: relevance 0.10 → 0.20",
        ]);
        TraceTables.Changes(_world, before, Pell([], [held with { Claim = byPell, Importance = 0.8 }])).ShouldBe(
        [
            "p1 Pell's version is now: p1 Pell attacked p0 Mara at t0 The Drowned Lantern, hearsay, hour -9 (Imp 0.80, Conf 0.40, Rel 0.10, Sev 0.50, Blame 0.00)",
            "p1 Pell on p1 Pell attacked p0 Mara at t0 The Drowned Lantern: importance 0.10 → 0.80",
        ]);
    }

    [Fact]
    public void Changes_NothingMoved_ListsNothing()
    {
        PersonSnapshot pell = Pell([Relation(_maraId, 0.2, 0.6, (TraitAxis.Kind, 0.1))], []);

        TraceTables.Changes(_world, pell, pell with { }).ShouldBeEmpty();
    }

    [Fact]
    public void BeliefRows_ListBeliefsNamingAWitnessOrATellerInHeardFromOrToldTo_ButNotAPlaceAlone()
    {
        // The Grey Heron is in the told story, but only as this attack's place.
        BeliefSnapshot aboard = Snapshot(new Claim(PersonOrThing.Unknown, Act.Attacked, Of(_ilseId), _heron.Id, hour: -3, severity: 0.5, blame: 0, witness: null));
        BeliefSnapshot witnessed = Snapshot(new Claim(Of(_ilseId), Act.Robbed, Of(_logbook.Id), _tavern.Id, hour: -4, severity: 0.2, blame: 0.3, _fenId));
        var seen = new Claim(Of(_ilseId), Act.WasSeenAt, Of(_tavern.Id), _tavern.Id, hour: -2, severity: 0, blame: 0, witness: null);
        BeliefSnapshot fromTeller = Snapshot(seen, heard: [new HeardEntry(_pellId, 30, seen)]);
        var helped = new Claim(Of(_ilseId), Act.Helped, Of(_ilseId), _tavern.Id, hour: -2, severity: 0.1, blame: 0, witness: null);
        BeliefSnapshot toTeller = Snapshot(helped) with { ToldTo = [new ToldEntry(_pellId, 31, helped, helped)] };
        PersonSnapshot pell = Pell([], [aboard, witnessed]);

        Rows(Mara([], [aboard, fromTeller, toTeller])).ShouldBe(
        [
            ["p1 Pell", "p4 Ilse", "stole", "t2 logbook"],
            ["p0 Mara", "p4 Ilse", "was seen at", "t0 The Drowned Lantern"],
            ["p0 Mara", "p4 Ilse", "helped", "p4 Ilse"],
        ]);
        Rows(Mara([], [aboard])).ShouldBe([["p1 Pell", "p4 Ilse", "stole", "t2 logbook"]]);

        string[][] Rows(PersonSnapshot mara) =>
            [.. TraceTables.BeliefRows(_world, pell, mara, _sank, minute: 72).Select(row => new[] { row.Holder, row.Actor, row.Act, row.Target })];
    }

    [Fact]
    public void BeliefRows_MarkTheToldStory_AndShowHowLongAgo_TheNumbers_AndEachTellingByIdAndTime()
    {
        Claim held = _sank.WithWitness(null);
        BeliefSnapshot belief = Snapshot(held, heard: [new HeardEntry(_fenId, 40, held), new HeardEntry(_ilseId, 55, held)]) with
        {
            ToldTo = [new ToldEntry(_maraId, 64, held, held)],
        };

        TraceTables.BeliefRows(_world, Pell([], [belief]), Mara([], []), _sank, minute: 72).ShouldBe(
        [
            new TraceTables.BeliefRow(
                IsTold: true, "p1 Pell", "p2 Bram", "sank", "t1 Grey Heron", "-9 (10 hours ago)", "hearsay",
                0.1, 0.4, 0.1, 0.6, 0.4, "p3 Fen 00:40\np4 Ilse 00:55", "p0 Mara 01:04"),
        ]);
    }

    [Fact]
    public void WhoAndWhat_ListsThePeople_ThenTheStory_ThenWhomTheirBeliefsName_OnceEach()
    {
        // The teller did it, the target is a thing, and Fen saw it.
        Claim told = new(Of(_pellId), Act.Sank, Of(_heron.Id), _tavern.Id, hour: -9, severity: 0.6, blame: 0.4, _fenId);
        BeliefSnapshot robbed = Snapshot(new Claim(Of(_ilseId), Act.Robbed, Of(_logbook.Id), _tavern.Id, hour: -4, severity: 0.2, blame: 0.3, _fenId));
        PersonSnapshot pell = Pell([], [robbed]) with { Traits = new TraitProfile(new Dictionary<TraitAxis, double> { [TraitAxis.Brave] = 0.9 }) };

        TraceTables.WhoAndWhat(_world, pell, Mara([], []), told).ShouldBe(
        [
            ("p1", "Pell", "person", "teller: brave; in the story: did it"),
            ("p0", "Mara", "person", "listener: no trait shows"),
            ("t1", "Grey Heron", "ship", "in the story: sunk"),
            ("t0", "The Drowned Lantern", "place", "in the story: where"),
            ("p3", "Fen", "person", "in the story: saw it"),
            ("p4", "Ilse", "person", "in their beliefs"),
            ("t2", "logbook", "item", "in their beliefs"),
        ]);
    }

    [Fact]
    public void WhoAndWhat_SeenAtAPlace_ListsThePlaceOnce()
    {
        Claim told = new(Of(_bramId), Act.WasSeenAt, Of(_tavern.Id), _tavern.Id, hour: -2, severity: 0, blame: 0, witness: null);

        TraceTables.WhoAndWhat(_world, Pell([], []), Mara([], []), told).ShouldBe(
        [
            ("p1", "Pell", "person", "teller: no trait shows"),
            ("p0", "Mara", "person", "listener: no trait shows"),
            ("p2", "Bram", "person", "in the story: seen"),
            ("t0", "The Drowned Lantern", "place", "in the story: where"),
        ]);
    }

    [Fact]
    public void WhoAndWhat_UnknownActor_IsNotListed()
    {
        Claim told = new(PersonOrThing.Unknown, Act.Sank, Of(_heron.Id), _tavern.Id, hour: -9, severity: 0.6, blame: 0, witness: null);

        TraceTables.WhoAndWhat(_world, Pell([], []), Mara([], []), told).ShouldBe(
        [
            ("p1", "Pell", "person", "teller: no trait shows"),
            ("p0", "Mara", "person", "listener: no trait shows"),
            ("t1", "Grey Heron", "ship", "in the story: sunk"),
            ("t0", "The Drowned Lantern", "place", "in the story: where"),
        ]);
    }

    [Fact]
    public void Feelings_FromEachTowardTheOther_AndEachPersonTheStoryNames_ButNotThemselves()
    {
        // Mara is the target, so she has no line toward herself; an impression of 0 says nothing.
        Claim told = new(Of(_bramId), Act.Attacked, Of(_maraId), _tavern.Id, hour: -9, severity: 0.5, blame: 0.4, _fenId);
        RelationSnapshot sister = new(
            _maraId, 0.2, 0.6, Ties.Sibling,
            new Dictionary<TraitAxis, double> { [TraitAxis.Kind] = -0.6, [TraitAxis.Brave] = 0.9, [TraitAxis.Honest] = 0.1, [TraitAxis.Patient] = 0.05 });
        PersonSnapshot pell = Pell([sister, Relation(_bramId, 0.1, 0.2, (TraitAxis.Kind, 0))], []);

        TraceTables.Feelings(_world, pell, Mara([Relation(_pellId, -0.3, 0.5)], []), told).ShouldBe(
        [
            ("p1 Pell → p0 Mara", "opinion 0.20, trust 0.60, sibling; sees them as brave (+0.90), unkind (-0.60), honest (+0.10)"),
            ("p1 Pell → p2 Bram", "opinion 0.10, trust 0.20"),
            ("p1 Pell → p3 Fen", "doesn't know them"),
            ("p0 Mara → p1 Pell", "opinion -0.30, trust 0.50"),
            ("p0 Mara → p2 Bram", "doesn't know them"),
            ("p0 Mara → p3 Fen", "doesn't know them"),
        ]);
    }

    [Theory]
    [InlineData(1, "this hour")]
    [InlineData(0, "1 hour ago")]
    [InlineData(-22, "23 hours ago")]
    [InlineData(-23, "1 day ago")]
    [InlineData(-207, "9 days ago")]
    public void Ago_InHoursUnderADay_ThenInRoundedDays(int hour, string ago) =>
        TraceTables.Ago(minute: 72, hour).ShouldBe(ago);

    [Fact]
    public void Id_IsPOrTAndTheNumber_OrAQuestionMarkWhenUnknown()
    {
        TraceTables.Id(Of(_bramId)).ShouldBe("p2");
        TraceTables.Id(Of(_logbook.Id)).ShouldBe("t2");
        TraceTables.Id(PersonOrThing.Unknown).ShouldBe("?");
    }

    private static PersonOrThing Of(PersonId person) => PersonOrThing.Of(person);

    private static PersonOrThing Of(ThingId thing) => PersonOrThing.Of(thing);

    private static Person Someone(PersonId id, string name) =>
        new(id, name, new TraitProfile(new Dictionary<TraitAxis, double>()), _tavern.Id);

    private static BeliefSnapshot Snapshot(Claim claim, HeardEntry[]? heard = null, double confidence = 0.4) =>
        new(claim, Importance: 0.1, confidence, Relevance: 0.1, heard ?? [], []);

    private static PersonSnapshot Pell(RelationSnapshot[] relations, BeliefSnapshot[] beliefs) => Snapshot(_pellId, "Pell", relations, beliefs);

    private static PersonSnapshot Mara(RelationSnapshot[] relations, BeliefSnapshot[] beliefs) => Snapshot(_maraId, "Mara", relations, beliefs);

    private static PersonSnapshot Snapshot(PersonId id, string name, RelationSnapshot[] relations, BeliefSnapshot[] beliefs) =>
        new(id, name, _tavern.Id, new TraitProfile(new Dictionary<TraitAxis, double>()), relations, beliefs);

    private static RelationSnapshot Relation(PersonId other, double opinion, double trust, params (TraitAxis Axis, double Value)[] impressions) =>
        new(other, opinion, trust, Ties.None, impressions.ToDictionary(impression => impression.Axis, impression => impression.Value));
}
