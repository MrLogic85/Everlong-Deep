namespace EverlongDeep.Simulation.People;

/// <summary>
/// The trait data every decision reads: each axis's visibility and affinity, and the effect table.
/// <c>docs/08 Social Model.md</c> → "Traits" holds the same starting values; listening tunes both together.
/// </summary>
internal static class TraitTables
{
    private sealed record Axis(bool Visible, double Affinity);

    // In TraitAxis order. Positive affinity: people like others alike on it; negative: opposites attract.
    private static readonly Axis[] _axes =
    [
        new(Visible: false, Affinity: 0.6), // Honest
        new(Visible: true, Affinity: 0.5), // Kind
        new(Visible: false, Affinity: 0.2), // Brave
        new(Visible: false, Affinity: 0.6), // Loyal
        new(Visible: false, Affinity: 0.2), // Patient
        new(Visible: false, Affinity: 0.1), // Ambitious
        new(Visible: false, Affinity: 0.4), // Empathetic
        new(Visible: false, Affinity: 0.4), // Reliable
        new(Visible: false, Affinity: 0.2), // Curious
        new(Visible: false, Affinity: 0.2), // Optimistic
        new(Visible: false, Affinity: 0.3), // Responsible
        new(Visible: false, Affinity: 0.3), // Generous
        new(Visible: false, Affinity: 0.1), // Creative
        new(Visible: false, Affinity: 0), // Independent
        new(Visible: false, Affinity: 0.1), // Adaptable
        new(Visible: false, Affinity: 0.4), // Hardworking
        new(Visible: true, Affinity: 0.4), // Humorous
        new(Visible: true, Affinity: 0), // Confident
        new(Visible: false, Affinity: 0.1), // Organized
        new(Visible: false, Affinity: 0.1), // Disciplined
        new(Visible: true, Affinity: -0.3), // Talkative
        new(Visible: false, Affinity: 0.2), // Open
    ];

    // A weight per (choice, axis); an axis left out weighs 0.
    private static readonly Dictionary<Choice, (TraitAxis Axis, double Weight)[]> _effects = new()
    {
        [Choice.Talk] = [(TraitAxis.Talkative, 1.0), (TraitAxis.Confident, 0.5)],
        [Choice.ApproachStranger] =
            [(TraitAxis.Confident, 0.6), (TraitAxis.Curious, 0.5), (TraitAxis.Talkative, 0.5), (TraitAxis.Open, 0.6)],
        [Choice.TellOthersBusiness] = [(TraitAxis.Curious, 0.8)],
        [Choice.TellScandal] = [(TraitAxis.Kind, -0.6), (TraitAxis.Optimistic, -0.6)],
        [Choice.TellAgain] = [(TraitAxis.Talkative, 0.5), (TraitAxis.Creative, -0.4)],
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

    /// <summary>Whether others see this axis at a table, rather than learn it from what the person does.</summary>
    public static bool IsVisible(TraitAxis axis) => _axes[(int)axis].Visible;

    public static double Affinity(TraitAxis axis) => _axes[(int)axis].Affinity;

    public static IReadOnlyList<(TraitAxis Axis, double Weight)> Effects(Choice choice) => _effects[choice];
}
