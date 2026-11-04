using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Events;

/// <summary>
/// Someone an <see cref="Event"/> brings in, held unchanging until they enter as a <see cref="Person"/>
/// (<c>docs/08 Social Model.md</c> → "Events"), so the event stays as it was scheduled.
/// </summary>
public sealed record Newcomer(PersonId Id, string Name, Gender Gender, TraitProfile Traits, ThingId Place)
{
    internal Person ToPerson() => new(Id, Name, Gender, Traits, Place);
}
