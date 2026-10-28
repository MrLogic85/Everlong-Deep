using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>A person or a thing a claim names, by id, or unknown.</summary>
public readonly record struct PersonOrThing
{
    private PersonOrThing(PersonId? person, ThingId? thing)
    {
        Person = person;
        Thing = thing;
    }

    public static PersonOrThing Unknown => default;

    public PersonId? Person { get; }
    public ThingId? Thing { get; }
    public bool IsUnknown => Person is null && Thing is null;

    public static PersonOrThing Of(PersonId person) => new(person, null);
    public static PersonOrThing Of(ThingId thing) => new(null, thing);
}
