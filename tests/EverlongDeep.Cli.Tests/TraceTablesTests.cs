using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Cli.Tests;

public class TraceTablesTests
{
    private static readonly Thing _tavern = new(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);
    private static readonly PersonId _maraId = new(0);
    private static readonly PersonId _pellId = new(1);
    private static readonly PersonId _bramId = new(2);

    private readonly World _world = new(
        seed: 0,
        new Random(0),
        [_tavern],
        [Someone(_maraId, "Mara"), Someone(_pellId, "Pell"), Someone(_bramId, "Bram")]);

    [Fact]
    public void Changes_ListEachNewBelief_NewEntry_MovedFeeling_ChangedImpression_AndNewRelation()
    {
        var seen = new Claim(PersonOrThing.Of(_bramId), Act.WasSeenAt, PersonOrThing.Of(_tavern.Id), _tavern.Id, hour: -5, severity: 0, blame: 0, _bramId);
        var helped = new Claim(PersonOrThing.Of(_maraId), Act.Helped, PersonOrThing.Of(_bramId), _tavern.Id, hour: -9, severity: 0.1, blame: 0, witness: null);
        BeliefSnapshot held = Snapshot(helped, heard: [new HeardEntry(_maraId, 10, helped)], told: []);
        PersonSnapshot before = Pell([Relation(_maraId, 0.2, 0.6, (TraitAxis.Kind, 0.1))], [held]);
        PersonSnapshot after = Pell(
            [Relation(_maraId, 0.3, 0.5, (TraitAxis.Kind, 0.2)), Relation(_bramId, 0, 0.3)],
            [
                held with { ToldTo = [new ToldEntry(_bramId, 64, helped, helped)] },
                Snapshot(seen, heard: [new HeardEntry(_bramId, 70, seen)], told: [], confidence: 0.54),
            ]);

        TraceTables.Changes(_world, before, after).ShouldBe(
        [
            "Pell now believes: Bram was seen at The Drowned Lantern, Bram saw it, hour -5 (confidence 0.54, relevance 0.10, severity 0.00, blame 0.00)",
            "Pell remembers p1/Helped/p2: to p2 Bram 01:04",
            "Pell remembers p1/WasSeenAt/t0: from p2 Bram 01:10",
            "Pell's opinion of Mara: 0.20 → 0.30",
            "Pell's trust in Mara: 0.60 → 0.50",
            "Pell's impressions of Mara: Kind +0.10 → Kind +0.20",
            "Pell met Bram: opinion 0.00, trust 0.30; impressions none",
        ]);
    }

    [Fact]
    public void Entries_HeardFromThenToldTo_EachWithIdNameAndTime()
    {
        var helped = new Claim(PersonOrThing.Of(_maraId), Act.Helped, PersonOrThing.Of(_bramId), _tavern.Id, hour: -9, severity: 0.1, blame: 0, witness: null);
        BeliefSnapshot belief = Snapshot(
            helped,
            heard: [new HeardEntry(_maraId, 40, helped)],
            told: [new ToldEntry(_bramId, 72, helped, helped), new ToldEntry(_maraId, 95, helped, helped)]);

        TraceTables.Entries(_world, belief).ShouldBe(["from p0 Mara 00:40", "to p2 Bram 01:12", "to p0 Mara 01:35"]);
    }

    [Fact]
    public void Changes_NothingMoved_ListsNothing()
    {
        PersonSnapshot pell = Pell([Relation(_maraId, 0.2, 0.6, (TraitAxis.Kind, 0.1))], []);

        TraceTables.Changes(_world, pell, pell with { }).ShouldBeEmpty();
    }

    [Fact]
    public void BeliefId_IsHolderActAndTarget_WhateverTheActorWitnessOrDials()
    {
        var told = new Claim(PersonOrThing.Of(_bramId), Act.Robbed, PersonOrThing.Of(_tavern.Id), _tavern.Id, hour: -5, severity: 0.4, blame: 0.5, _bramId);
        var colored = new Claim(PersonOrThing.Unknown, Act.Robbed, PersonOrThing.Of(_tavern.Id), _tavern.Id, hour: -9, severity: 0.7, blame: 0, witness: null);

        TraceTables.BeliefId(_pellId, told).ShouldBe("p1/Robbed/t0");
        TraceTables.BeliefId(_pellId, colored).ShouldBe("p1/Robbed/t0");
    }

    [Fact]
    public void IdRows_ListEachNamedOnce_PeopleThenThingsById_WithoutUnknowns()
    {
        PersonOrThing[] named =
        [
            PersonOrThing.Of(_tavern.Id), PersonOrThing.Of(_bramId), PersonOrThing.Unknown,
            PersonOrThing.Of(_maraId), PersonOrThing.Of(_bramId), PersonOrThing.Of(_tavern.Id),
        ];

        TraceTables.IdRows(_world, named).ShouldBe(
        [
            ("p0", "Mara", "person"),
            ("p2", "Bram", "person"),
            ("t0", "The Drowned Lantern", "place"),
        ]);
        TraceTables.Id(PersonOrThing.Unknown).ShouldBe("?");
    }

    private static Person Someone(PersonId id, string name) =>
        new(id, name, new TraitProfile(new Dictionary<TraitAxis, double>()), _tavern.Id);

    private static BeliefSnapshot Snapshot(Claim claim, HeardEntry[] heard, ToldEntry[] told, double confidence = 0.4) =>
        new(claim, confidence, Relevance: 0.1, heard, told);

    private static PersonSnapshot Pell(RelationSnapshot[] relations, BeliefSnapshot[] beliefs) =>
        new(_pellId, "Pell", _tavern.Id, new TraitProfile(new Dictionary<TraitAxis, double>()), relations, beliefs);

    private static RelationSnapshot Relation(PersonId other, double opinion, double trust, params (TraitAxis Axis, double Value)[] impressions) =>
        new(other, opinion, trust, Ties.None, impressions.ToDictionary(impression => impression.Axis, impression => impression.Value));
}
