using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// A belief a teller could have told and its chance; its score is the belief's relevance times
/// <see cref="RelevanceWeight"/>, plus each trait's pull on each choice that weighs on telling it.
/// </summary>
public sealed record TopicOption(BeliefSnapshot Belief, double RelevanceWeight, IReadOnlyList<TraitPull> Pulls, double Chance)
{
    public double RelevanceTerm => RelevanceWeight * Belief.Relevance;

    public double Score => ScoreOf(RelevanceWeight, Belief.Relevance, Pulls);

    internal static double ScoreOf(double relevanceWeight, double relevance, IEnumerable<TraitPull> pulls) =>
        (relevanceWeight * relevance) + pulls.Sum(pull => pull.Value);
}
