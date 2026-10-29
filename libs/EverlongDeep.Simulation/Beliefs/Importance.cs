using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// How much a story matters to its holder: their own judgment of who did what to whom, never the teller's dials
/// (<c>docs/08 Social Model.md</c> → "Belief"). The stakes are tuned by listening.
/// </summary>
internal static class Importance
{
    /// <summary>The stake of a holder who is the story's actor, target or witness.</summary>
    public const double OwnStoryStake = 0.3;

    /// <summary>The stake of a holder with a tie to the actor or the target.</summary>
    public const double TieStake = 0.2;

    /// <summary>The stake of a holder with a strong opinion of the actor or the target.</summary>
    public const double StrongOpinionStake = 0.2;

    /// <summary>How far from 0 an opinion is strong, either way.</summary>
    public const double StrongOpinion = 0.6;

    /// <summary>
    /// How much <paramref name="claim"/> matters to <paramref name="holder"/>: its act's severity in the "Claim"
    /// table for its target, plus their stake in it.
    /// </summary>
    public static double Judge(World world, Person holder, Claim claim)
    {
        double severity = Claim.TableSeverity(claim.Act, claim.Target.Thing is { } thing ? world.Thing(thing) : null);
        double stake =
            (claim.Involves(holder.Id) ? OwnStoryStake : 0) +
            (AboutSomeone(holder, claim, relation => relation.Ties != Ties.None) ? TieStake : 0) +
            (AboutSomeone(holder, claim, relation => Math.Abs(relation.Opinion) >= StrongOpinion) ? StrongOpinionStake : 0);
        return Math.Min(1, severity + stake);
    }

    // Whether the holder's relation to the actor or the target matches; a thing, an unknown or a stranger never does.
    private static bool AboutSomeone(Person holder, Claim claim, Func<Relation, bool> matches) =>
        Matches(holder, claim.Actor, matches) || Matches(holder, claim.Target, matches);

    private static bool Matches(Person holder, PersonOrThing who, Func<Relation, bool> matches) =>
        who.Person is { } person && holder.RelationTo(person) is { } relation && matches(relation);
}
