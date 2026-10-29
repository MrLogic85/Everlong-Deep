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
    public void TopPulls_NameTheLargestContributors_EitherWay_LargestFirst()
    {
        var pulls = new Dictionary<TraitAxis, double> { [TraitAxis.Confident] = 0.1, [TraitAxis.Curious] = -1.2, [TraitAxis.Open] = 0.5, [TraitAxis.Kind] = 0 };

        TraitEffects.TopPulls(pulls, count: 2).ShouldBe([TraitAxis.Curious, TraitAxis.Open]);
        TraitEffects.TopPulls(pulls, count: 5).ShouldBe([TraitAxis.Curious, TraitAxis.Open, TraitAxis.Confident]);
    }

    [Theory]
    [InlineData(0.0, 0)]
    [InlineData(0.5, 1)]
    [InlineData(0.99, 2)]
    public void Draw_PicksTheOptionWhoseShareTheRollFallsIn(double roll, int expected)
    {
        TraitEffects.Draw([0, 0, 0], new ScriptedRandom(roll)).Index.ShouldBe(expected);
    }

    [Theory]
    [InlineData(0.49, true)]
    [InlineData(0.5, false)]
    public void Decide_SaysYesBelowTheChance(double roll, bool expected)
    {
        // A profile of 0 scores the context alone.
        TraitEffects.Decide(Profile(), Choice.Lie, context: 0, new ScriptedRandom(roll)).Yes.ShouldBe(expected);
    }

    [Fact]
    public void Decide_RecordsTheContextAndEachTraitsPull_WhichSumToTheScore()
    {
        TraitProfile traits = Profile((TraitAxis.Honest, -0.5), (TraitAxis.Confident, 0.25), (TraitAxis.Kind, 1));

        Roll roll = TraitEffects.Decide(traits, Choice.ClaimWitnessed, context: -2, new ScriptedRandom(0));

        roll.Context.ShouldBe(-2);
        roll.Pulls.ShouldNotBeEmpty();
        roll.Score.ShouldBe(-2 + roll.Pulls.Sum(pull => pull.Value), _tolerance);
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
