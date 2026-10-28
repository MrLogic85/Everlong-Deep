using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.People;

public class MeetingTests
{
    private const double _tolerance = 1e-9;

    private static Person Someone(int id, double kindness) =>
        new(new PersonId(id), $"Person {id}", new TraitProfile(new Dictionary<TraitAxis, double> { [TraitAxis.Kind] = kindness }), new ThingId(0));

    [Fact]
    public void Introduce_DrawsTheFirstSidesImpressionsFirst_AndSetsFeelingsFromCompatibilityBothWays()
    {
        Person fen = Someone(0, 1);
        Person ilse = Someone(1, 1);
        Person hollis = Someone(2, -1);

        // Visible axes form in axis order, Kind first: the first side's from the first sample, the second's from the fifth.
        Meeting.Introduce(fen, ilse, new ScriptedRandom(1, 0.5, 0.5, 0.5, 0, 0.5, 0.5, 0.5));
        Relation fenToIlse = fen.RelationTo(ilse.Id).ShouldNotBeNull();
        Relation ilseToFen = ilse.RelationTo(fen.Id).ShouldNotBeNull();
        fenToIlse.Impressions[TraitAxis.Kind].ShouldBe(1, _tolerance);
        ilseToFen.Impressions[TraitAxis.Kind].ShouldBe(1 - TraitImpressions.FirstMeetingNoise, _tolerance);

        // A kind pair warms to each other and a kind and an unkind one cool, by the same formula.
        Meeting.Introduce(fen, hollis, new FixedRandom(0.5));
        Relation fenToHollis = fen.RelationTo(hollis.Id).ShouldNotBeNull();
        foreach ((Person holder, Relation relation, bool warm) in new[] { (fen, fenToIlse, true), (ilse, ilseToFen, true), (fen, fenToHollis, false) })
        {
            double compatibility = TraitImpressions.Compatibility(holder, relation);
            (compatibility > 0).ShouldBe(warm);
            relation.Opinion.ShouldBe(Meeting.FirstOpinionScale * compatibility, _tolerance);
            relation.Trust.ShouldBe(Meeting.FirstTrustBase + (Meeting.FirstTrustScale * compatibility), _tolerance);
        }
    }

    [Fact]
    public void Introduce_KeepsARelationThatAlreadyExists()
    {
        Person fen = Someone(0, 1);
        Person ilse = Someone(1, 1);
        Relation ilseToFen = ilse.Meet(fen.Id, opinion: -0.7, trust: 0.9);

        Meeting.Introduce(fen, ilse, new FixedRandom(0.5));

        fen.RelationTo(ilse.Id).ShouldNotBeNull().Impressions.ShouldNotBeEmpty();
        ilseToFen.Opinion.ShouldBe(-0.7);
        ilseToFen.Trust.ShouldBe(0.9);
        ilseToFen.Impressions.ShouldBeEmpty();
    }
}
