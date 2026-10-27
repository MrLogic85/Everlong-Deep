using EverlongDeep.Simulation.Places;

namespace EverlongDeep.Simulation.People;

/// <summary>A person in the world, as <c>docs/08 Social Model.md</c> → "Person" defines.</summary>
public sealed class Person(PersonId id, string name, IReadOnlyList<Trait> traits, PlaceId place)
{
    public PersonId Id { get; } = id;
    public string Name { get; } = name;
    public IReadOnlyList<Trait> Traits { get; } = traits;
    public PlaceId Place { get; } = place;
}
