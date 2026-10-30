using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// Gives a new world a past: a few events from before tick 0, which their witnesses and known actor believe
/// first-hand and some others as hearsay. Starting values are tuned by listening (<c>docs/08 Social Model.md</c> → "Purpose").
/// </summary>
internal static class SeededPast
{
    public const int MaxEvents = 5;
    public const int PastTicks = 1_209_600;
    public const double UnknownActorShare = 0.3;
    public const double HearsayKeepsWitnessShare = 0.5;

    /// <summary>How many claims an event may draw to find a story not yet in the past, before it is left out.</summary>
    public const int ClaimDrawsPerEvent = 20;

    public static readonly (double Min, double Max) FirstHandConfidence = (0.8, 1);
    public static readonly (double Min, double Max) HearsayConfidence = (0.3, 0.6);

    // Killed isn't drawn: the past leaves everyone in the tavern alive.
    private static readonly Act[] _drawnActs = [Act.Sank, Act.Robbed, Act.Attacked, Act.Betrayed, Act.Helped, Act.WasSeenAt];

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

        // Each event is another story: a holder keeps one belief per story, and place and time don't tell two apart.
        int events = Math.Min(MaxEvents, (peopleCount + 2) / 3);
        var stories = new HashSet<Story>();
        for (int i = 0; i < events; i++)
        {
            for (int draw = 0; draw < ClaimDrawsPerEvent; draw++)
            {
                Claim claim = DrawClaim(world, tavern);
                if (stories.Add(claim.Story))
                {
                    DrawEvent(world, claim, log);
                    break;
                }
            }
        }
    }

    private static void DrawEvent(World world, Claim claim, SimulationLog log)
    {
        Random random = world.Random;
        IReadOnlyList<Person> people = world.People;

        // A third of the tavern at most saw it, so with three or more people never everyone did.
        int third = people.Count / 3;
        Person[] candidates = [.. people.Where(person => claim.Actor.Person != person.Id)];
        random.Shuffle(candidates);
        Person[] witnesses = candidates[..random.Next(1, Math.Max(1, third) + 1)];

        // A known actor was there: they hold what they did first-hand, and never as hearsay.
        Person? actor = claim.Actor.Person is { } actorId ? world.Person(actorId) : null;
        Person[] rest = [.. people.Except(witnesses).Where(person => person != actor)];
        random.Shuffle(rest);
        Person[] hearsayHolders = rest[..random.Next(0, Math.Min(third, rest.Length) + 1)];

        foreach (Person witness in witnesses)
        {
            Believe(world, witness, claim.WithWitness(witness.Id), random.Between(FirstHandConfidence));
        }

        if (actor is not null)
        {
            Believe(world, actor, claim.WithWitness(actor.Id), random.Between(FirstHandConfidence));
        }

        foreach (Person holder in hearsayHolders)
        {
            PersonId? witness = random.NextDouble() < HearsayKeepsWitnessShare
                ? witnesses[random.Next(witnesses.Length)].Id
                : null;
            Believe(world, holder, claim.WithWitness(witness), random.Between(HearsayConfidence));
        }

        log.Write(new PastEvent(claim.Tick, claim, [.. witnesses.Select(witness => witness.Id)]));
    }

    // The claim as its witnesses tell it, with the witness left for each to fill in.
    private static Claim DrawClaim(World world, ThingId tavern)
    {
        Random random = world.Random;
        IReadOnlyList<Person> people = world.People;
        Act act = _drawnActs[random.Next(_drawnActs.Length)];
        Person actor = people[random.Next(people.Count)];
        int tick = random.Next(-PastTicks, 0);

        (PersonOrThing Actor, PersonOrThing Target, ThingId Place, double Severity) drawn;
        switch (act)
        {
            case Act.Sank:
                Thing ship = Pick(random, world.Things.Where(thing => thing.Kind == ThingKind.Ship));
                drawn = (PersonOrUnknown(random, actor), PersonOrThing.Of(ship.Id), ship.Id, Claim.TableSeverity(act, ship));
                break;
            case Act.Robbed:
                Thing item = Pick(random, world.Things.Where(thing => thing.Kind == ThingKind.Item));
                drawn = (PersonOrUnknown(random, actor), PersonOrThing.Of(item.Id), tavern, Claim.TableSeverity(act, item));
                break;
            case Act.WasSeenAt:
                Thing seenAt = Pick(random, world.Things.Where(thing => thing.Kind == ThingKind.Ship || thing.Id == tavern));
                drawn = (PersonOrThing.Of(actor.Id), PersonOrThing.Of(seenAt.Id), seenAt.Id, Claim.TableSeverity(act));
                break;
            default:
                Person target = Pick(random, people.Where(person => person != actor));
                drawn = (PersonOrThing.Of(actor.Id), PersonOrThing.Of(target.Id), tavern, Claim.TableSeverity(act));
                break;
        }

        var claim = new Claim(drawn.Actor, act, drawn.Target, drawn.Place, tick, drawn.Severity, blame: 0, witness: null);
        return claim.WithBlameDrawn(random);
    }

    private static void Believe(World world, Person holder, Claim claim, double confidence) =>
        holder.Believe(claim, confidence, Importance.Judge(world, holder, claim));

    private static PersonOrThing PersonOrUnknown(Random random, Person person) =>
        random.NextDouble() < UnknownActorShare ? PersonOrThing.Unknown : PersonOrThing.Of(person.Id);

    private static T Pick<T>(Random random, IEnumerable<T> choices)
    {
        T[] all = [.. choices];
        return all[random.Next(all.Length)];
    }
}
