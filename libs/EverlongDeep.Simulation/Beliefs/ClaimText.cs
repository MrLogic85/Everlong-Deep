using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>Words a claim the same way in the log and in every tool that prints one.</summary>
public static class ClaimText
{
    /// <summary>The claim as a sentence without its tick, dials or witness: "Someone sank the Saltwren".</summary>
    public static string Describe(World world, Claim claim)
    {
        string sentence = Describe(claim, who => Name(world, who));
        return char.ToUpperInvariant(sentence[0]) + sentence[1..];
    }

    /// <summary>The claim as a sentence with each person and thing written by <paramref name="name"/>, uncapitalized.</summary>
    public static string Describe(Claim claim, Func<PersonOrThing, string> name)
    {
        string place = claim.Target == PersonOrThing.Of(claim.Place) ? "" : $" at {name(PersonOrThing.Of(claim.Place))}";
        return $"{name(claim.Actor)} {Verb(claim.Act)} {name(claim.Target)}{place}";
    }

    /// <summary>What the witness did to know of it: "did" when they are the actor, otherwise "saw".</summary>
    public static string WitnessVerb(Claim claim) => claim.WitnessDidIt ? "did" : "saw";

    /// <summary>The act as the claim's verb, such as "sank".</summary>
    public static string Verb(Act act) => act switch
    {
        Act.Sank => "sank",
        Act.Killed => "killed",
        // The target of a robbery is what was taken.
        Act.Robbed => "stole",
        Act.Attacked => "attacked",
        Act.Betrayed => "betrayed",
        Act.Helped => "helped",
        Act.WasSeenAt => "was seen at",
        _ => throw new ArgumentOutOfRangeException(nameof(act), act, null),
    };

    private static string Name(World world, PersonOrThing who)
    {
        if (who.Person is { } person)
        {
            return world.Person(person).Name;
        }

        if (who.Thing is { } id)
        {
            Thing thing = world.Thing(id);
            // A place's name carries its own article: "The Drowned Lantern".
            return thing.Kind == ThingKind.Place ? thing.Name : $"the {thing.Name}";
        }

        return "someone";
    }
}
