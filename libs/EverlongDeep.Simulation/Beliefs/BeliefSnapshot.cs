namespace EverlongDeep.Simulation.Beliefs;

/// <summary>A belief as it stood at one moment.</summary>
public sealed record BeliefSnapshot(
    Claim Claim, double Importance, double Confidence, double Relevance, IReadOnlyList<HeardEntry> HeardFrom, IReadOnlyList<ToldEntry> ToldTo)
{
    internal static BeliefSnapshot Of(Belief belief) =>
        new(belief.Claim, belief.Importance, belief.Confidence, belief.Relevance, [.. belief.HeardFrom], [.. belief.ToldTo]);
}
