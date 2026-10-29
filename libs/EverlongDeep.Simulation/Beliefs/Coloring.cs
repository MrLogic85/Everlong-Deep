namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// How a teller colors a claim's dials (<c>docs/08 Social Model.md</c> → "Telling" step 4): the reach, the opinions it
/// multiplies and whose they are, whether blame was colored harsher, and whether a dial stopped at the end of its range.
/// An opinion is 0 unless its source is <see cref="OpinionSource.Known"/>.
/// </summary>
public sealed record Coloring(
    double Reach, OpinionSource TargetSource, double TargetOpinion, OpinionSource ActorSource, double ActorOpinion, bool Harsher)
{
    /// <summary>The reach × the teller's opinion of the target.</summary>
    public double SeverityPart => Reach * TargetOpinion;

    /// <summary>The reach × −the teller's opinion of the actor.</summary>
    public double BlamePart => -Reach * ActorOpinion;

    /// <summary>A further reach toward blame when "Color toward harsher blame" said yes.</summary>
    public double HarsherPart => Harsher ? Reach : 0;

    /// <summary>Whether severity stopped at 0 or 1 short of its parts.</summary>
    public bool SeverityCapped { get; init; }

    /// <summary>Whether blame stopped at an end of <see cref="Claim.HarmfulBlame"/> short of its parts.</summary>
    public bool BlameCapped { get; init; }
}
