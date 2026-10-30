using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;

namespace EverlongDeep.Storyteller;

/// <summary>People, things and claims as a conversation's trace writes them: by id and name.</summary>
public static class TraceNames
{
    /// <summary>A person's or thing's id as the trace prints it: "p3" or "t5", and "?" when unknown.</summary>
    public static string Id(PersonOrThing who) => who switch
    {
        { Person: { } person } => $"p{person.Value}",
        { Thing: { } thing } => $"t{thing.Value}",
        _ => "?",
    };

    /// <summary>A person or thing by id and name, "p1 Bram" or "t3 Grey Heron", and "someone" when unknown.</summary>
    public static string Named(World world, PersonOrThing who) => who switch
    {
        { Person: { } person } => $"{Id(who)} {world.Person(person).Name}",
        { Thing: { } thing } => $"{Id(who)} {world.Thing(thing).Name}",
        _ => "someone",
    };

    public static string Named(World world, PersonId person) => Named(world, PersonOrThing.Of(person));

    /// <summary>The claim as a sentence by id and name, without its time, dials or witness: "p5 Fen sank t3 Grey Heron".</summary>
    public static string Describe(World world, Claim claim) => ClaimText.Describe(claim, who => Named(world, who));

    /// <summary>The claim's source: "hearsay", or its witness and what they did to know of it.</summary>
    public static string Witness(World world, Claim claim) =>
        claim.Witness is { } witness ? $"{Named(world, witness)} {ClaimText.WitnessVerb(claim)} it" : "hearsay";
}
