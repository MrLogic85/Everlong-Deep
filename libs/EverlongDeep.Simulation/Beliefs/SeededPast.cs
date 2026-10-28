using System.Globalization;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// Gives a new world a past: a few events from before hour 0, which their witnesses believe first-hand and some
/// others as hearsay. Starting values are tuned by listening (<c>docs/08 Social Model.md</c> → "Purpose").
/// </summary>
internal static class SeededPast
{
    public const int MaxEvents = 5;
    public const int Days = 14;
    public const double UnknownActorShare = 0.3;
    public const double HearsayKeepsWitnessShare = 0.5;

    /// <summary>Relevance a holder adds to a story they are the actor, target or witness of.</summary>
    public const double OwnStoryRelevance = 0.3;

    public static readonly (double Min, double Max) HarmfulBlame = (-0.2, 1);
    public static readonly (double Min, double Max) FirstHandConfidence = (0.8, 1);
    public static readonly (double Min, double Max) HearsayConfidence = (0.3, 0.6);

    // Killed isn't drawn: nobody in the world can die.
    private static readonly Act[] _drawnActs = [Act.Sank, Act.Robbed, Act.Attacked, Act.Betrayed, Act.Helped, Act.WasSeenAt];

    /// <summary>The severity an act starts at, before the target's importance scales it.</summary>
    public static double BaseSeverity(Act act) => act switch
    {
        Act.Sank => 0.8,
        Act.Killed => 0.9,
        Act.Robbed => 0.4,
        Act.Attacked => 0.5,
        Act.Betrayed => 0.6,
        Act.Helped => 0.1,
        Act.WasSeenAt => 0,
        _ => throw new ArgumentOutOfRangeException(nameof(act), act, null),
    };

    /// <summary>A thing's importance moves the severity of harm to it by up to half.</summary>
    public static double ImportanceScale(Thing thing) => 0.5 + (thing.Importance / 2);

    /// <summary>
    /// Draws the past of <paramref name="world"/>, whose people are in <paramref name="tavern"/>. Drawn after
    /// everything else, so a seed's people, relations and things don't depend on it.
    /// </summary>
    public static void Draw(World world, ThingId tavern, SimulationLog log)
    {
        int peopleCount = world.People.Count;
        if (peopleCount < 2)
        {
            return;
        }

        int events = Math.Min(MaxEvents, (peopleCount + 2) / 3);
        for (int i = 0; i < events; i++)
        {
            DrawEvent(world, tavern, log);
        }
    }

    private static void DrawEvent(World world, ThingId tavern, SimulationLog log)
    {
        Random random = world.Random;
        IReadOnlyList<Person> people = world.People;
        Claim claim = DrawClaim(world, tavern);

        // A third of the tavern at most saw it, so with three or more people never everyone did.
        int third = people.Count / 3;
        Person[] candidates = [.. people.Where(person => claim.Actor.Person != person.Id)];
        random.Shuffle(candidates);
        Person[] witnesses = candidates[..random.Next(1, Math.Max(1, third) + 1)];

        Person[] rest = [.. people.Except(witnesses)];
        random.Shuffle(rest);
        Person[] hearsayHolders = rest[..random.Next(0, Math.Min(third, rest.Length) + 1)];

        foreach (Person witness in witnesses)
        {
            Believe(witness, claim.WithWitness(witness.Id), Between(random, FirstHandConfidence));
        }

        foreach (Person holder in hearsayHolders)
        {
            PersonId? witness = random.NextDouble() < HearsayKeepsWitnessShare
                ? witnesses[random.Next(witnesses.Length)].Id
                : null;
            Believe(holder, claim.WithWitness(witness), Between(random, HearsayConfidence));
        }

        log.Write(string.Create(
            CultureInfo.InvariantCulture,
            $"Past, hour {claim.Hour}: {ClaimText.Describe(world, claim)} " +
            $"(severity {claim.Severity:0.00}, blame {claim.Blame:0.00}), " +
            $"seen by {string.Join(", ", witnesses.Select(witness => witness.Name))}"));
    }

    // The claim as its witnesses tell it, with the witness left for each to fill in.
    private static Claim DrawClaim(World world, ThingId tavern)
    {
        Random random = world.Random;
        IReadOnlyList<Person> people = world.People;
        Act act = _drawnActs[random.Next(_drawnActs.Length)];
        Person actor = people[random.Next(people.Count)];
        int hour = random.Next(-Days * 24, 0);

        (PersonOrThing Actor, PersonOrThing Target, ThingId Place, double Severity) drawn;
        switch (act)
        {
            case Act.Sank:
                Thing ship = Pick(random, world.Things.Where(thing => thing.Kind == ThingKind.Ship));
                drawn = (PersonOrUnknown(random, actor), PersonOrThing.Of(ship.Id), ship.Id, BaseSeverity(act) * ImportanceScale(ship));
                break;
            case Act.Robbed:
                Thing item = Pick(random, world.Things.Where(thing => thing.Kind == ThingKind.Item));
                drawn = (PersonOrUnknown(random, actor), PersonOrThing.Of(item.Id), tavern, BaseSeverity(act) * ImportanceScale(item));
                break;
            case Act.WasSeenAt:
                Thing seenAt = Pick(random, world.Things.Where(thing => thing.Kind == ThingKind.Ship || thing.Id == tavern));
                drawn = (PersonOrThing.Of(actor.Id), PersonOrThing.Of(seenAt.Id), seenAt.Id, BaseSeverity(act));
                break;
            default:
                Person target = Pick(random, people.Where(person => person != actor));
                drawn = (PersonOrThing.Of(actor.Id), PersonOrThing.Of(target.Id), tavern, BaseSeverity(act));
                break;
        }

        bool harmful = act is not (Act.Helped or Act.WasSeenAt);
        double blame = harmful && !drawn.Actor.IsUnknown ? Between(random, HarmfulBlame) : 0;
        return new Claim(drawn.Actor, act, drawn.Target, drawn.Place, hour, drawn.Severity, blame, witness: null);
    }

    private static void Believe(Person holder, Claim claim, double confidence)
    {
        PersonOrThing self = PersonOrThing.Of(holder.Id);
        bool ownStory = claim.Actor == self || claim.Target == self || claim.Witness == holder.Id;
        double relevance = Math.Min(1, claim.Severity + (ownStory ? OwnStoryRelevance : 0));
        holder.Believe(claim, confidence, relevance);
    }

    private static PersonOrThing PersonOrUnknown(Random random, Person person) =>
        random.NextDouble() < UnknownActorShare ? PersonOrThing.Unknown : PersonOrThing.Of(person.Id);

    private static T Pick<T>(Random random, IEnumerable<T> choices)
    {
        T[] all = [.. choices];
        return all[random.Next(all.Length)];
    }

    private static double Between(Random random, (double Min, double Max) range) =>
        range.Min + (random.NextDouble() * (range.Max - range.Min));
}
