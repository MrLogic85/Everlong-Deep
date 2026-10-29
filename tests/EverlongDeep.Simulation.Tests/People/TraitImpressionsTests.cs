using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.People;

public class TraitImpressionsTests
{
    private const double _tolerance = 1e-9;

    private static readonly TraitAxis[] _visible = [TraitAxis.Kind, TraitAxis.Humorous, TraitAxis.Confident, TraitAxis.Talkative];

    private static Person Someone(int id, params (TraitAxis Axis, double Value)[] values) =>
        new(new PersonId(id), $"Person {id}", new TraitProfile(values.ToDictionary(value => value.Axis, value => value.Value)), new ThingId(0));

    private static Person Extreme(int id, double value) =>
        Someone(id, [.. Enum.GetValues<TraitAxis>().Select(axis => (axis, value))]);

    [Fact]
    public void FirstMeeting_FormsTheVisibleAxesWithinNoise_AndLeavesTheRestAbsent()
    {
        Person mara = Someone(0);
        Person pell = Someone(1, (TraitAxis.Kind, 0.9), (TraitAxis.Talkative, -0.6), (TraitAxis.Honest, -0.9));
        Relation maraToPell = mara.Meet(pell.Id, opinion: 0.2, trust: 0.4);

        TraitImpressions.FirstMeeting(maraToPell, pell, new Random(3));

        maraToPell.Impressions.Keys.ShouldBe(_visible, ignoreOrder: true);
        foreach (TraitAxis axis in _visible)
        {
            maraToPell.Impressions[axis].ShouldBe(pell.Traits[axis], TraitImpressions.FirstMeetingNoise);
        }

        maraToPell.Opinion.ShouldBe(0.2);
        maraToPell.Trust.ShouldBe(0.4);
        maraToPell.Ties.ShouldBe(Ties.None);
    }

    [Fact]
    public void GrewUpWith_FormsEveryAxisWithinKinNoise_AndKeepsImpressionsInRange()
    {
        Person mara = Someone(0);
        Person pell = Extreme(1, 1);
        Relation maraToPell = mara.Meet(pell.Id, opinion: 0.5, trust: 0.5);

        TraitImpressions.GrewUpWith(maraToPell, pell, new Random(3));

        maraToPell.Impressions.Count.ShouldBe(TraitProfile.AxisCount);
        maraToPell.Impressions.Values.ShouldAllBe(impression => impression >= 1 - TraitImpressions.KinNoise && impression <= 1);
    }

    [Fact]
    public void Compatibility_AHiddenRealValue_ChangesNothing()
    {
        Person mara = Someone(0, (TraitAxis.Honest, 1), (TraitAxis.Kind, 0.7));
        Person honestPell = Someone(1, (TraitAxis.Kind, 0.6), (TraitAxis.Honest, 1));
        Person lyingPell = Someone(2, (TraitAxis.Kind, 0.6), (TraitAxis.Honest, -1));
        Relation toHonest = mara.Meet(honestPell.Id, opinion: 0, trust: 0.5);
        Relation toLying = mara.Meet(lyingPell.Id, opinion: 0, trust: 0.5);

        TraitImpressions.FirstMeeting(toHonest, honestPell, new Random(5));
        TraitImpressions.FirstMeeting(toLying, lyingPell, new Random(5));

        TraitImpressions.Compatibility(mara, toLying).ShouldBe(TraitImpressions.Compatibility(mara, toHonest));
        TraitImpressions.Compatibility(mara, toLying).ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Compatibility_AtTheExtremes_StaysInRange()
    {
        foreach ((double holder, double seen) in new[] { (1.0, 1.0), (1.0, -1.0), (-1.0, 1.0), (-1.0, -1.0) })
        {
            Person mara = Extreme(0, holder);
            Relation relation = mara.Meet(new PersonId(1), opinion: 0, trust: 0.5);
            foreach (TraitAxis axis in Enum.GetValues<TraitAxis>())
            {
                relation.SetImpression(axis, seen);
            }

            TraitImpressions.Compatibility(mara, relation).ShouldBeInRange(-1, 1);
        }

        // No impressions, or only of an axis with no affinity: nothing to like or dislike.
        Person fen = Extreme(2, 1);
        Relation onlyConfident = fen.Meet(new PersonId(3), opinion: 0, trust: 0.5);
        TraitImpressions.Compatibility(fen, onlyConfident).ShouldBe(0);
        onlyConfident.SetImpression(TraitAxis.Confident, 1);
        TraitImpressions.Compatibility(fen, onlyConfident).ShouldBe(0);
    }
}
