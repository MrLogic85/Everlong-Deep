using System.Globalization;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Storyteller;
using static EverlongDeep.Storyteller.TraceNames;

namespace EverlongDeep.Cli;

/// <summary>
/// The words a conversation's trace is told in, shared by <see cref="TraceTables"/> and <see cref="TraceJson"/>, each
/// person or thing by id and name.
/// </summary>
internal static class TraceText
{
    // How many of a relation's impressions Feelings names, strongest first.
    private const int _strongestImpressions = 3;

    /// <summary>
    /// The scale of every number the trace shows and the roll rule: the name <see cref="TraceJson"/> gives it, the
    /// column <see cref="TraceTables"/> gives it, and what it means.
    /// </summary>
    internal static readonly (string Name, string Column, string Scale)[] Scales =
    [
        ("importance", "Imp", "how much it matters to the holder, 0–1"),
        ("confidence", "Conf", "how sure the holder is, 0–1"),
        ("relevance", "Rel", "how much they want to talk of it, 0–1; 0 is a memory, no longer told"),
        ("severity", "Sev", "how bad the act was, 0–1"),
        ("blame", "Blame", Invariant($"how much the actor is to blame, {Claim.HarmfulBlame.Min:0.0}–{Claim.HarmfulBlame.Max:0}")),
        ("opinion", "Opinion", "how much they like the other, -1–1"),
        ("trust", "Trust", "how much they trust the other, 0–1"),
        ("familiarity", "Familiarity", "how well they know the other, 0–1; 0 is still someone known, as a stranger has none"),
        ("score", "Why", "a score is its context, or a topic's relevance × its weight and a retelling's context, plus each trait's pull, the trait × its weight; a listener's is opinion + trust + familiarity; the higher, the likelier"),
        ("roll", "Roll", "a choice says yes when its roll is below its chance"),
    ];

    /// <summary>Whom Feelings shows <paramref name="from"/>'s feelings toward: the other speaker, then each person the told story names but them.</summary>
    internal static IEnumerable<PersonId> FeltToward(PersonId from, PersonId other, Claim told) =>
        ClaimNames(told).Where(who => who.Person is not null).Select(who => who.Person!.Value)
            .Prepend(other).Distinct().Where(to => to != from);

    internal static IEnumerable<PersonOrThing> ClaimNames(Claim claim) =>
        new[] { claim.Actor, claim.Target }
            .Concat(claim.Witness is { } witness ? [PersonOrThing.Of(witness)] : [])
            .Where(who => !who.IsUnknown);

    /// <summary>What the listener made of it: "p2 Ilse believes it".</summary>
    internal static string Outcome(World world, ConversationTrace trace)
    {
        string listener = Named(world, trace.ListenerBefore.Id);
        return trace.Hearing switch
        {
            Hearing.Believed => $"{listener} believes it",
            Hearing.NotBelieved => $"{listener} doesn't believe it",
            _ => $"{listener} {Weighed(world, trace)}",
        };
    }

    /// <summary>What a listener who already held the story made of it.</summary>
    internal static string Weighed(World world, ConversationTrace trace) => trace.Hearing switch
    {
        Hearing.Retold => "hears a retelling, which changes nothing",
        Hearing.SameTeller => "heard it from them before",
        Hearing.Surer => "is surer",
        Hearing.KeptOwn => "keeps their own, having seen it",
        Hearing.TookTellers => $"takes {Named(world, trace.TellerBefore.Id)}'s version",
        Hearing.ThinksLying => $"thinks {Named(world, trace.TellerBefore.Id)} is lying",
        _ => throw new ArgumentOutOfRangeException(nameof(trace), trace.Hearing, "A new story is believed or not, never weighed"),
    };

