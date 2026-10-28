using System.Diagnostics;

namespace EverlongDeep.Simulation.People;

/// <summary>
/// How people form impressions of each other's traits and how well they get on by them, as
/// <c>docs/08 Social Model.md</c> → "Impressions" defines.
/// </summary>
internal static class TraitImpressions
{
    /// <summary>How far a first impression can miss the real value, either way.</summary>
    public const double FirstMeetingNoise = 0.3;

    /// <summary>How far kin's impressions can miss the real value, either way.</summary>
    public const double KinNoise = 0.1;

    /// <summary>The holder of <paramref name="relation"/> sizes up <paramref name="other"/>'s visible axes; nothing else changes.</summary>
    public static void FirstMeeting(Relation relation, Person other, Random random)
    {
        Debug.Assert(relation.Impressions.Count == 0, "A first meeting comes before any impression");
        Form(relation, other, Enum.GetValues<TraitAxis>().Where(TraitTables.IsVisible), FirstMeetingNoise, random);
    }

    /// <summary>The holder of <paramref name="relation"/> grew up with <paramref name="other"/> and knows every axis, closely.</summary>
    public static void GrewUpWith(Relation relation, Person other, Random random)
    {
        Debug.Assert(relation.Impressions.Count == 0, "Kin start with no impressions to overwrite");
        Form(relation, other, Enum.GetValues<TraitAxis>(), KinNoise, random);
    }

    /// <summary>
    /// How well <paramref name="holder"/> thinks they get on with the other person of <paramref name="relation"/>,
    /// −1 to 1, from the holder's impressions alone; 0 when the holder has none that matter.
    /// </summary>
    public static double Compatibility(Person holder, Relation relation)
    {
        double sum = 0;
        double most = 0;
        foreach ((TraitAxis axis, double impression) in relation.Impressions)
        {
            double affinity = TraitTables.Affinity(axis);
            sum += affinity * holder.Traits[axis] * impression;
            most += Math.Abs(affinity);
        }

        // Scaled by the most the formed axes could add up to, so a stranger's few axes can weigh as much as kin's.
        return most == 0 ? 0 : sum / most;
    }

    // Drawn in axis order, so a seed forms the same impressions.
    private static void Form(Relation relation, Person other, IEnumerable<TraitAxis> axes, double noise, Random random)
    {
        foreach (TraitAxis axis in axes)
        {
            double missedBy = ((random.NextDouble() * 2) - 1) * noise;
            relation.SetImpression(axis, Math.Clamp(other.Traits[axis] + missedBy, -1, 1));
        }
    }
}
