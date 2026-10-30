using System.Globalization;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using static EverlongDeep.Storyteller.TraceNames;

namespace EverlongDeep.Storyteller;

/// <summary>A conversation's trace in sentences, each person by id and name.</summary>
public static class TraceProse
{
    /// <summary>
    /// What the conversation was, in sentences: who told whom what, how the told version differs from the teller's, and
    /// what the listener made of it, with the numbers the rules compared. Never a pronoun the simulation doesn't record.
    /// </summary>
    public static string Of(World world, ConversationTrace trace)
    {
        string teller = Named(world, trace.TellerBefore.Id);
        string listener = Named(world, trace.ListenerBefore.Id);
        string tie = trace.TellerBefore.RelationTo(trace.ListenerBefore.Id) switch
        {
            null => ", a stranger,",
            { Ties: Ties.None } => "",
            { Ties: var ties } => $", their {string.Join(" and ", ties.ToString().ToLowerInvariant().Split(", "))},",
        };
        string told = $"{teller} tells {listener}{tie} that {Describe(world, trace.Told)}.";
        return $"{told} {Source(world, trace)}{Dials(trace)}. {Heard(world, trace)}";
    }

    // How the teller knows it, and the witness they tell it with: "p1 Bram heard it from p5 Fen and tells it as hearsay".
    private static string Source(World world, ConversationTrace trace)
    {
        string teller = Named(world, trace.TellerBefore.Id);
        string knows = trace.Held.Witness == trace.TellerBefore.Id
            ? $"{teller} {ClaimText.WitnessVerb(trace.Held)} it"
            : trace.TellerBefore.BeliefIn(trace.Held.Story) is { HeardFrom.Count: > 0 } held
                ? $"{teller} heard it from {string.Join(" and ", held.HeardFrom.Select(entry => Named(world, entry.From)))}"
                : $"{teller} heard it";
        string tells = trace.Told.Witness switch
        {
            null => "tells it as hearsay",
            { } witness when witness == trace.Held.Witness && witness == trace.TellerBefore.Id => "says so",
            _ => $"says {Witness(world, trace.Told)}",
        };
        return $"{knows} and {tells}";
    }

    // The dials the teller told otherwise than they hold them: ", with blame 0.37 → 0.57".
    private static string Dials(ConversationTrace trace)
    {
        string[] moved =
        [
            .. new[] { (Name: "severity", Held: trace.Held.Severity, Told: trace.Told.Severity), (Name: "blame", Held: trace.Held.Blame, Told: trace.Told.Blame) }
                .Where(dial => dial.Held != dial.Told)
                .Select(dial => Invariant($"{dial.Name} {dial.Held:0.00} → {dial.Told:0.00}")),
        ];
        return moved.Length == 0 ? "" : $", with {string.Join(" and ", moved)}";
    }

    // What the listener made of it, from the numbers the rule compared.
    private static string Heard(World world, ConversationTrace trace)
    {
        string teller = Named(world, trace.TellerBefore.Id);
        string listener = Named(world, trace.ListenerBefore.Id);
        ListenerConfidence offered = trace.Offered;
        string confidence = Invariant($"{offered.Value:0.00} ({teller}'s {offered.Teller:0.00} × trust {offered.Trust:0.00})");
        BeliefSnapshot? before = trace.ListenerBefore.BeliefIn(trace.Told.Story);
        BeliefSnapshot? after = trace.ListenerAfter.BeliefIn(trace.Told.Story);
        switch (trace.Hearing)
        {
            case Hearing.Believed or Hearing.NotBelieved:
                Roll believe = trace.Believe!.Value;
                string odds = string.Create(CultureInfo.InvariantCulture,
                    $"{listener} didn't hold this story and trusts {teller} {offered.Trust:0.00}, so believing it was " +
                    $"{(believe.Chance >= 0.5 ? "likely" : "unlikely")} ({believe.Chance:0.00}); the roll of {believe.Value:0.00}");
                return believe.Yes
                    ? $"{odds} made {listener} believe it, with confidence {confidence}."
                    : $"{odds} left {listener} not believing it, so {listener} holds nothing; believed, it would have had confidence {confidence}.";
            case Hearing.Retold:
                string retold = trace.Ended.Outcome is HearingOutcome.Retold { ToSource: true }
                    ? $"{teller} heard this story from {listener}"
                    : $"{teller} told {listener} this before, as {teller} holds it now";
                return $"{retold}, so it is a retelling, and for now a retelling changes nothing for either.";
            case Hearing.SameTeller:
                return $"{listener} already holds this story and heard it from {teller} before, so it changes nothing.";
            case Hearing.KeptOwn:
                return $"{listener} {ClaimText.WitnessVerb(before!.Claim)} it, so {listener} keeps their own version.";
            case Hearing.Surer:
                return string.Create(CultureInfo.InvariantCulture,
                    $"{listener} already holds the same story, not heard from {teller}, and a new teller makes {listener} surer: " +
                    $"confidence {before!.Confidence:0.00} → {after!.Confidence:0.00} and relevance {before.Relevance:0.00} → {after.Relevance:0.00}, " +
                    $"from a telling worth {confidence}.");
            case Hearing.TookTellers:
                return string.Create(CultureInfo.InvariantCulture,
                    $"{listener} held another version, {Describe(world, before!.Claim)}, with confidence {before.Confidence:0.00}, " +
                    $"below the {confidence} from this telling, so {listener} takes {teller}'s version.");
            case Hearing.ThinksLying:
                string own = before!.Claim.Witness == trace.ListenerBefore.Id
                    ? $"and {ClaimText.WitnessVerb(before.Claim)} it"
                    : Invariant($"with confidence {before.Confidence:0.00}, not below the {confidence} from this telling");
                double trustAfter = trace.ListenerAfter.RelationTo(trace.TellerBefore.Id)!.Trust;
                return string.Create(CultureInfo.InvariantCulture,
                    $"{listener} held another version, {Describe(world, before.Claim)}, {own}, so {listener} thinks {teller} is lying " +
                    $"and trusts {teller} less: {offered.Trust:0.00} → {trustAfter:0.00}.");
            default:
                throw new ArgumentOutOfRangeException(nameof(trace), trace.Hearing, null);
        }
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
