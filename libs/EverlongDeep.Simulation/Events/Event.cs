using System.Diagnostics;
using System.Globalization;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Events;

/// <summary>
/// Something that happens at the first minute of <see cref="Hour"/>, as <c>docs/08 Social Model.md</c> → "Events"
/// defines: <see cref="Happen"/> brings in the <see cref="Newcomer"/>, applies the act, gives the people there their
/// beliefs by role and writes one log line. Nothing keeps the event afterwards: the truth is never stored.
/// </summary>
/// <param name="Act">What was done.</param>
/// <param name="Actor">Who did it, the newcomer or someone already in the world.</param>
/// <param name="Target">Who it was done to; a killed target dies.</param>
/// <param name="Place">Where it happens; everyone in a role is there.</param>
/// <param name="Hour">The hour it happens at, from its first minute.</param>
/// <param name="KeyWitnesses">The people closest to it, who most likely saw it and who did it.</param>
/// <param name="Bystanders">The people further off, who may only have caught the commotion.</param>
/// <param name="Newcomer">Someone who enters the world as it happens, a stranger to all.</param>
public sealed record Event(
    Act Act,
    PersonId Actor,
    PersonId Target,
    ThingId Place,
    int Hour,
    IReadOnlyList<PersonId> KeyWitnesses,
    IReadOnlyList<PersonId> Bystanders,
    Person? Newcomer)
{
    // How likely a witness saw it, and, having seen it, saw who did it. Tuned by listening until stop 5.
    public const double KeyWitnessSawChance = 0.9;
    public const double KeyWitnessSawWhoChance = 0.8;
    public const double BystanderSawChance = 0.4;
    public const double BystanderSawWhoChance = 0.3;

    /// <summary>
    /// Makes the event happen in <paramref name="world"/>, whose clock must be at the first minute of
    /// <see cref="Hour"/>. Draws from the world's randomness only here.
    /// </summary>
    public void Happen(World world, SimulationLog log)
    {
        string? problem = Problem(world);
        Debug.Assert(problem is null, problem);
        if (problem is not null)
        {
            log.Write($"{world.Clock} Event skipped, {Act} by p{Actor.Value} of p{Target.Value} at t{Place.Value}, hour {Hour}: {problem}");
            return;
        }

        if (Newcomer is not null)
        {
            world.Enter(Newcomer);
        }

        if (Act == Act.Killed)
        {
            world.Kill(Target);
        }

        // Everyone there holds the same dials; blame applies only where they know who did it.
        Random random = world.Random;
        Claim seen = new Claim(PersonOrThing.Of(Actor), Act, PersonOrThing.Of(Target), Place, Hour, Claim.TableSeverity(Act), blame: 0, witness: null)
            .WithBlameDrawn(random);
        Claim unknownActor = new Claim(PersonOrThing.Unknown, Act, seen.Target, Place, Hour, seen.Severity, blame: 0, witness: null);

        Believe(world, world.Person(Actor), seen.WithWitness(Actor), random.Between(SeededPast.FirstHandConfidence));
        var saw = new List<string>();
        var heard = new List<string>();
        foreach ((PersonId id, bool key) in KeyWitnesses.Select(id => (id, true)).Concat(Bystanders.Select(id => (id, false))))
        {
            Person witness = world.Person(id);
            if (random.NextDouble() < (key ? KeyWitnessSawChance : BystanderSawChance))
            {
                Claim claim = random.NextDouble() < (key ? KeyWitnessSawWhoChance : BystanderSawWhoChance) ? seen : unknownActor;
                Believe(world, witness, claim.WithWitness(id), random.Between(SeededPast.FirstHandConfidence));
                saw.Add(witness.Name);
            }
            else
            {
                Believe(world, witness, unknownActor, random.Between(SeededPast.HearsayConfidence));
                heard.Add(witness.Name);
            }
        }

        string entered = Newcomer is null ? "" : $"{Newcomer.Name} came into {world.Thing(Place).Name}. ";
        log.Write(string.Create(
            CultureInfo.InvariantCulture,
            $"{world.Clock} {entered}{ClaimText.Describe(world, seen)} (severity {seen.Severity:0.00}, blame {seen.Blame:0.00}), " +
            $"seen by {Names(saw)}, only the commotion caught by {Names(heard)}"));
    }

    // Why the event can't happen in the world as it is, or null when it can.
    private string? Problem(World world)
    {
        if (world.Minute != Hour * World.MinutesPerHour)
        {
            return $"the clock is at minute {world.Minute}, not the hour's first";
        }

        if (!world.Things.Any(thing => thing.Id == Place && thing.CanHoldPeople))
        {
            return $"thing {Place.Value} is not a place";
        }

        if (Newcomer is not null)
        {
            // The talk keeps each place's people in id order by adding a newcomer last.
            if (world.People.Any(person => person.Id.Value >= Newcomer.Id.Value))
            {
                return $"newcomer p{Newcomer.Id.Value} doesn't have the highest id";
            }

            if (Newcomer.Place != Place)
            {
                return $"newcomer p{Newcomer.Id.Value} would enter elsewhere";
            }

            // A newcomer is a stranger to all: giving them a relation would mean starting the other side too.
            if (Newcomer.Relations.Count > 0)
            {
                return $"newcomer p{Newcomer.Id.Value} already knows someone";
            }
        }

        PersonId[] involved = [Actor, Target, .. KeyWitnesses, .. Bystanders];
        if (involved.GroupBy(id => id).FirstOrDefault(roles => roles.Count() > 1) is { } twice)
        {
            return $"p{twice.Key.Value} has two roles";
        }

        var story = new Story(Act, PersonOrThing.Of(Target));
        foreach (PersonId id in involved)
        {
            Person? person = id == Newcomer?.Id ? Newcomer : world.People.FirstOrDefault(person => person.Id == id);
            string? why = person switch
            {
                null => "isn't in the world",
                { IsDead: true } => "is dead",
                _ when person.Place != Place => $"is in t{person.Place.Value}",
                _ when person.Beliefs.Any(belief => belief.Claim.Story == story) => "already holds the story",
                _ => null,
            };
            if (why is not null)
            {
                return $"p{id.Value} {why}";
            }
        }

        return null;
    }

    private static void Believe(World world, Person holder, Claim claim, double confidence) =>
        holder.Believe(claim, confidence, Importance.Judge(world, holder, claim));

    private static string Names(List<string> names) => names.Count == 0 ? "nobody" : string.Join(", ", names);
}
