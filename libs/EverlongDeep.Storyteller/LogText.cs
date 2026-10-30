using System.Diagnostics;
using System.Globalization;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.Events;
using EverlongDeep.Simulation.People;

namespace EverlongDeep.Storyteller;

/// <summary>
/// Words the simulation's records as the log's plain template lines, each with its time first: "27:05:12 Mara tells
/// Pell …". The <see cref="ConversationEnded"/> line tells a conversation; <see cref="ConversationStarted"/> and
/// <see cref="ConversationCutShort"/> print nothing.
/// </summary>
public static class LogText
{
    public static IEnumerable<string> Lines(World world, LogRecord record)
    {
        IEnumerable<string> texts = record switch
        {
            WorldBuilt built when built.People != built.PeopleAsked =>
                [$"Seed: {built.Seed}", Invariant($"People: {built.PeopleAsked} asked, clamped to {built.People}")],
            WorldBuilt built => [$"Seed: {built.Seed}"],
            PastEvent past =>
                [Invariant($"Past: {ClaimText.Describe(world, past.Claim)} (severity {past.Claim.Severity:0.00}, blame {past.Claim.Blame:0.00}), ") +
                    $"seen by {string.Join(", ", past.Witnesses.Select(id => Name(world, id)))}"],
            EventHappened happened => [Happened(world, happened)],
            ConversationStarted or ConversationCutShort => [],
            ConversationEnded ended => [Ended(world, ended)],
            BecameMemory memory =>
                [Invariant($"{Name(world, memory.Holder)} no longer talks of it, a memory: {ClaimText.Describe(world, memory.Claim)} ") +
                    Invariant($"(importance {memory.Importance:0.00}, confidence {memory.Confidence:0.00})")],
            TieChanged tie =>
                [Invariant($"{Name(world, tie.Holder)} {(tie.Formed ? "now counts" : "no longer counts")} {Name(world, tie.Other)} ") +
                    Invariant($"as a {tie.Tie.ToString().ToLowerInvariant()} (opinion {tie.Opinion:0.00}, trust {tie.Trust:0.00})")],
            EventSkipped skipped =>
                [Invariant($"Event skipped, {skipped.Act} by p{skipped.Actor.Value} of p{skipped.Target.Value} at t{skipped.Place.Value}, ") +
                    Invariant($"tick {skipped.EventTick}: {Why(skipped)}")],
            EventDropped dropped => [Invariant($"Event dropped, {dropped.Act} of p{dropped.Target.Value}: {Why(dropped)}")],
            NegativeStep step => [Invariant($"Time can't step {step.Ticks} ticks; the clock stays")],
            TimeWentBack back => [Invariant($"Time can't go back to tick {back.ToTick}; the clock stays")],
            _ => [Unworded(record)],
        };
        return texts.Select(text => $"{Clock.At(record.Tick)} {text}");
    }

    private static string Happened(World world, EventHappened happened)
    {
        Claim seen = happened.Seen;
        string entered = happened.Newcomer is { } newcomer ? $"{Name(world, newcomer)} came into {world.Thing(seen.Place).Name}. " : "";
        return Invariant($"{entered}{ClaimText.Describe(world, seen)} (severity {seen.Severity:0.00}, blame {seen.Blame:0.00}), ") +
            $"seen by {Names(world, happened.Saw)}, only the commotion caught by {Names(world, happened.CaughtCommotion)}";
    }

    private static string Ended(World world, ConversationEnded ended)
    {
        Claim told = ended.Told;
        string met = ended.IsStranger ? "a stranger met, " : "";
        string source = told.Witness is { } witness ? $"{Name(world, witness)} {ClaimText.WitnessVerb(told)} it" : "hearsay";
        return Invariant($"{Name(world, ended.Teller)} tells {Name(world, ended.Listener)} ") +
            Invariant($"({ended.Tick - ended.StartTick} ticks; {met}opinion {ended.Opinion:0.00}, trust {ended.Trust:0.00}): ") +
            Invariant($"{ClaimText.Describe(world, told)}, {source} ") +
            Invariant($"(confidence {ended.Confidence:0.00}, severity {told.Severity:0.00}, blame {told.Blame:0.00}); ") +
            Invariant($"why: relevance {ended.Relevance:0.00}; pulls {Labels(ended.Pulls)}; {Outcome(world, ended)}");
    }

