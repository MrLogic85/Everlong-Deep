using System.Diagnostics;
using System.Globalization;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using static EverlongDeep.Storyteller.TraceNames;

namespace EverlongDeep.Storyteller;

/// <summary>
/// A conversation's trace in sentences: each person by id and name at first in a sentence, and after it by their own
/// pronoun when no one else in the sentence shares it.
/// </summary>
public static class TraceProse
{
    /// <summary>
    /// What the conversation was, in sentences: who told whom what, how the told version differs from the teller's, and
    /// what the listener made of it, with the numbers the rules compared.
    /// </summary>
    public static string Of(World world, ConversationTrace trace)
    {
        PersonId teller = trace.TellerBefore.Id;
        PersonId listener = trace.ListenerBefore.Id;
        var sentence = new Sentence(world, [teller, listener, .. Sentence.People(trace.Told)]);
        string told = $"{sentence.Does(teller, "tells", "tell")} {sentence.Whom(listener)}";
        // The tie is the teller's possessive and the listener's role, even when both share a pronoun: "p4 Jory, his brother,".
        told += trace.TellerBefore.RelationTo(listener) switch
        {
            null => ", a stranger,",
            { Ties: Ties.None } => "",
            { Ties: var ties } => $", {Pronouns.Of(world.Person(teller).Gender).Possessive} {Tie(ties, world.Person(listener).Gender)},",
        };
        told += $" that {sentence.Story(trace.Told)}.";
        return $"{told} {Source(world, trace)}{Dials(trace)}{Dropped(trace)}. {Heard(world, trace)}";
    }

    // What the other is to the holder, in the other's gender for the family ties: "sister and rival".
    private static string Tie(Ties ties, Gender gender) => string.Join(" and ", Enum.GetValues<Ties>()
        .Where(tie => tie != Ties.None && ties.HasFlag(tie))
        .Select(tie => (tie, gender) switch
        {
            (Ties.Parent, Gender.She) => "mother",
            (Ties.Parent, Gender.He) => "father",
            (Ties.Child, Gender.She) => "daughter",
            (Ties.Child, Gender.He) => "son",
            (Ties.Sibling, Gender.She) => "sister",
            (Ties.Sibling, Gender.He) => "brother",
            (Ties.Spouse, Gender.She) => "wife",
            (Ties.Spouse, Gender.He) => "husband",
            _ => tie.ToString().ToLowerInvariant(),
        }));

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

    // The details the teller held but left out: ", leaving out "with a knife"".
    private static string Dropped(ConversationTrace trace)
    {
        string[] dropped = [.. trace.Held.Details.Except(trace.Told.Details).Select(detail => $"\"{ClaimText.Phrase(detail)}\"")];
        return dropped.Length == 0 ? "" : $", leaving out {string.Join(" and ", dropped)}";
    }

