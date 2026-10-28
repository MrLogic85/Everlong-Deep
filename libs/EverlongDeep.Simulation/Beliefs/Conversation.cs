using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>A telling under way: <see cref="Told"/> is what the teller said, heard out when the conversation ends.</summary>
internal sealed record Conversation(
    Person Teller,
    Person Listener,
    Belief Topic,
    Claim Told,
    bool IsStranger,
    int StartMinute,
    Dictionary<TraitAxis, double> Pulls);
