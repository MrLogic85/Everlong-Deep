namespace EverlongDeep.Simulation.People;

/// <summary>
/// The trait data every decision reads: each axis's − pole, visibility and affinity, and the effect table.
/// <c>docs/08 Social Model.md</c> → "Traits" holds the same starting values; listening tunes both together.
/// </summary>
internal static class TraitTables
{
    private sealed record Axis(string Negative, bool Visible, double Affinity);

    // In TraitAxis order. Positive affinity: people like others alike on it; negative: opposites attract.
    private static readonly Axis[] _axes =
    [
        new("dishonest", Visible: false, Affinity: 0.6),
        new("unkind", Visible: true, Affinity: 0.5),
        new("cowardly", Visible: false, Affinity: 0.2),
        new("disloyal", Visible: false, Affinity: 0.6),
        new("impatient", Visible: false, Affinity: 0.2),
        new("unambitious", Visible: false, Affinity: 0.1),
        new("apathetic", Visible: false, Affinity: 0.4),
        new("unreliable", Visible: false, Affinity: 0.4),
        new("indifferent", Visible: false, Affinity: 0.2),
        new("pessimistic", Visible: false, Affinity: 0.2),
        new("irresponsible", Visible: false, Affinity: 0.3),
        new("stingy", Visible: false, Affinity: 0.3),
        new("unimaginative", Visible: false, Affinity: 0.1),
        new("dependent", Visible: false, Affinity: 0),
        new("rigid", Visible: false, Affinity: 0.1),
        new("lazy", Visible: false, Affinity: 0.4),
        new("humorless", Visible: true, Affinity: 0.4),
        new("insecure", Visible: true, Affinity: 0),
        new("disorganized", Visible: false, Affinity: 0.1),
        new("undisciplined", Visible: false, Affinity: 0.1),
        new("quiet", Visible: true, Affinity: -0.3),
        new("secretive", Visible: false, Affinity: 0.2),
    ];

    // A weight per (choice, axis); an axis left out weighs 0.
    private static readonly Dictionary<Choice, (TraitAxis Axis, double Weight)[]> _effects = new()
    {
        [Choice.Talk] = [(TraitAxis.Talkative, 1.0), (TraitAxis.Confident, 0.5)],
        [Choice.ApproachStranger] =
            [(TraitAxis.Confident, 0.6), (TraitAxis.Curious, 0.5), (TraitAxis.Talkative, 0.5), (TraitAxis.Open, 0.6)],
        [Choice.TellOthersBusiness] = [(TraitAxis.Curious, 0.8)],
        [Choice.TellScandal] = [(TraitAxis.Kind, -0.6), (TraitAxis.Optimistic, -0.6)],
        [Choice.ColorDials] = [(TraitAxis.Creative, 0.6), (TraitAxis.Honest, -0.6)],
        [Choice.ColorBlameHarsher] = [(TraitAxis.Kind, -0.6), (TraitAxis.Empathetic, -0.4)],
        [Choice.ClaimWitnessed] = [(TraitAxis.Honest, -0.8), (TraitAxis.Confident, 0.4)],
        [Choice.KeepWitness] = [(TraitAxis.Honest, 0.6), (TraitAxis.Reliable, 0.4)],
        [Choice.TellTruth] = [(TraitAxis.Honest, 0.6), (TraitAxis.Open, 0.3)],
        [Choice.TellColored] = [(TraitAxis.Honest, -0.4), (TraitAxis.Creative, 0.4)],
        [Choice.HoldBack] = [(TraitAxis.Open, -0.8)],
        [Choice.Lie] = [(TraitAxis.Honest, -1.0)],
        [Choice.Believe] = [(TraitAxis.Adaptable, 0.5), (TraitAxis.Curious, 0.3), (TraitAxis.Independent, -0.5)],
    };

    /// <summary>The label of <paramref name="axis"/>'s pole that <paramref name="value"/> leans to, such as "honest".</summary>
    public static string Label(TraitAxis axis, double value) =>
        value >= 0 ? axis.ToString().ToLowerInvariant() : _axes[(int)axis].Negative;

    /// <summary>Whether others see this axis at a table, rather than learn it from what the person does.</summary>
    public static bool IsVisible(TraitAxis axis) => _axes[(int)axis].Visible;

    public static double Affinity(TraitAxis axis) => _axes[(int)axis].Affinity;

    public static IReadOnlyList<(TraitAxis Axis, double Weight)> Effects(Choice choice) => _effects[choice];
}
