using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// What one conversation decided and changed, recorded only while the world traces conversations. A choice that
/// wasn't made is null, never a made-up number.
/// </summary>
public sealed class ConversationTrace
{
    internal ConversationTrace(long startTick, PersonSnapshot tellerBefore, Roll talk)
    {
        StartTick = startTick;
        TellerBefore = tellerBefore;
        Talk = talk;
    }

    public long StartTick { get; }
    public long EndTick { get; internal set; }

    /// <summary>The index in the log of the line the conversation wrote as it ended.</summary>
    public int LogLineIndex { get; internal set; }

    /// <summary>The teller as they were when they chose to talk.</summary>
    public PersonSnapshot TellerBefore { get; }

    /// <summary>The listener as they were when picked, before any introduction.</summary>
    public PersonSnapshot ListenerBefore { get; internal set; } = null!;

    public PersonSnapshot TellerAfter { get; internal set; } = null!;
    public PersonSnapshot ListenerAfter { get; internal set; } = null!;

    /// <summary>"Talk now".</summary>
    public Roll Talk { get; }

    /// <summary>"Approach a stranger"; null when no stranger was there.</summary>
    public Roll? ApproachStranger { get; internal set; }

    /// <summary>The known people the listener was drawn from; empty when a stranger was approached.</summary>
    public IReadOnlyList<ListenerOption> KnownListeners { get; internal set; } = [];

    /// <summary>The roll that drew the listener from <see cref="KnownListeners"/>; null when a stranger was approached.</summary>
    public double? ListenerRoll { get; internal set; }

    /// <summary>Each belief the teller could tell this listener, and the roll that picked one.</summary>
    public IReadOnlyList<TopicOption> Topics { get; internal set; } = [];
    public double TopicRoll { get; internal set; }

    /// <summary>The claim as the teller held it, and as they told it.</summary>
    public Claim Held { get; internal set; } = null!;
    public Claim Told { get; internal set; } = null!;

    /// <summary>How the teller colored <see cref="Held"/> into <see cref="Told"/>: each part that moved a dial.</summary>
    public Coloring Coloring { get; internal set; } = null!;

    /// <summary>"Color toward harsher blame"; null when nobody can be blamed.</summary>
    public Roll? HarsherBlame { get; internal set; }

    /// <summary>"Claim I saw it myself"; null when the teller saw it.</summary>
    public Roll? ClaimWitnessed { get; internal set; }

    /// <summary>"Keep the witness"; null when the teller saw it, claimed to, or knew of no witness.</summary>
    public Roll? KeepWitness { get; internal set; }

    /// <summary>"Believe the teller"; null when the listener already held the story.</summary>
    public Roll? Believe { get; internal set; }

    /// <summary>
    /// The confidence the telling gave the listener: what a listener new to the story takes, or would have taken, and
    /// what one who holds it weighs their own against.
    /// </summary>
    public ListenerConfidence Offered { get; internal set; }

    /// <summary>What the listener made of it.</summary>
    public Hearing Hearing { get; internal set; }
}
