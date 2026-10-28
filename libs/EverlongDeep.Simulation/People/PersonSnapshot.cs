using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.People;

/// <summary>Everything about a person at one moment: who they are, whom they know and what they believe.</summary>
public sealed record PersonSnapshot(
    PersonId Id,
    string Name,
    ThingId Place,
    TraitProfile Traits,
    IReadOnlyList<RelationSnapshot> Relations,
    IReadOnlyList<BeliefSnapshot> Beliefs)
{
    internal static PersonSnapshot Of(Person person) => new(
        person.Id,
        person.Name,
        person.Place,
        person.Traits,
        [.. person.Relations.Select(RelationSnapshot.Of)],
        [.. person.Beliefs.Select(BeliefSnapshot.Of)]);

    public RelationSnapshot? RelationTo(PersonId other) => Relations.FirstOrDefault(relation => relation.Other == other);
}
