namespace EverlongDeep.Simulation.People;

/// <summary>How strangers come to know each other, as <c>docs/08 Social Model.md</c> → "Impressions" defines.</summary>
internal static class Meeting
{
    public const double FirstOpinionScale = 0.5;
    public const double FirstTrustBase = 0.3;
    public const double FirstTrustScale = 0.2;

    /// <summary>
    /// Starts each side's relation if it has none, in this draw order: <paramref name="first"/>'s impressions of
    /// <paramref name="second"/>, then the other way. Feelings come from each side's compatibility.
    /// </summary>
    public static void Introduce(Person first, Person second, Random random)
    {
        Relation? firstSide = MeetIfStrangers(first, second, random);
        Relation? secondSide = MeetIfStrangers(second, first, random);
        SetFirstFeelings(first, firstSide);
        SetFirstFeelings(second, secondSide);
    }

    private static Relation? MeetIfStrangers(Person holder, Person other, Random random)
    {
        if (holder.RelationTo(other.Id) is not null)
        {
            return null;
        }

        Relation relation = holder.Meet(other.Id, opinion: 0, trust: FirstTrustBase);
        TraitImpressions.FirstMeeting(relation, other, random);
        return relation;
    }

    private static void SetFirstFeelings(Person holder, Relation? relation)
    {
        if (relation is null)
        {
            return;
        }

        double compatibility = TraitImpressions.Compatibility(holder, relation);
        relation.Opinion = FirstOpinionScale * compatibility;
        relation.Trust = FirstTrustBase + (FirstTrustScale * compatibility);
    }
}
