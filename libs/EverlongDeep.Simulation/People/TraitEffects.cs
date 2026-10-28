using System.Diagnostics;

namespace EverlongDeep.Simulation.People;

/// <summary>How a person's traits pull on a choice, as <c>docs/08 Social Model.md</c> → "Traits" defines.</summary>
internal static class TraitEffects
{
    /// <summary>The sum of <paramref name="choice"/>'s weights times <paramref name="traits"/>' values.</summary>
    public static double Pull(TraitProfile traits, Choice choice) =>
        TraitTables.Effects(choice).Sum(effect => effect.Weight * traits[effect.Axis]);

    /// <summary>Adds each axis's pull on <paramref name="choice"/> to <paramref name="pulls"/>, summed over several choices.</summary>
    public static void AddPulls(TraitProfile traits, Choice choice, Dictionary<TraitAxis, double> pulls)
    {
        foreach ((TraitAxis axis, double weight) in TraitTables.Effects(choice))
        {
            pulls[axis] = pulls.GetValueOrDefault(axis) + (weight * traits[axis]);
        }
    }

    /// <summary>The axes that pulled most in <paramref name="pulls"/>, either way, largest first, for log lines.</summary>
    public static IReadOnlyList<TraitAxis> TopPulls(IReadOnlyDictionary<TraitAxis, double> pulls, int count) =>
        [.. pulls
            .Where(pull => pull.Value != 0)
            .OrderByDescending(pull => Math.Abs(pull.Value))
            .ThenBy(pull => pull.Key)
            .Take(count)
            .Select(pull => pull.Key)];

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

    /// <summary>Draws one option by its chance from <paramref name="scores"/>, with one draw.</summary>
    public static Pick Draw(IReadOnlyList<double> scores, Random random)
    {
        double[] chances = Chances(scores);
        double roll = random.NextDouble();
        double left = roll;
        for (int i = 0; i < chances.Length - 1; i++)
        {
            left -= chances[i];
            if (left < 0)
            {
                return new Pick(i, roll, chances);
            }
        }

        return new Pick(chances.Length - 1, roll, chances);
    }

    /// <summary>Draws a yes-or-no choice by its chance from <paramref name="score"/>, with one draw.</summary>
    public static Roll Decide(double score, Random random) => new(score, Chance(score), random.NextDouble());
}