    // What the listener made of it, from the numbers the rule compared, in one sentence.
    private static string Heard(World world, ConversationTrace trace)
    {
        PersonId teller = trace.TellerBefore.Id;
        PersonId listener = trace.ListenerBefore.Id;
        ListenerConfidence offered = trace.Offered;
        BeliefSnapshot? before = trace.ListenerBefore.BeliefIn(trace.Told.Story);
        BeliefSnapshot? after = trace.ListenerAfter.BeliefIn(trace.Told.Story);
        var sentence = new Sentence(world, [teller, listener, .. before is null ? [] : Sentence.People(before.Claim)]);
        string Confidence() =>
            Invariant($"{offered.Value:0.00} ({sentence.Whose(teller)} {offered.Teller:0.00} × trust {offered.Trust:0.00})");
        switch (trace.Hearing)
        {
            case Hearing.Believed or Hearing.NotBelieved:
                Roll believe = trace.Believe!.Value;
                string odds = string.Create(CultureInfo.InvariantCulture,
                    $"{sentence.Does(listener, "didn't hold")} this story and trusts {sentence.Whom(teller)} {offered.Trust:0.00}, " +
                    $"so believing it was {(believe.Chance >= 0.5 ? "likely" : "unlikely")} ({believe.Chance:0.00}); the roll of {believe.Value:0.00}");
                return believe.Yes
                    ? $"{odds} made {sentence.Whom(listener)} believe it, with confidence {Confidence()}."
                    : $"{odds} left {sentence.Whom(listener)} not believing it, so {sentence.Does(listener, "holds", "hold")} nothing; " +
                      $"believed, it would have had confidence {Confidence()}.";
            case Hearing.Retold:
                string retold = trace.Ended.Outcome is HearingOutcome.Retold { ToSource: true }
                    ? $"{sentence.Does(teller, "heard")} this story from {sentence.Whom(listener)}"
                    : $"{sentence.Does(teller, "told")} {sentence.Whom(listener)} this before, as {sentence.Does(teller, "holds", "hold")} it now";
                return $"{retold}, so it is a retelling, and for now a retelling changes nothing for either.";
            case Hearing.SameTeller:
                return $"{sentence.Does(listener, "already holds", "already hold")} this story and heard it from {sentence.Whom(teller)} before, " +
                    "so it changes nothing.";
            case Hearing.KeptOwn:
                // The teller isn't named here, so the listener's pronoun can't be taken for theirs.
                var alone = new Sentence(world, [listener]);
                string witnessed = ClaimText.WitnessVerb(before!.Claim);
                return $"{alone.Does(listener, witnessed)} it, so {alone.Does(listener, "keeps", "keep")} {alone.Whose(listener)} own version.";
            case Hearing.Surer:
                return string.Create(CultureInfo.InvariantCulture,
                    $"{sentence.Does(listener, "already holds", "already hold")} the same story, not heard from {sentence.Whom(teller)}, " +
                    $"and a new teller makes {sentence.Whom(listener)} surer: " +
                    $"confidence {before!.Confidence:0.00} → {after!.Confidence:0.00} and relevance {before.Relevance:0.00} → {after.Relevance:0.00}, " +
                    $"from a telling worth {Confidence()}.");
            case Hearing.TookTellers:
                return string.Create(CultureInfo.InvariantCulture,
                    $"{sentence.Does(listener, "held")} another version, {sentence.Story(before!.Claim)}, with confidence {before.Confidence:0.00}, " +
                    $"below the {Confidence()} from this telling, so {sentence.Does(listener, "takes", "take")} {sentence.Whose(teller)} version.");
            case Hearing.ThinksLying:
                string held = $"{sentence.Does(listener, "held")} another version, {sentence.Story(before!.Claim)}";
                string own = before.Claim.Witness == listener
                    ? $"and {ClaimText.WitnessVerb(before.Claim)} it"
                    : Invariant($"with confidence {before.Confidence:0.00}, not below the {Confidence()} from this telling");
                double trustAfter = trace.ListenerAfter.RelationTo(teller)!.Trust;
                string thinks = sentence.Does(listener, "thinks", "think");
                string lying = sentence.Does(teller, "is lying", "are lying");
                return string.Create(CultureInfo.InvariantCulture,
                    $"{held}, {own}, so {thinks} {lying} and {sentence.Verb(listener, "trusts", "trust")} {sentence.Whom(teller)} less: " +
                    $"{offered.Trust:0.00} → {trustAfter:0.00}.");
            default:
                throw new ArgumentOutOfRangeException(nameof(trace), trace.Hearing, null);
        }
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The people of one sentence, each written by id and name at their first mention, and after it by their pronoun
    /// when no one else in the sentence shares it, so a pronoun is never in doubt; otherwise by id and name again. Each
    /// call is a mention, so a sentence is built in the order it reads.
    /// </summary>
    private sealed class Sentence(World world, IEnumerable<PersonId> people)
    {
        private readonly PersonId[] _people = [.. people.Distinct()];
        private readonly HashSet<PersonId> _mentioned = [];

        // The people last written by a plural pronoun, so the verbs after them agree.
        private readonly HashSet<PersonId> _plural = [];

        /// <summary>The people a claim's sentence names: its actor and target.</summary>
        public static IEnumerable<PersonId> People(Claim claim) =>
            new[] { claim.Actor.Person, claim.Target.Person }.OfType<PersonId>();

        /// <summary>
        /// The person and their verb: "p1 Bram trusts", "he trusts" or "they trust"; a verb with no plural of its own,
        /// such as "held", stays as it is.
        /// </summary>
        public string Does(PersonId person, string singular, string? plural = null)
        {
            string who = Pronoun(person)?.Subject ?? Named(world, person);
            return $"{who} {Verb(person, singular, plural)}";
        }

        /// <summary>
        /// A further verb for the person just written by <see cref="Does"/>, agreeing with it: "and trust" after
        /// "they think".
        /// </summary>
        public string Verb(PersonId person, string singular, string? plural = null) =>
            _plural.Contains(person) ? plural ?? singular : singular;

        /// <summary>The person as an object: "p1 Bram" or "him".</summary>
        public string Whom(PersonId person) => Pronoun(person)?.Object ?? Named(world, person);

        /// <summary>Whose: "p1 Bram's" or "his".</summary>
        public string Whose(PersonId person) => Pronoun(person)?.Possessive ?? $"{Named(world, person)}'s";

        /// <summary>The claim as <see cref="TraceNames.Describe"/> writes it, its people by id and name and mentioned.</summary>
        public string Story(Claim claim) => ClaimText.Describe(claim, who =>
        {
            if (who.Person is { } person)
            {
                Debug.Assert(_people.Contains(person), "A sentence knows its people before it names them");
                _mentioned.Add(person);
                _plural.Remove(person);
            }

            return Named(world, who);
        });

        // The person's pronouns for this mention, or null when it must be their id and name.
        private Pronouns? Pronoun(PersonId person)
        {
            Debug.Assert(_people.Contains(person), "A sentence knows its people before it names them");
            Gender gender = world.Person(person).Gender;
            bool later = !_mentioned.Add(person);
            Pronouns? pronouns = later && _people.All(other => other == person || world.Person(other).Gender != gender)
                ? Pronouns.Of(gender)
                : null;
            if (pronouns is { TakesPluralVerb: true })
            {
                _plural.Add(person);
            }
            else
            {
                _plural.Remove(person);
            }

            return pronouns;
        }
    }
}
