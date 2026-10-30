using EverlongDeep.Simulation.People;

namespace EverlongDeep.Cli;

/// <summary>A trait as a word: its axis names the + pole, and this table the − pole (<c>docs/08 Social Model.md</c> → "Traits").</summary>
internal static class TraitWords
{
    private static readonly Dictionary<TraitAxis, string> _negative = new()
    {
        [TraitAxis.Honest] = "dishonest",
        [TraitAxis.Kind] = "unkind",
        [TraitAxis.Brave] = "cowardly",
        [TraitAxis.Loyal] = "disloyal",
        [TraitAxis.Patient] = "impatient",
        [TraitAxis.Ambitious] = "unambitious",
        [TraitAxis.Empathetic] = "apathetic",
        [TraitAxis.Reliable] = "unreliable",
        [TraitAxis.Curious] = "indifferent",
        [TraitAxis.Optimistic] = "pessimistic",
        [TraitAxis.Responsible] = "irresponsible",
        [TraitAxis.Generous] = "stingy",
        [TraitAxis.Creative] = "unimaginative",
        [TraitAxis.Independent] = "dependent",
        [TraitAxis.Adaptable] = "rigid",
        [TraitAxis.Hardworking] = "lazy",
        [TraitAxis.Humorous] = "humorless",
        [TraitAxis.Confident] = "insecure",
        [TraitAxis.Organized] = "disorganized",
        [TraitAxis.Disciplined] = "undisciplined",
        [TraitAxis.Talkative] = "quiet",
        [TraitAxis.Open] = "secretive",
    };

    /// <summary>The pole of <paramref name="axis"/> that <paramref name="value"/> leans to: "kind" or "unkind".</summary>
    public static string Label(TraitAxis axis, double value) =>
        value >= 0 ? axis.ToString().ToLowerInvariant() : _negative[axis];

    public static string Label(TraitValue trait) => Label(trait.Axis, trait.Value);

    /// <summary>The traits that show, in axis order, such as "honest, secretive".</summary>
    public static IEnumerable<string> Shown(TraitProfile traits) => traits.Shown.Select(Label);
}
