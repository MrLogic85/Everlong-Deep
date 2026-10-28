using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>A belief a teller could have told, the trait pulls on it, its score and chance.</summary>
public sealed record TopicOption(BeliefSnapshot Belief, IReadOnlyList<(Choice Choice, double Pull)> Pulls, double Score, double Chance);
