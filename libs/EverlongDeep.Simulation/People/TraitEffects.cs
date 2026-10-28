using System.Diagnostics;

namespace EverlongDeep.Simulation.People;

/// <summary>How a person's traits pull on a choice, as <c>docs/08 Social Model.md</c> → "Traits" defines.</summary>
internal static class TraitEffects
{
    /// <summary>The sum of <paramref name="choice"/>'s weights times <paramref name="traits"/>' values.</summary>
    public static double Pull(TraitProfile traits, Choice choice) =>
        TraitTables.Effects(choice).Sum(effect => effect.Weight * traits[effect.Axis]);

    /// <summary>The axes that pull most on <paramref name="choice"/>, either way, largest first, for log lines.</summary>
    public static IReadOnlyList<(TraitAxis Axis, double Contribution)> TopPulls(TraitProfile traits, Choice choice, int count) =>
        [.. TraitTables.Effects(choice)
            .Select(effect => (effect.Axis, Contribution: effect.Weight * traits[effect.Axis]))
            .Where(pull => pull.Contribution != 0)
            .OrderByDescending(pull => Math.Abs(pull.Contribution))
            .Take(count)];

    /// <summary>The chance of each option from its score; each is above 0 and they sum to 1.</summary>
    public static double[] Chances(IReadOnlyList<double> scores)
    {
        Debug.Assert(scores.Count > 0, "A choice needs at least one option");
        if (scores.Count == 0)
        {
            return [];
        }

        // Subtracting the largest keeps Exp from overflowing without changing the ratios.
        double max = scores.Max();
        double[] weights = [.. scores.Select(score => Math.Exp(score - max))];
        double total = weights.Sum();
        return [.. weights.Select(weight => weight / total)];
    }

    /// <summary>The chance of a yes-or-no choice, scored against not doing it at 0.</summary>
    public static double Chance(double score) => Chances([score, 0])[0];
}
