namespace EverlongDeep.Simulation.People;

/// <summary>A relation as it stood at one moment.</summary>
public sealed record RelationSnapshot(
    PersonId Other, double Opinion, double Trust, Ties Ties, IReadOnlyDictionary<TraitAxis, double> Impressions)
{
    internal static RelationSnapshot Of(Relation relation) =>
        new(relation.Other, relation.Opinion, relation.Trust, relation.Ties, new Dictionary<TraitAxis, double>(relation.Impressions));
}
