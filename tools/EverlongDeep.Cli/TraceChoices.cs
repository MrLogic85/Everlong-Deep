using System.Diagnostics;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Storyteller;
using static EverlongDeep.Storyteller.TraceNames;

namespace EverlongDeep.Cli;

/// <summary>One choice of a conversation, as the Choices table and the JSON line both show it.</summary>
internal abstract record TraceChoice(string Name);

/// <summary>A yes-or-no choice, with its roll, or why it wasn't rolled.</summary>
internal sealed record RolledChoice(string Name, Roll? Roll, string? NotRolled) : TraceChoice(Name);

/// <summary>A known person the teller could have told; the roll shows on the one picked.</summary>
internal sealed record ListenerChoice(string Person, ListenerOption Option, double? Roll, bool Picked) : TraceChoice("Listener");

/// <summary>A stranger the teller approached, picked at random, so with no score.</summary>
internal sealed record StrangerChoice(string Person) : TraceChoice("Listener")
{
    public const string Why = "a stranger, picked at random";
}

/// <summary>A belief the teller could have told; the roll shows on the one picked.</summary>
internal sealed record TopicChoice(string Story, TopicOption Option, double? Roll, bool Picked) : TraceChoice("Topic");

/// <summary>Every choice of a conversation, in the order it was made.</summary>
internal static class TraceChoices
{
    public static IEnumerable<TraceChoice> Of(World world, ConversationTrace trace)
    {
        PersonSnapshot teller = trace.TellerBefore;
        PersonSnapshot listener = trace.ListenerBefore;
        yield return new RolledChoice("Talk now", trace.Talk, null);
        yield return new RolledChoice("Approach a stranger", trace.ApproachStranger, "no stranger here");
        if (trace.KnownListeners.Count == 0)
        {
            yield return new StrangerChoice(Named(world, listener.Id));
        }

        foreach (ListenerOption option in trace.KnownListeners)
        {
            bool picked = option.Person == listener.Id;
            yield return new ListenerChoice(Named(world, option.Person), option, picked ? trace.ListenerRoll : null, picked);
        }

        foreach (TopicOption option in trace.Topics)
        {
            bool picked = ReferenceEquals(option.Belief.Claim, trace.Held);
            yield return new TopicChoice(Describe(world, option.Belief.Claim), option, picked ? trace.TopicRoll : null, picked);
        }

        yield return new RolledChoice("Color toward harsher blame", trace.HarsherBlame, "nobody to blame");
        yield return new RolledChoice("Claim I saw it myself", trace.ClaimWitnessed, $"{Named(world, teller.Id)} saw it");
        string noKeep = trace.Told.Witness == teller.Id ? $"{Named(world, teller.Id)} is the witness" : "no witness to keep";
        yield return new RolledChoice("Keep the witness", trace.KeepWitness, noKeep);
        Debug.Assert(trace.KeepDetails.Count == trace.Held.Details.Count, "One keep roll per detail held");
        foreach ((Detail detail, Roll kept) in trace.Held.Details.Zip(trace.KeepDetails))
        {
            yield return new RolledChoice($"Keep \"{ClaimText.Phrase(detail)}\"", kept, null);
        }

        string? notRolled = trace switch
        {
            { Believe: not null } => null,
            { Hearing: Hearing.Retold } => "a retelling, nothing new for the listener",
            _ => $"already holds this story and {TraceText.Weighed(world, trace)}",
        };
        yield return new RolledChoice("Believe the teller", trace.Believe, notRolled);
    }
}
