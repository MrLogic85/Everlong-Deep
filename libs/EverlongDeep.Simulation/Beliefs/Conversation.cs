using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// A telling under way: <see cref="Told"/> is what the teller said, heard out when the conversation ends.
/// <see cref="Retelling"/> is decided as the topic is picked, since the teller's told-to entry changes as it ends.
/// </summary>
internal sealed record Conversation(
    Person Teller,
    Person Listener,
    Belief Topic,
    Claim Told,
    HearingOutcome.Retold? Retelling,
    bool IsStranger,
    long StartTick,
    Dictionary<TraitAxis, double> Pulls,
    ConversationTrace? Trace);
