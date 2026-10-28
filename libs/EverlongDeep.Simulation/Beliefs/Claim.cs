using System.Diagnostics;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// What someone believes happened, as <c>docs/08 Social Model.md</c> → "Claim" defines. Nothing in it refers to
/// what really happened: the truth is never stored.
/// </summary>
public sealed record Claim
{
    /// <summary>The range of blame for a claim that <see cref="HasBlame"/>; any other claim's blame is 0.</summary>
    public static readonly (double Min, double Max) HarmfulBlame = (-0.2, 1);

    public Claim(
        PersonOrThing actor, Act act, PersonOrThing target, ThingId place, int hour, double severity, double blame,
        PersonId? witness)
    {
        Debug.Assert(!target.IsUnknown, "A claim's target is a person or a thing");
        Debug.Assert(severity is >= 0 and <= 1, "Severity is 0 to 1");
        Debug.Assert(blame is >= -1 and <= 1, "Blame is −1 to 1");

        Actor = actor;
        Act = act;
        Target = target;
        Place = place;
        Hour = hour;
        Severity = Math.Clamp(severity, 0, 1);
        Blame = Math.Clamp(blame, -1, 1);
        Witness = witness;
    }

    public PersonOrThing Actor { get; }
    public Act Act { get; }
    public PersonOrThing Target { get; }
    public ThingId Place { get; }

    /// <summary>When it happened, in simulation hours; before the run began is below 0.</summary>
    public int Hour { get; }

    /// <summary>How bad it was, 0 (harmless) to 1 (atrocious).</summary>
    public double Severity { get; }

    /// <summary>Whose fault it was, −1 (fully justified) to 1 (fully the actor's fault).</summary>
    public double Blame { get; }

    /// <summary>Who claims to have seen it, or null when unknown. The holder is first-hand when they are the witness.</summary>
    public PersonId? Witness { get; }

    /// <summary>Whether <paramref name="act"/> does harm, so its blame can be other than 0.</summary>
    public static bool IsHarmful(Act act) => act is not (Act.Helped or Act.WasSeenAt);

    /// <summary>Whether someone can be blamed: the act does harm and the actor is known.</summary>
    public bool HasBlame => IsHarmful(Act) && !Actor.IsUnknown;

    /// <summary>Whether it is <paramref name="person"/>'s own story: they are its actor, target or witness.</summary>
    public bool Involves(PersonId person)
    {
        PersonOrThing who = PersonOrThing.Of(person);
        return Actor == who || Target == who || Witness == person;
    }

    /// <summary>The same claim told with other dials.</summary>
    public Claim WithDials(double severity, double blame) => new(Actor, Act, Target, Place, Hour, severity, blame, Witness);

    /// <summary>The same claim told with another witness, or with none.</summary>
    public Claim WithWitness(PersonId? witness) => new(Actor, Act, Target, Place, Hour, Severity, Blame, witness);
}
