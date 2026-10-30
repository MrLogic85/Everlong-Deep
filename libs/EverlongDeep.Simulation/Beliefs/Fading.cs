using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// Lowers every belief's relevance as time passes, slower the more it matters, until it is a memory
/// (<c>docs/08 Social Model.md</c> → "Fading"). The rates are tuned by listening.
/// </summary>
internal static class Fading
{
    /// <summary>The relevance a belief of no importance loses each tick.</summary>
    public const double RelevancePerTick = 0.05 / 3_600;

    /// <summary>How much of the fade the most important story is spared: at importance 1 it fades at half the rate.</summary>
    public const double ImportanceSlowdown = 0.5;

    /// <summary>The relevance <paramref name="belief"/> loses each tick, computed afresh at each step and never stored.</summary>
    public static double Rate(Belief belief) => RelevancePerTick * (1 - (ImportanceSlowdown * belief.Importance));

    /// <summary>Fades every living person's beliefs by <paramref name="ticks"/>, and logs each that becomes a memory.</summary>
    public static void Step(World world, int ticks, SimulationLog log)
    {
        foreach (Person person in world.People.Where(person => !person.IsDead))
        {
            foreach (Belief belief in person.Beliefs.Where(belief => !belief.IsMemory))
            {
                belief.Relevance = Math.Max(0, belief.Relevance - (Rate(belief) * ticks));
                if (belief.IsMemory)
                {
                    log.Write(new BecameMemory(world.Tick, person.Id, belief.Claim, belief.Importance, belief.Confidence));
                }
            }
        }
    }
}
