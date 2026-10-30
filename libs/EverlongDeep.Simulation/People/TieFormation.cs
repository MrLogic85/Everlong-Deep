using System.Globalization;

namespace EverlongDeep.Simulation.People;

/// <summary>
/// Forms and breaks friend and rival ties from opinion and trust held over time
/// (<c>docs/08 Social Model.md</c> → "Relation").
/// </summary>
internal static class TieFormation
{
    /// <summary>How long feelings must hold past a mark before a tie forms or breaks.</summary>
    public const int HoldTicks = 86_400;

    // Each tie breaks at a mark short of where it forms, so feelings near one mark don't make it flicker.
    public const double FriendFormOpinion = 0.6;
    public const double FriendFormTrust = 0.5;
    public const double FriendBreakOpinion = 0.3;
    public const double FriendBreakTrust = 0.25;
    public const double RivalFormOpinion = -0.6;
    public const double RivalBreakOpinion = -0.3;

    /// <summary>Advances every living person's relations by <paramref name="ticks"/>, those toward the dead included.</summary>
    public static void Step(World world, int ticks, SimulationLog log)
    {
        foreach (Person person in world.People.Where(person => !person.IsDead))
        {
            foreach (Relation relation in person.Relations)
            {
                StepFriend(world, person, relation, ticks, log);
                StepRival(world, person, relation, ticks, log);
            }
        }
    }

    private static void StepFriend(World world, Person holder, Relation relation, int ticks, SimulationLog log)
    {
        bool isFriend = relation.Ties.HasFlag(Ties.Friend);
        bool pastMark = isFriend
            ? relation.Opinion < FriendBreakOpinion || relation.Trust < FriendBreakTrust
            : relation.Opinion >= FriendFormOpinion && relation.Trust >= FriendFormTrust;
        relation.FriendChangeTicks = pastMark ? relation.FriendChangeTicks + ticks : 0;
        if (relation.FriendChangeTicks < HoldTicks)
        {
            return;
        }

        relation.FriendChangeTicks = 0;
        relation.Ties ^= Ties.Friend;
        Log(world, holder, relation, isFriend ? "no longer counts" : "now counts", "friend", log);
    }

    private static void StepRival(World world, Person holder, Relation relation, int ticks, SimulationLog log)
    {
        bool isRival = relation.Ties.HasFlag(Ties.Rival);
        bool pastMark = isRival
            ? relation.Opinion > RivalBreakOpinion
            : relation.Opinion <= RivalFormOpinion;
        relation.RivalChangeTicks = pastMark ? relation.RivalChangeTicks + ticks : 0;
        if (relation.RivalChangeTicks < HoldTicks)
        {
            return;
        }

        relation.RivalChangeTicks = 0;
        relation.Ties ^= Ties.Rival;
        Log(world, holder, relation, isRival ? "no longer counts" : "now counts", "rival", log);
    }

    private static void Log(World world, Person holder, Relation relation, string change, string tie, SimulationLog log)
    {
        string other = world.Person(relation.Other).Name;
        log.Write(world.Tick, string.Create(
            CultureInfo.InvariantCulture,
            $"{holder.Name} {change} {other} as a {tie} " +
            $"(opinion {relation.Opinion:0.00}, trust {relation.Trust:0.00})"));
    }
}
