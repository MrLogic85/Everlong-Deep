using System.Globalization;
using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// Lowers every belief's relevance each hour, slower the more it matters, until it is a memory
/// (<c>docs/08 Social Model.md</c> → "Fading"). The rates are tuned by listening.
/// </summary>
internal static class Fading
{
    /// <summary>The relevance a belief of no importance loses each hour.</summary>
    public const double RelevancePerHour = 0.05;

    /// <summary>How much of the fade the most important story is spared: at importance 1 it fades at half the rate.</summary>
    public const double ImportanceSlowdown = 0.5;

    /// <summary>The relevance <paramref name="belief"/> loses this hour, computed afresh each hour and never stored.</summary>
    public static double Rate(Belief belief) => RelevancePerHour * (1 - (ImportanceSlowdown * belief.Importance));

    /// <summary>Fades every belief in <paramref name="world"/> by one hour, and logs each that becomes a memory.</summary>
    public static void StepHour(World world, SimulationLog log)
    {
        foreach (Person person in world.People)
        {
            foreach (Belief belief in person.Beliefs.Where(belief => !belief.IsMemory))
            {
                belief.Relevance = Math.Max(0, belief.Relevance - Rate(belief));
                if (belief.IsMemory)
                {
                    log.Write(string.Create(
                        CultureInfo.InvariantCulture,
                        $"{world.Clock} {person.Name} no longer talks of it, a memory: {ClaimText.Describe(world, belief.Claim)} " +
                        $"(importance {belief.Importance:0.00}, confidence {belief.Confidence:0.00})"));
                }
            }
        }
    }
}
