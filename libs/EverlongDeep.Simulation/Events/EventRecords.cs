using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Events;

/// <summary>
/// An event happened as <paramref name="Seen"/> says, at its place, after <paramref name="Newcomer"/>, if any, came in.
/// <paramref name="Saw"/> saw it, and <paramref name="CaughtCommotion"/> only caught the commotion.
/// </summary>
public sealed record EventHappened(long Tick, Claim Seen, PersonId? Newcomer, ValueList<PersonId> Saw, ValueList<PersonId> CaughtCommotion)
    : LogRecord(Tick);

/// <summary>
/// An event of <paramref name="Act"/> was scheduled to happen at <paramref name="EventTick"/>, with the values it was
/// given; <paramref name="Newcomer"/> is who enters as it happens, if anyone.
/// </summary>
public sealed record EventScheduled(
    long Tick,
    Act Act,
    PersonId Actor,
    PersonId Target,
    ThingId Place,
    long EventTick,
    ValueList<PersonId> KeyWitnesses,
    ValueList<PersonId> Bystanders,
    PersonId? Newcomer) : LogRecord(Tick);

/// <summary>
/// The event due at <paramref name="EventTick"/> couldn't happen in the world as it was, for <paramref name="Reason"/>;
/// <paramref name="Person"/> is whom the reason names, if anyone, and <paramref name="PersonPlace"/>, for Elsewhere,
/// where they were.
/// </summary>
public sealed record EventSkipped(
    long Tick,
    Act Act,
    PersonId Actor,
    PersonId Target,
    ThingId Place,
    long EventTick,
    SkipReason Reason,
    PersonId? Person = null,
    ThingId? PersonPlace = null) : Problem(Tick);

/// <summary>Why an event couldn't happen.</summary>
public enum SkipReason
{
    /// <summary>The clock wasn't at the event's tick.</summary>
    ClockElsewhere,

    /// <summary>Its place can't hold people.</summary>
    NotAPlace,

    /// <summary>The newcomer's id isn't above everyone's.</summary>
    NewcomerNotLast,

    /// <summary>The newcomer would enter elsewhere.</summary>
    NewcomerElsewhere,

    TwoRoles,
    NotInWorld,
    Dead,
    Elsewhere,

    /// <summary>Someone in a role already holds the story.</summary>
    AlreadyHoldsStory,
}

/// <summary>
/// An event of <paramref name="Act"/> to <paramref name="Target"/> was never scheduled, for <paramref name="Reason"/>;
/// <paramref name="Newcomer"/>, for NewcomerScheduled, is the newcomer already scheduled to enter.
/// </summary>
public sealed record EventDropped(long Tick, Act Act, PersonId Target, long EventTick, DropReason Reason, PersonId? Newcomer = null)
    : Problem(Tick);

/// <summary>Why an event was never scheduled.</summary>
public enum DropReason
{
    /// <summary>Its tick was past.</summary>
    Past,

    /// <summary>Its newcomer was already scheduled to enter.</summary>
    NewcomerScheduled,
}
