using System.Globalization;

namespace EverlongDeep.Simulation.People;

/// <summary>
/// Forms and breaks friend and rival ties from opinion and trust held over time
/// (<c>docs/08 Social Model.md</c> → "Relation").
/// </summary>
internal static class TieFormation
{
    /// <summary>How long feelings must hold past a mark before a tie forms or breaks.</summary>
    public const int HoldHours = 24;

    // Each tie breaks at a mark short of where it forms, so feelings near one mark don't make it flicker.
    public const double FriendFormOpinion = 0.6;
    public const double FriendFormTrust = 0.5;
    public const double FriendBreakOpinion = 0.3;
    public const double FriendBreakTrust = 0.25;
    public const double RivalFormOpinion = -0.6;
    public const double RivalBreakOpinion = -0.3;

    /// <summary>Advances every relation in <paramref name="world"/> by one hour.</summary>
    public static void StepHour(World world, SimulationLog log)
    {
        foreach (Person person in world.People)
        {
            foreach (Relation relation in person.Relations)
            {
                StepFriend(world, person, relation, log);
                StepRival(world, person, relation, log);
            }
        }
    }

    private static void StepFriend(World world, Person holder, Relation relation, SimulationLog log)
    {
        bool isFriend = relation.Ties.HasFlag(Ties.Friend);
        bool pastMark = isFriend
            ? relation.Opinion < FriendBreakOpinion || relation.Trust < FriendBreakTrust
            : relation.Opinion >= FriendFormOpinion && relation.Trust >= FriendFormTrust;
        relation.FriendChangeHours = pastMark ? relation.FriendChangeHours + 1 : 0;
        if (relation.FriendChangeHours < HoldHours)
        {
            return;
        }

        relation.FriendChangeHours = 0;
        relation.Ties ^= Ties.Friend;
        Log(world, holder, relation, isFriend ? "no longer counts" : "now counts", "friend", log);
    }

    private static void StepRival(World world, Person holder, Relation relation, SimulationLog log)
    {
        bool isRival = relation.Ties.HasFlag(Ties.Rival);
        bool pastMark = isRival
            ? relation.Opinion > RivalBreakOpinion
            : relation.Opinion <= RivalFormOpinion;
        relation.RivalChangeHours = pastMark ? relation.RivalChangeHours + 1 : 0;
        if (relation.RivalChangeHours < HoldHours)
        {
            return;
        }

        relation.RivalChangeHours = 0;
        relation.Ties ^= Ties.Rival;
        Log(world, holder, relation, isRival ? "no longer counts" : "now counts", "rival", log);
    }

    private static void Log(World world, Person holder, Relation relation, string change, string tie, SimulationLog log)
    {
        string other = world.Person(relation.Other).Name;
        log.Write(string.Create(
            CultureInfo.InvariantCulture,
            $"Hour {world.Hour}: {holder.Name} {change} {other} as a {tie} " +
            $"(opinion {relation.Opinion:0.00}, trust {relation.Trust:0.00})"));
    }
}
