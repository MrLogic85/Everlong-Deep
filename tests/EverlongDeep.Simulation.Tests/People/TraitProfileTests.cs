using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Tests.People;

public class TraitProfileTests
{
    [Fact]
    public void Labels_NameThePoleOfEachTraitPastTheMark_InAxisOrder()
    {
        var traits = new TraitProfile(new Dictionary<TraitAxis, double>
        {
            [TraitAxis.Talkative] = -0.51,
            [TraitAxis.Honest] = 0.8,
            [TraitAxis.Kind] = TraitProfile.LabelMark,
            [TraitAxis.Open] = -0.7,
            [TraitAxis.Brave] = -0.2,
        });

        traits.Labels.ShouldBe([TraitTables.Label(TraitAxis.Honest, 1), TraitTables.Label(TraitAxis.Talkative, -1), TraitTables.Label(TraitAxis.Open, -1)]);
        new TraitProfile(new Dictionary<TraitAxis, double>()).Labels.ShouldBeEmpty();
    }

    [Fact]
    public void Indexer_AnAxisLeftOut_IsZero()
    {
        var traits = new TraitProfile(new Dictionary<TraitAxis, double> { [TraitAxis.Hardworking] = -0.6 });

        traits[TraitAxis.Honest].ShouldBe(0);
    }
}
