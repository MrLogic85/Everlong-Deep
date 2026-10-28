using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Cli.Tests;

public class TraceTextTests
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
    public void Changes_ListEachNewBelief_MovedFeeling_ChangedImpression_AndNewRelation()
    {
        var claim = new Claim(PersonOrThing.Of(_bramId), Act.WasSeenAt, PersonOrThing.Of(_tavern.Id), _tavern.Id, hour: -5, severity: 0, blame: 0, _bramId);
        PersonSnapshot before = Pell([Relation(_maraId, 0.2, 0.6, (TraitAxis.Kind, 0.1))], []);
        PersonSnapshot after = Pell(
            [Relation(_maraId, 0.3, 0.5, (TraitAxis.Kind, 0.2)), Relation(_bramId, 0, 0.3)],
            [new BeliefSnapshot(claim, Confidence: 0.54, Relevance: 0.1)]);

        TraceText.Changes(_world, before, after).ShouldBe(
        [
            "Pell now believes: Bram was seen at The Drowned Lantern, Bram saw it, hour -5 (confidence 0.54, relevance 0.10, severity 0.00, blame 0.00)",
            "Pell's opinion of Mara: 0.20 → 0.30",
            "Pell's trust in Mara: 0.60 → 0.50",
            "Pell's impressions of Mara: Kind +0.10 → Kind +0.20",
            "Pell met Bram: opinion 0.00, trust 0.30; impressions none",
        ]);
    }

    [Fact]
    public void Changes_NothingMoved_ListsNothing()
    {
        PersonSnapshot pell = Pell([Relation(_maraId, 0.2, 0.6, (TraitAxis.Kind, 0.1))], []);

        TraceText.Changes(_world, pell, pell with { }).ShouldBeEmpty();
    }

    private static Person Someone(PersonId id, string name) =>
        new(id, name, new TraitProfile(new Dictionary<TraitAxis, double>()), _tavern.Id);

    private static PersonSnapshot Pell(RelationSnapshot[] relations, BeliefSnapshot[] beliefs) =>
        new(_pellId, "Pell", _tavern.Id, new TraitProfile(new Dictionary<TraitAxis, double>()), relations, beliefs);

    private static RelationSnapshot Relation(PersonId other, double opinion, double trust, params (TraitAxis Axis, double Value)[] impressions) =>
        new(other, opinion, trust, Ties.None, impressions.ToDictionary(impression => impression.Axis, impression => impression.Value));
}
