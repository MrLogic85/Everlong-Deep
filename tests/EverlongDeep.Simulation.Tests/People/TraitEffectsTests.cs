using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Tests.People;

public class TraitEffectsTests
{
    private const double _tolerance = 1e-9;

    private static TraitProfile Profile(params (TraitAxis Axis, double Value)[] values) =>
        new(values.ToDictionary(value => value.Axis, value => value.Value));

    [Fact]
    public void Tables_HaveARowForEveryChoice_AndBothPolesForEveryAxis()
    {
        foreach (Choice choice in Enum.GetValues<Choice>())
        {
            TraitTables.Effects(choice).ShouldNotBeEmpty();
        }

        string[] labels =
            [.. Enum.GetValues<TraitAxis>().SelectMany(axis => new[] { TraitTables.Label(axis, 1), TraitTables.Label(axis, -1) })];
        labels.ShouldBeUnique();
        labels.Length.ShouldBe(2 * TraitProfile.AxisCount);
    }

    [Fact]
    public void Pull_SumsEachWeightTimesTheTrait()
    {
        TraitProfile traits = Profile((TraitAxis.Creative, 0.5), (TraitAxis.Honest, 0.8), (TraitAxis.Kind, 1));

        // Creative 0.6 × 0.5, honest −0.6 × 0.8; kindness doesn't weigh on coloring.
        TraitEffects.Pull(traits, Choice.ColorDials).ShouldBe(0.3 - 0.48, _tolerance);
    }

    [Fact]
    public void Chances_HonestAndSecretive_HoldsBackRatherThanLies()
    {
        TraitProfile fen = Profile((TraitAxis.Honest, 0.8), (TraitAxis.Open, -0.7));
        Choice[] options = [Choice.TellTruth, Choice.TellColored, Choice.HoldBack, Choice.Lie];

        double[] chances = TraitEffects.Chances([.. options.Select(option => TraitEffects.Pull(fen, option))]);

        // Hold back 0.56, truth 0.27, colored −0.32, lie −0.8.
        options[Array.IndexOf(chances, chances.Max())].ShouldBe(Choice.HoldBack);
        options[Array.IndexOf(chances, chances.Min())].ShouldBe(Choice.Lie);
    }

    [Fact]
    public void TopPulls_NameTheLargestContributors_EitherWay_LargestFirst()
    {
        TraitProfile traits = Profile((TraitAxis.Confident, 0.2), (TraitAxis.Curious, -0.9), (TraitAxis.Open, 0.5));

        var pulls = new Dictionary<TraitAxis, double>();
        TraitEffects.AddPulls(traits, Choice.ApproachStranger, pulls);
        TraitEffects.AddPulls(traits, Choice.TellOthersBusiness, pulls);

        // Curious −0.45 − 0.72 sums across both choices.
        pulls[TraitAxis.Curious].ShouldBe(-1.17, _tolerance);
        TraitEffects.TopPulls(pulls, count: 2).ShouldBe([TraitAxis.Curious, TraitAxis.Open]);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.5, 1)]
    [InlineData(0.99, 2)]
    public void Draw_PicksTheOptionWhoseShareTheRollFallsIn(double roll, int expected)
    {
        TraitEffects.Draw([0, 0, 0], new ScriptedRandom(roll)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0.49, true)]
    [InlineData(0.5, false)]
    public void Decide_SaysYesBelowTheChance(double roll, bool expected)
    {
        TraitEffects.Decide(0, new ScriptedRandom(roll)).ShouldBe(expected);
    }

    [Fact]
    public void Chances_ForEveryChoice_AtTheExtremes_StayPositive_AndSumToOne()
    {
        TraitProfile[] extremes =
        [
            Profile([.. Enum.GetValues<TraitAxis>().Select(axis => (axis, 1.0))]),
            Profile([.. Enum.GetValues<TraitAxis>().Select(axis => (axis, -1.0))]),
        ];

        foreach (TraitProfile traits in extremes)
        {
            double[] chances = TraitEffects.Chances([.. Enum.GetValues<Choice>().Select(choice => TraitEffects.Pull(traits, choice))]);

            chances.ShouldAllBe(chance => chance > 0 && chance < 1);
            chances.Sum().ShouldBe(1, _tolerance);
        }

        TraitEffects.Chances([1000, -1000]).ShouldAllBe(chance => !double.IsNaN(chance));
        TraitEffects.Chance(0).ShouldBe(0.5, _tolerance);
    }
}
