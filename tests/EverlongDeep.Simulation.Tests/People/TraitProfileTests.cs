using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Tests.People;

public class TraitProfileTests
{
    [Fact]
    public void Shown_TheTraitsPastTheMark_InAxisOrder()
    {
        var traits = new TraitProfile(new Dictionary<TraitAxis, double>
        {
            [TraitAxis.Talkative] = -0.51,
            [TraitAxis.Honest] = 0.8,
            [TraitAxis.Kind] = TraitProfile.ShownMark,
            [TraitAxis.Open] = -0.7,
            [TraitAxis.Brave] = -0.2,
        });

        traits.Shown.ShouldBe([new TraitValue(TraitAxis.Honest, 0.8), new TraitValue(TraitAxis.Talkative, -0.51), new TraitValue(TraitAxis.Open, -0.7)]);
        new TraitProfile(new Dictionary<TraitAxis, double>()).Shown.ShouldBeEmpty();
    }

    [Fact]
    public void Indexer_AnAxisLeftOut_IsZero()
    {
        var traits = new TraitProfile(new Dictionary<TraitAxis, double> { [TraitAxis.Hardworking] = -0.6 });

        traits[TraitAxis.Honest].ShouldBe(0);
    }
}