    private static string Outcome(World world, ConversationEnded ended)
    {
        string listener = Name(world, ended.Listener);
        string teller = Name(world, ended.Teller);
        return ended.Outcome switch
        {
            HearingOutcome.NotBelieved not => $"{listener} doesn't believe it ({Labels(not.Pulls)})",
            HearingOutcome.Believed believed =>
                Invariant($"{listener} believes it{(believed.IsMemory ? ", a memory at once" : "")} (confidence {believed.Confidence:0.00}; {Labels(believed.Pulls)})"),
            HearingOutcome.SameTeller => $"{listener} heard it from {teller} before",
            HearingOutcome.KeptOwn => $"{listener} keeps their own, having seen it",
            HearingOutcome.Surer surer => Invariant($"{listener} is surer (confidence {surer.Before:0.00} → {surer.After:0.00})"),
            HearingOutcome.ThinksLying lying =>
                Invariant($"{listener} thinks {teller} is lying (confidence {lying.Confidence:0.00}; trust {lying.TrustBefore:0.00} → {lying.TrustAfter:0.00})"),
            HearingOutcome.TookTellers took => Invariant($"{listener} takes {teller}'s version (confidence {took.Confidence:0.00})"),
            _ => Unworded(ended.Outcome),
        };
    }

    private static string Why(EventSkipped skipped) => skipped.Reason switch
    {
        SkipReason.ClockElsewhere => $"the clock is at tick {skipped.Tick}",
        SkipReason.NotAPlace => $"thing {skipped.Place.Value} is not a place",
        SkipReason.NewcomerNotLast => $"newcomer p{skipped.Person?.Value} doesn't have the highest id",
        SkipReason.NewcomerElsewhere => $"newcomer p{skipped.Person?.Value} would enter elsewhere",
        SkipReason.NewcomerKnowsSomeone => $"newcomer p{skipped.Person?.Value} already knows someone",
        SkipReason.TwoRoles => $"p{skipped.Person?.Value} has two roles",
        SkipReason.NotInWorld => $"p{skipped.Person?.Value} isn't in the world",
        SkipReason.Dead => $"p{skipped.Person?.Value} is dead",
        SkipReason.Elsewhere => $"p{skipped.Person?.Value} is in t{skipped.PersonPlace?.Value}",
        SkipReason.AlreadyHoldsStory => $"p{skipped.Person?.Value} already holds the story",
        _ => Unworded(skipped.Reason),
    };

    private static string Why(EventDropped dropped) => dropped.Reason switch
    {
        DropReason.Past => $"tick {dropped.EventTick} is past",
        DropReason.NewcomerScheduled => $"newcomer p{dropped.Newcomer?.Value} is already scheduled to enter",
        _ => Unworded(dropped.Reason),
    };

    // The traits that pulled most, as "talkative, dishonest".
    private static string Labels(ValueList<TraitValue> pulls) => pulls.Count == 0 ? "none" : string.Join(", ", pulls.Select(TraitWords.Label));

    private static string Names(World world, ValueList<PersonId> people) =>
        people.Count == 0 ? "nobody" : string.Join(", ", people.Select(id => Name(world, id)));

    private static string Name(World world, PersonId id) => world.Person(id).Name;

    // Something the simulation records that this has no words for yet: a Debug build fails, and a run prints it raw
    // rather than losing the rest of its log.
    private static string Unworded(object data)
    {
        Debug.Fail($"No words for {data}");
        return $"{data}";
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
