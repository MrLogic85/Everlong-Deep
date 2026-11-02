using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Storyteller;

/// <summary>A claim as a sentence, and the verbs the Storyteller's other texts reuse.</summary>
public static class ClaimText
{
    /// <summary>The claim as a sentence with its details, without its tick, dials or witness: "Someone sank the Saltwren in a storm".</summary>
    public static string Describe(World world, Claim claim)
    {
        string sentence = Describe(claim, who => Name(world, who));
        return char.ToUpperInvariant(sentence[0]) + sentence[1..];
    }

    /// <summary>The claim as a sentence with each person and thing written by <paramref name="name"/>, uncapitalized.</summary>
    public static string Describe(Claim claim, Func<PersonOrThing, string> name)
    {
        string place = claim.Target == PersonOrThing.Of(claim.Place) ? "" : $" at {name(PersonOrThing.Of(claim.Place))}";
        string details = claim.Details.Count == 0 ? "" : $" {string.Join(", ", claim.Details.Select(Phrase))}";
        return $"{name(claim.Actor)} {Verb(claim.Act)} {name(claim.Target)}{place}{details}";
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

    /// <summary>The detail as a phrase that follows the claim's sentence, such as "with a knife".</summary>
    public static string Phrase(Detail detail) => detail switch
    {
        Detail.InAStorm => "in a storm",
        Detail.AtNight => "at night",
        Detail.WithAllHands => "with all hands",
        Detail.OffTheReef => "off the reef",
        Detail.WithAKnife => "with a knife",
        Detail.InTheBack => "in the back",
        Detail.WithoutAWord => "without a word",
        Detail.OverCards => "over cards",
        Detail.InPlainSight => "in plain sight",
        Detail.DuringABrawl => "during a brawl",
        Detail.WithABottle => "with a bottle",
        Detail.FromBehind => "from behind",
        Detail.ForCoin => "for coin",
        Detail.OverADebt => "over a debt",
        Detail.AfterYears => "after years",
        Detail.WithCoin => "with coin",
        Detail.InAFight => "in a fight",
        Detail.WithoutAsking => "without asking",
        Detail.DrinkingAlone => "drinking alone",
        Detail.WithAStranger => "with a stranger",
        _ => throw new ArgumentOutOfRangeException(nameof(detail), detail, null),
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