    /// <summary>
    /// Why the told version differs from the teller's, a line each: the witness, the teller's reach, and each dial's
    /// parts as the reach × an opinion, or why it didn't move.
    /// </summary>
    internal static IEnumerable<string> StoryNotes(World world, ConversationTrace trace)
    {
        string teller = Named(world, trace.TellerBefore.Id);
        if (trace.Told.Witness != trace.Held.Witness)
        {
            yield return trace.Told.Witness == trace.TellerBefore.Id
                ? $"Witness: {teller} claims to have seen it (\"Claim I saw it myself\" said yes)."
                : $"Witness dropped: {teller} didn't keep it (\"Keep the witness\" said no).";
        }

        Coloring coloring = trace.Coloring;
        yield return Invariant($"{teller}'s reach is {coloring.Reach:0.00}: how far an opinion of 1 or -1 moves severity or blame as they tell it.");

        string severity = OpinionPart(world, trace, trace.Held.Target, coloring.TargetSource, coloring.TargetOpinion, coloring.SeverityPart, against: false);
        yield return $"Severity: {severity}{Result(trace.Held.Severity, trace.Told.Severity, coloring.SeverityCapped)}";

        string blame = OpinionPart(world, trace, trace.Held.Actor, coloring.ActorSource, coloring.ActorOpinion, coloring.BlamePart, against: true);
        if (coloring.ActorSource is OpinionSource.Unknown or OpinionSource.NoHarm)
        {
            yield return $"Blame: unchanged; {blame}, so nobody can be blamed.";
            yield break;
        }

        string[] parts =
        [
            blame,
            coloring.Harsher
                ? Invariant($"harsher {coloring.HarsherPart:+0.00} (\"Color toward harsher blame\" said yes)")
                : "not harsher (\"Color toward harsher blame\" said no)",
        ];
        yield return $"Blame: {string.Join(", ", parts)}{Result(trace.Held.Blame, trace.Told.Blame, coloring.BlameCapped)}";
    }

    // A dial's part as the reach × an opinion of who, "-(…)" when it moves against them, or why it weighs nothing, as
    // the simulation recorded it. A source it doesn't explain throws, so a run that reaches it fails.
    private static string OpinionPart(
        World world, ConversationTrace trace, PersonOrThing who, OpinionSource source, double opinion, double part, bool against) => source switch
        {
            OpinionSource.Known => Invariant(
                $"{trace.Coloring.Reach:0.00} × {(against ? "-(" : "")}opinion of {Named(world, who)} {opinion:0.00}{(against ? ")" : "")} = {part:+0.00;-0.00}"),
            OpinionSource.Thing => $"{Named(world, who)} is {Article(world.Thing(who.Thing!.Value).Kind.ToString().ToLowerInvariant())}, and only an opinion of a person colors it",
            OpinionSource.Self => $"{Named(world, who)} has no opinion of themselves",
            OpinionSource.NeverMet => $"{Named(world, trace.TellerBefore.Id)} doesn't know {Named(world, who)}",
            OpinionSource.Unknown => "nobody knows who did it",
            OpinionSource.NoHarm => $"to have {ClaimText.Verb(trace.Held.Act)} does no harm",
            _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
        };

    private static string Article(string noun) => ("aeiou".Contains(noun[0]) ? "an " : "a ") + noun;

    // A dial's outcome after its parts: ", 0.37 → 0.57", capped at its end, or unchanged.
    private static string Result(double held, double told, bool capped) => (held == told, capped) switch
    {
        (true, true) => Invariant($", capped at {told:0.00}, so unchanged."),
        (true, false) => ", so unchanged.",
        (false, true) => Invariant($", capped: {held:0.00} → {told:0.00}."),
        (false, false) => Invariant($": {held:0.00} → {told:0.00}."),
    };

    /// <summary>What the conversation changed in the listener, then in the teller, a line each; empty when nothing did.</summary>
    internal static IEnumerable<string> Changes(World world, ConversationTrace trace)
    {
        string[] changes = [.. Changes(world, trace.ListenerBefore, trace.ListenerAfter), .. Changes(world, trace.TellerBefore, trace.TellerAfter)];

        // A listener who already held the story may feel no different about anyone; that is said, not left blank.
        PersonSnapshot listener = trace.ListenerBefore;
        return listener.BeliefIn(trace.Told.Story) is not null && !RelationChanges(world, listener, trace.ListenerAfter).Any()
            ? [.. changes, $"{Named(world, listener.Id)} already held this story: nothing in how they feel about anyone changed"]
            : changes;
    }

    /// <summary>
    /// What changed in one person between two snapshots, as lines: only what a conversation can touch, which is
    /// beliefs, plus how each relation moved. The telling itself is the trace's heading, so
    /// the heard-from and told-to entries it adds are left out.
    /// </summary>
    private static IEnumerable<string> Changes(World world, PersonSnapshot before, PersonSnapshot after)
    {
        string holder = Named(world, after.Id);
        foreach (BeliefSnapshot added in after.Beliefs.Skip(before.Beliefs.Count))
        {
            yield return $"{holder} now believes: {Belief(world, added)}";
        }

        foreach (BeliefSnapshot now in after.Beliefs)
        {
            BeliefSnapshot? was = before.Beliefs.FirstOrDefault(belief => belief.Claim.Story == now.Claim.Story);
            foreach (string change in BeliefChanges(world, holder, was, now))
            {
                yield return change;
            }
        }

        foreach (string change in RelationChanges(world, before, after))
        {
            yield return change;
        }
    }

