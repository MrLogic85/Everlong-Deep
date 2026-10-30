using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// A belief a teller could have told and its chance; its score is the belief's relevance times
/// <see cref="RelevanceWeight"/>, plus a retelling's <see cref="Context"/>, plus each trait's pull on each choice that
/// weighs on telling it.
/// </summary>
public sealed record TopicOption(
    BeliefSnapshot Belief, double RelevanceWeight, bool IsRetelling, IReadOnlyList<TraitPull> Pulls, double Chance)
{
    public double RelevanceTerm => RelevanceWeight * Belief.Relevance;

    /// <summary>"Tell a story again"'s context for a retelling, 0 otherwise.</summary>
    public double Context => ContextOf(IsRetelling);

    public double Score => ScoreOf(RelevanceWeight, Belief.Relevance, IsRetelling, Pulls);

    internal static double ScoreOf(double relevanceWeight, double relevance, bool isRetelling, IEnumerable<TraitPull> pulls) =>
        (relevanceWeight * relevance) + ContextOf(isRetelling) + pulls.Sum(pull => pull.Value);

    private static double ContextOf(bool isRetelling) => isRetelling ? Telling.TellAgainContext : 0;
}
