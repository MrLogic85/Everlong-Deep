using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>The seeded past held <paramref name="Claim"/>, seen by <paramref name="Witnesses"/>; its tick is the claim's.</summary>
public sealed record PastEvent(long Tick, Claim Claim, ValueList<PersonId> Witnesses) : LogRecord(Tick);

/// <summary><paramref name="Holder"/> no longer talks of <paramref name="Claim"/>: it faded into a memory.</summary>
public sealed record BecameMemory(long Tick, PersonId Holder, Claim Claim, double Importance, double Confidence) : LogRecord(Tick);

/// <summary><paramref name="Teller"/> began telling <paramref name="Listener"/>; nobody hears it before it ends.</summary>
public sealed record ConversationStarted(long Tick, PersonId Teller, PersonId Listener) : LogRecord(Tick);

/// <summary>
/// The conversation <paramref name="Teller"/> began with <paramref name="Listener"/> at <paramref name="StartTick"/> was
/// cut short by a death: it reached its end with nothing heard, and both were free again.
/// </summary>
public sealed record ConversationCutShort(long Tick, PersonId Teller, PersonId Listener, long StartTick) : LogRecord(Tick);

/// <summary>
/// <paramref name="Listener"/> heard out what <paramref name="Teller"/> <paramref name="Told"/> in the conversation begun
/// at <paramref name="StartTick"/>, met in it when <paramref name="IsStranger"/>. <paramref name="Opinion"/> and
/// <paramref name="Trust"/> are the teller's of the listener, <paramref name="Confidence"/> and
/// <paramref name="Relevance"/> are the story's to the teller, and <paramref name="Pulls"/> are the teller's traits that
/// pulled most across their choices, either way.
/// </summary>
public sealed record ConversationEnded(
    long Tick,
    PersonId Teller,
    PersonId Listener,
    long StartTick,
    bool IsStranger,
    double Opinion,
    double Trust,
    Claim Told,
    double Confidence,
    double Relevance,
    ValueList<TraitValue> Pulls,
    HearingOutcome Outcome) : LogRecord(Tick);
