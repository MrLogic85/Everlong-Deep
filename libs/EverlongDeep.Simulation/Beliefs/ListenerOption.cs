using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>A known person a teller could have told, scored by the teller's opinion + trust + familiarity of them.</summary>
public sealed record ListenerOption(PersonId Person, double Opinion, double Trust, double Familiarity, double Chance)
{
    public double Score => ScoreOf(Opinion, Trust, Familiarity);

    internal static double ScoreOf(Relation relation) => ScoreOf(relation.Opinion, relation.Trust, relation.Familiarity);

    private static double ScoreOf(double opinion, double trust, double familiarity) => opinion + trust + familiarity;
}
