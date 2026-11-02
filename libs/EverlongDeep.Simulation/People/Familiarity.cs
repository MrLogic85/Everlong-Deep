namespace EverlongDeep.Simulation.People;

/// <summary>
/// Raises familiarity with each conversation and fades it as time passes (<c>docs/08 Social Model.md</c> →
/// "Relation"). The rates are tuned by listening.
/// </summary>
internal static class Familiarity
{
    /// <summary>The share of the gap to 1 that one conversation closes, so each adds less the better they know each other.</summary>
    public const double RiseShare = 0.05;

    /// <summary>The ticks familiarity takes to fade from 1 to 0 with no conversation: 2,592,000 (a month).</summary>
    public const double LifeTicks = 2_592_000;

    /// <summary>One conversation heard out between the holder and <see cref="Relation.Other"/>.</summary>
    public static void Rise(Relation relation) => relation.Familiarity += RiseShare * (1 - relation.Familiarity);

    /// <summary>Fades every living person's familiarity by <paramref name="ticks"/>, toward the dead included.</summary>
    public static void Step(World world, int ticks)
    {
        foreach (Person person in world.People.Where(person => !person.IsDead))
        {
            foreach (Relation relation in person.Relations)
            {
                relation.Familiarity = Math.Max(0, relation.Familiarity - (ticks / LifeTicks));
            }
        }
    }
}
