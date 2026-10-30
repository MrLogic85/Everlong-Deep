using System.Diagnostics;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Events;

/// <summary>
/// Something that happens at <see cref="Tick"/>, as <c>docs/08 Social Model.md</c> → "Events" defines:
/// <see cref="Happen"/> brings in the <see cref="Newcomer"/>, applies the act, gives the people there their
/// beliefs by role and logs what happened. Nothing keeps the event afterwards: the truth is never stored.
/// </summary>
/// <param name="Act">What was done.</param>
/// <param name="Actor">Who did it, the newcomer or someone already in the world.</param>
/// <param name="Target">Who it was done to; a killed target dies.</param>
/// <param name="Place">Where it happens; everyone in a role is there.</param>
/// <param name="Tick">The tick it happens at, before that tick's talk.</param>
/// <param name="KeyWitnesses">The people closest to it, who most likely saw it and who did it.</param>
/// <param name="Bystanders">The people further off, who may only have caught the commotion.</param>
/// <param name="Newcomer">Someone who enters the world as it happens, a stranger to all.</param>
public sealed record Event(
    Act Act,
    PersonId Actor,
    PersonId Target,
    ThingId Place,
    long Tick,
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
    /// Makes the event happen in <paramref name="world"/>, whose clock must be at <see cref="Tick"/>. Draws from the
    /// world's randomness only here.
    /// </summary>
    internal void Happen(World world, SimulationLog log)
    {
        EventSkipped? skipped = Skipped(world);
        Debug.Assert(skipped is null, skipped?.ToString());
        if (skipped is not null)
        {
            log.Write(skipped);
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
        Claim seen = new Claim(PersonOrThing.Of(Actor), Act, PersonOrThing.Of(Target), Place, Tick, Claim.TableSeverity(Act), blame: 0, witness: null)
            .WithBlameDrawn(random);
        Claim unknownActor = new Claim(PersonOrThing.Unknown, Act, seen.Target, Place, Tick, seen.Severity, blame: 0, witness: null);

        Believe(world, world.Person(Actor), seen.WithWitness(Actor), random.Between(SeededPast.FirstHandConfidence));
        var saw = new List<PersonId>();
        var heard = new List<PersonId>();
        foreach ((PersonId id, bool key) in KeyWitnesses.Select(id => (id, true)).Concat(Bystanders.Select(id => (id, false))))
        {
            Person witness = world.Person(id);
            if (random.NextDouble() < (key ? KeyWitnessSawChance : BystanderSawChance))
            {
                Claim claim = random.NextDouble() < (key ? KeyWitnessSawWhoChance : BystanderSawWhoChance) ? seen : unknownActor;
                Believe(world, witness, claim.WithWitness(id), random.Between(SeededPast.FirstHandConfidence));
                saw.Add(id);
            }
            else
            {
                Believe(world, witness, unknownActor, random.Between(SeededPast.HearsayConfidence));
                heard.Add(id);
            }
        }

        log.Write(new EventHappened(world.Tick, seen, Newcomer?.Id, [.. saw], [.. heard]));
    }

    // Why the event can't happen in the world as it is, or null when it can.
    private EventSkipped? Skipped(World world)
    {
        if (world.Tick != Tick)
        {
            return Skip(SkipReason.ClockElsewhere);
        }

        if (!world.Things.Any(thing => thing.Id == Place && thing.CanHoldPeople))
        {
            return Skip(SkipReason.NotAPlace);
        }

        if (Newcomer is not null)
        {
            // The talk keeps each place's people in id order by adding a newcomer last.
            if (world.People.Any(person => person.Id.Value >= Newcomer.Id.Value))
            {
                return Skip(SkipReason.NewcomerNotLast, Newcomer.Id);
            }

            if (Newcomer.Place != Place)
            {
                return Skip(SkipReason.NewcomerElsewhere, Newcomer.Id);
            }

            // A newcomer is a stranger to all: giving them a relation would mean starting the other side too.
            if (Newcomer.Relations.Count > 0)
            {
                return Skip(SkipReason.NewcomerKnowsSomeone, Newcomer.Id);
            }
        }

        PersonId[] involved = [Actor, Target, .. KeyWitnesses, .. Bystanders];
        if (involved.GroupBy(id => id).FirstOrDefault(roles => roles.Count() > 1) is { } twice)
        {
            return Skip(SkipReason.TwoRoles, twice.Key);
        }

        var story = new Story(Act, PersonOrThing.Of(Target));
        foreach (PersonId id in involved)
        {
            Person? person = id == Newcomer?.Id ? Newcomer : world.People.FirstOrDefault(person => person.Id == id);
            SkipReason? why = person switch
            {
                null => SkipReason.NotInWorld,
                { IsDead: true } => SkipReason.Dead,
                _ when person.Place != Place => SkipReason.Elsewhere,
                _ when person.Beliefs.Any(belief => belief.Claim.Story == story) => SkipReason.AlreadyHoldsStory,
                _ => null,
            };
            if (why is { } reason)
            {
                return Skip(reason, id, reason == SkipReason.Elsewhere ? person!.Place : null);
            }
        }

        return null;

        EventSkipped Skip(SkipReason reason, PersonId? person = null, ThingId? personPlace = null) =>
            new(world.Tick, Act, Actor, Target, Place, Tick, reason, person, personPlace);
    }

    private static void Believe(World world, Person holder, Claim claim, double confidence) =>
        holder.Believe(claim, confidence, Importance.Judge(world, holder, claim));
}
