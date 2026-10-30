using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// Lowers every belief's relevance as time passes, slower the more it matters, until it is a memory
/// (<c>docs/08 Social Model.md</c> → "Fading"). The lives are tuned by listening.
/// </summary>
internal static class Fading
{
    /// <summary>The ticks a belief of no importance takes to fade from relevance 1: 86,400 (a day).</summary>
    public const double LifeAtNoImportanceTicks = 86_400;

    /// <summary>The factor by which a story's life grows from importance 0 to 1: 30 times longer.</summary>
    public const double LifeGrowth = 30;

    /// <summary>
    /// The relevance <paramref name="belief"/> loses each tick, 1 over its life, which grows geometrically with
    /// importance; computed afresh at each step and never stored.
    /// </summary>
    public static double Rate(Belief belief) => 1 / (LifeAtNoImportanceTicks * Math.Pow(LifeGrowth, belief.Importance));

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