    // How one person's relations moved: someone met, or an opinion, trust, familiarity or impressions changed.
    private static IEnumerable<string> RelationChanges(World world, PersonSnapshot before, PersonSnapshot after)
    {
        string holder = Named(world, after.Id);
        foreach (RelationSnapshot now in after.Relations)
        {
            string other = Named(world, now.Other);
            if (before.RelationTo(now.Other) is not { } was)
            {
                yield return $"{holder} met {other}: {Feeling(now)}";
                continue;
            }

            if (now.Opinion != was.Opinion)
            {
                yield return Invariant($"{holder}'s opinion of {other}: {was.Opinion:0.00} → {now.Opinion:0.00}");
            }

            if (now.Trust != was.Trust)
            {
                yield return Invariant($"{holder}'s trust in {other}: {was.Trust:0.00} → {now.Trust:0.00}");
            }

            if (now.Familiarity != was.Familiarity)
            {
                yield return Invariant($"{holder}'s familiarity with {other}: {was.Familiarity:0.00} → {now.Familiarity:0.00}");
            }

            if (!now.Impressions.OrderBy(pair => pair.Key).SequenceEqual(was.Impressions.OrderBy(pair => pair.Key)))
            {
                yield return $"{holder}'s impressions of {other}: {Impressions(was)} → {Impressions(now)}";
            }
        }
    }

    // How a belief the person already held moved: its version, importance, confidence and relevance.
    private static IEnumerable<string> BeliefChanges(World world, string holder, BeliefSnapshot? was, BeliefSnapshot now)
    {
        if (was is null)
        {
            yield break;
        }

        if (now.Claim != was.Claim)
        {
            yield return $"{holder}'s version is now: {Belief(world, now)}";
        }

        string story = Describe(world, now.Claim);
        if (now.Importance != was.Importance)
        {
            yield return Invariant($"{holder} on {story}: importance {was.Importance:0.00} → {now.Importance:0.00}");
        }

        if (now.Confidence != was.Confidence)
        {
            yield return Invariant($"{holder} on {story}: confidence {was.Confidence:0.00} → {now.Confidence:0.00}");
        }

        if (now.Relevance != was.Relevance)
        {
            yield return Invariant($"{holder} on {story}: relevance {was.Relevance:0.00} → {now.Relevance:0.00}");
        }
    }

    // Opinion, trust, familiarity, ties and the strongest impressions as trait words:
    // "opinion -0.08, trust 0.72, familiarity 0.80, sibling; sees them as generous (+0.84)".
    internal static string Feeling(RelationSnapshot relation)
    {
        string ties = relation.Ties == Ties.None ? "" : $", {relation.Ties.ToString().ToLowerInvariant()}";
        string[] strongest = [.. relation.Impressions
            .Where(pair => pair.Value != 0)
            .OrderByDescending(pair => Math.Abs(pair.Value)).ThenBy(pair => pair.Key)
            .Take(_strongestImpressions)
            .Select(pair => Invariant($"{TraitWords.Label(pair.Key, pair.Value)} ({pair.Value:+0.00;-0.00})"))];
        string seen = strongest.Length == 0 ? "" : $"; sees them as {string.Join(", ", strongest)}";
        return Invariant($"opinion {relation.Opinion:0.00}, trust {relation.Trust:0.00}, familiarity {relation.Familiarity:0.00}{ties}{seen}");
    }

    private static string Impressions(RelationSnapshot relation) =>
        relation.Impressions.Count == 0
            ? "none"
            : string.Join(", ", relation.Impressions.OrderBy(pair => pair.Key).Select(pair => Invariant($"{pair.Key} {pair.Value:+0.00;-0.00;0.00}")));

    // A belief in a line, its numbers named in full, so the line reads alone in the JSON too.
    private static string Belief(World world, BeliefSnapshot belief) =>
        Invariant($"{Describe(world, belief.Claim)}, {Witness(world, belief.Claim)}, at {Clock.At(belief.Claim.Tick)} ") +
        Invariant($"(importance {belief.Importance:0.00}, confidence {belief.Confidence:0.00}, relevance {belief.Relevance:0.00}, ") +
        Invariant($"severity {belief.Claim.Severity:0.00}, blame {belief.Claim.Blame:0.00})");

    internal static string ChoiceName(Choice choice) => choice switch
    {
        Choice.TellOthersBusiness => "others' business",
        Choice.TellScandal => "scandal",
        Choice.TellAgain => "again",
        _ => choice.ToString(),
    };

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
