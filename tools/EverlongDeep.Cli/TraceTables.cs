using System.Globalization;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace EverlongDeep.Cli;

/// <summary>
/// Renders a conversation's trace as the tables <c>docs/MANUAL_TESTING.md</c> → "Running a seeded world" lists, each
/// person or thing by id and name.
/// </summary>
public static class TraceTables
{
    private const string _legendTitle = "Legend";
    private const string _whoTitle = "Who and what";
    private const string _feelingsTitle = "Feelings";
    private const string _beliefsTitle = "Beliefs (before)";
    private const string _choicesTitle = "Choices";
    private const string _storyTitle = "Story";
    private const string _changesTitle = "Changes";

    /// <summary>The width of output redirected to a file: wide enough that no table wraps a cell.</summary>
    public const int Width = 200;

    // How many of a relation's impressions Feelings names, strongest first.
    private const int _strongestImpressions = 3;

    private static readonly Style _dim = new(decoration: Decoration.Dim);
    private static readonly Style _yes = new(Color.Green);
    private static readonly Style _no = new(Color.Red);
    private static readonly Style _picked = new(Color.Aqua, decoration: Decoration.Bold);

    /// <summary>The scale of every number column and the roll rule, printed once per traced run.</summary>
    public static IRenderable Legend()
    {
        Table table = Titled(_legendTitle, ["Column", "Scale"]).HideHeaders();
        table.AddRow(Bold("Imp"), Plain("how much it matters to the holder, 0–1"));
        table.AddRow(Bold("Conf"), Plain("how sure the holder is, 0–1"));
        table.AddRow(Bold("Rel"), Plain("how much they want to talk of it, 0–1; 0 is a memory, no longer told"));
        table.AddRow(Bold("Sev"), Plain("how bad the act was, 0–1"));
        table.AddRow(Bold("Blame"), Plain(Invariant($"how much the actor is to blame, {Claim.HarmfulBlame.Min:0.0}–{Claim.HarmfulBlame.Max:0}")));
        table.AddRow(Bold("Opinion"), Plain("how much they like the other, -1–1"));
        table.AddRow(Bold("Trust"), Plain("how much they trust the other, 0–1"));
        table.AddRow(Bold("Why"), Plain("a score is its context, or a topic's relevance × its weight, plus each trait's pull, the trait × its weight; the higher, the likelier"));
        table.AddRow(Bold("Roll"), Plain("a choice says yes when its roll is below its chance"));
        return table;
    }

    /// <summary>The rule that opens a trace, before its log line: the time, who told whom and the outcome.</summary>
    public static Rule Heading(World world, ConversationTrace trace)
    {
        string teller = Markup.Escape(Named(world, trace.TellerBefore.Id));
        string listener = Markup.Escape(Named(world, trace.ListenerBefore.Id));
        string outcome = trace.Hearing switch
        {
            Hearing.Believed => $"[green]{listener} believes it[/]",
            Hearing.NotBelieved => $"[red]{listener} doesn't believe it[/]",
            Hearing.Surer or Hearing.TookTellers => $"[green]{listener} {Markup.Escape(Weighed(world, trace))}[/]",
            Hearing.ThinksLying => $"[red]{listener} {Markup.Escape(Weighed(world, trace))}[/]",
            _ => $"[dim]{listener} {Markup.Escape(Weighed(world, trace))}[/]",
        };
        string time = $"{Clock.At(trace.StartTick)}–{Clock.At(trace.EndTick)} ({Clock.Length(trace.EndTick - trace.StartTick)})";
        return new Rule($"[bold]{time}[/] · {teller} tells {listener} · {outcome}").LeftJustified();
    }

    /// <summary>The trace's tables after its log line, closed by a bare rule.</summary>
    public static IRenderable Render(World world, ConversationTrace trace) => new Rows(
        WhoTable(world, trace),
        FeelingsTable(world, trace),
        BeliefsTable(world, trace),
        Choices(world, trace),
        Story(world, trace),
        ChangesTable(world, trace),
        new Rule().RuleStyle(_dim));

    private static Table WhoTable(World world, ConversationTrace trace)
    {
        Table table = Titled(_whoTitle, ["Id", "Name", "Kind", "Role"]);
        foreach ((string id, string name, string kind, string role) in WhoAndWhat(world, trace.TellerBefore, trace.ListenerBefore, trace.Told))
        {
            table.AddRow(Bold(id), Plain(name), Plain(kind), Plain(role));
        }

        return table;
    }

    /// <summary>
    /// The two people, then each person or thing the told story names, then each one named by a belief in Beliefs, once
    /// each, with their role: "teller", "listener", or what they were in the story.
    /// </summary>
    private static IEnumerable<(string Id, string Name, string Kind, string Role)> WhoAndWhat(
        World world, PersonSnapshot teller, PersonSnapshot listener, Claim told)
    {
        var order = new List<PersonOrThing>();
        var speakerRoles = new Dictionary<PersonOrThing, string>();
        var storyRoles = new Dictionary<PersonOrThing, List<string>>();
        void Add(PersonOrThing who)
        {
            if (!who.IsUnknown && !order.Contains(who))
            {
                order.Add(who);
            }
        }

        void InStory(PersonOrThing who, string act)
        {
            if (who.IsUnknown)
            {
                return;
            }

            Add(who);
            if (!storyRoles.TryGetValue(who, out List<string>? list))
            {
                storyRoles[who] = list = [];
            }

            list.Add(act);
        }

        foreach ((PersonSnapshot person, string role) in new[] { (teller, "teller"), (listener, "listener") })
        {
            string labels = string.Join(", ", TraitWords.Shown(person.Traits));
            speakerRoles[PersonOrThing.Of(person.Id)] = $"{role}: {(labels.Length == 0 ? "no trait shows" : labels)}";
            Add(PersonOrThing.Of(person.Id));
        }

        InStory(told.Actor, told.Act == Act.WasSeenAt ? "seen" : "did it");
        InStory(told.Target, TargetRole(told.Act));
        if (PersonOrThing.Of(told.Place) != told.Target)
        {
            InStory(PersonOrThing.Of(told.Place), "where");
        }

        if (told.Witness is { } witness && !told.WitnessDidIt)
        {
            InStory(PersonOrThing.Of(witness), "saw it");
        }

        foreach (PersonOrThing who in ListedBeliefs(teller, listener, told).SelectMany(pair => Shown(pair.Belief))
            .OrderBy(who => who.Person is null).ThenBy(who => who.Person?.Value ?? who.Thing!.Value.Value))
        {
            Add(who);
        }

        foreach (PersonOrThing who in order)
        {
            var roles = new List<string>();
            if (speakerRoles.TryGetValue(who, out string? speakerRole))
            {
                roles.Add(speakerRole);
            }

            if (storyRoles.TryGetValue(who, out List<string>? list))
            {
                roles.Add($"in the story: {string.Join(", ", list)}");
            }

            string role = roles.Count == 0 ? "in their beliefs" : string.Join("; ", roles);
            yield return who.Person is { } id
                ? (Id(who), Name(world, id), "person", role)
                : (Id(who), world.Thing(who.Thing!.Value).Name, world.Thing(who.Thing!.Value).Kind.ToString().ToLowerInvariant(), role);
        }
    }

    // What the target was in the story, as a word after "in the story:".
    private static string TargetRole(Act act) => act switch
    {
        Act.Sank => "sunk",
        Act.Killed => "killed",
        Act.Robbed => "stolen",
        Act.Attacked => "attacked",
        Act.Betrayed => "betrayed",
        Act.Helped => "helped",
        Act.WasSeenAt => "where",
        _ => throw new ArgumentOutOfRangeException(nameof(act), act, null),
    };

    private static Table FeelingsTable(World world, ConversationTrace trace)
    {
        Table table = Titled(_feelingsTitle, ["Who", "Feels"]);
        foreach ((string who, string feels) in Feelings(world, trace.TellerBefore, trace.ListenerBefore, trace.Told))
        {
            table.AddRow(Bold(who), Plain(feels));
        }

        return table;
    }

    /// <summary>
    /// One line per direction: from the teller, then from the listener, toward the other and toward each person the
    /// told story names other than themselves.
    /// </summary>
    private static IEnumerable<(string Who, string Feels)> Feelings(World world, PersonSnapshot teller, PersonSnapshot listener, Claim told)
    {
        PersonId[] named = [.. ClaimNames(told).Where(who => who.Person is not null).Select(who => who.Person!.Value)];
        foreach ((PersonSnapshot from, PersonSnapshot other) in new[] { (teller, listener), (listener, teller) })
        {
            foreach (PersonId to in named.Prepend(other.Id).Distinct().Where(to => to != from.Id))
            {
                yield return ($"{Named(world, from.Id)} → {Named(world, to)}", from.RelationTo(to) is { } relation ? Feeling(relation) : "doesn't know them");
            }
        }
    }

    private static Table BeliefsTable(World world, ConversationTrace trace)
    {
        Table table = Titled(
            _beliefsTitle,
            ["", "Holder", "Actor", "Act", "Target", "When", "Witness"],
            numbers: ["Imp", "Conf", "Rel", "Sev", "Blame"],
            after: ["Heard from", "Told to"]);
        foreach (BeliefRow row in BeliefRows(world, trace.TellerBefore, trace.ListenerBefore, trace.Told, trace.StartTick))
        {
            table.AddRow(
                row.IsTold ? new Text("▶", _picked) : Empty(),
                Bold(row.Holder),
                Plain(row.Actor),
                Plain(row.Act),
                Plain(row.Target),
                Plain(row.When),
                Plain(row.Witness),
                Number(row.Importance),
                Number(row.Confidence),
                Number(row.Relevance),
                Number(row.Severity),
                Number(row.Blame),
                Entries(row.HeardFrom),
                Entries(row.ToldTo));
        }

        return table;

        static Text Entries(string entries) => entries.Length == 0 ? new Text("—", _dim) : Plain(entries);
    }

    /// <summary>
    /// One row of Beliefs: whether it is a version of the told story, and its cells. When says how long before the
    /// conversation it was; Heard from and Told to have one person and time per line, and are empty when nobody.
    /// </summary>
    private sealed record BeliefRow(
        bool IsTold, string Holder, string Actor, string Act, string Target, string When, string Witness,
        double Importance, double Confidence, double Relevance, double Severity, double Blame, string HeardFrom, string ToldTo);

    /// <summary>The rows of Beliefs, for the conversation that began at <paramref name="tick"/>.</summary>
    private static IEnumerable<BeliefRow> BeliefRows(World world, PersonSnapshot teller, PersonSnapshot listener, Claim told, long tick) =>
        ListedBeliefs(teller, listener, told).Select(pair =>
        {
            Claim claim = pair.Belief.Claim;
            return new BeliefRow(
                claim.Story == told.Story,
                Named(world, pair.Holder.Id),
                Named(world, claim.Actor),
                ClaimText.Verb(claim.Act),
                Named(world, claim.Target),
                $"{Clock.At(claim.Tick)} ({Clock.Ago(tick, claim.Tick)})",
                Witness(world, claim),
                pair.Belief.Importance,
                pair.Belief.Confidence,
                pair.Belief.Relevance,
                claim.Severity,
                claim.Blame,
                Entries(world, pair.Belief.HeardFrom.Select(entry => (entry.From, entry.Tick))),
                Entries(world, pair.Belief.ToldTo.Select(entry => (entry.To, entry.Tick))));
        });

    private static string Entries(World world, IEnumerable<(PersonId Who, long Tick)> entries) =>
        string.Join('\n', entries.Select(entry => $"{Named(world, entry.Who)} {Clock.At(entry.Tick)}"));

    // The beliefs of the teller, then of the listener, whose actor, target or witness is the teller, the listener, or a
    // person or thing the told story names, or that the two have talked about before: heard from or told to one of
    // them. A place alone doesn't count, since most stories happen in the same few places.
    private static IEnumerable<(PersonSnapshot Holder, BeliefSnapshot Belief)> ListedBeliefs(PersonSnapshot teller, PersonSnapshot listener, Claim told)
    {
        HashSet<PersonOrThing> speakers = [PersonOrThing.Of(teller.Id), PersonOrThing.Of(listener.Id)];
        HashSet<PersonOrThing> about = [.. speakers, .. ClaimNames(told)];
        return new[] { teller, listener }.SelectMany(holder =>
            holder.Beliefs
                .Where(belief => ClaimNames(belief.Claim).Any(about.Contains) || HeardFromOrToldTo(belief).Any(speakers.Contains))
                .Select(belief => (holder, belief)));
    }

    // Everyone a belief's row shows: its actor, target and witness, and whomever it was heard from or told to.
    private static IEnumerable<PersonOrThing> Shown(BeliefSnapshot belief) => ClaimNames(belief.Claim).Concat(HeardFromOrToldTo(belief));

    private static IEnumerable<PersonOrThing> HeardFromOrToldTo(BeliefSnapshot belief) =>
        belief.HeardFrom.Select(entry => PersonOrThing.Of(entry.From)).Concat(belief.ToldTo.Select(entry => PersonOrThing.Of(entry.To)));

    private static IEnumerable<PersonOrThing> ClaimNames(Claim claim) =>
        new[] { claim.Actor, claim.Target }
            .Concat(claim.Witness is { } witness ? [PersonOrThing.Of(witness)] : [])
            .Where(who => !who.IsUnknown);

    private static Table Choices(World world, ConversationTrace trace)
    {
        PersonSnapshot teller = trace.TellerBefore;
        PersonSnapshot listener = trace.ListenerBefore;
        Table table = Titled(_choicesTitle, ["Choice", "Why"], numbers: ["Chance", "Roll"], after: ["Result"]);

        RollRow(table, "Talk now", trace.Talk, null);
        RollRow(table, "Approach a stranger", trace.ApproachStranger, "no stranger here");
        if (trace.KnownListeners.Count == 0)
        {
            table.AddRow(Plain($"Listener {Named(world, listener.Id)}"), Plain("a stranger, picked at random"), Empty(), Empty(), new Text("picked", _picked));
        }

        foreach (ListenerOption option in trace.KnownListeners)
        {
            bool picked = option.Person == listener.Id;
            PickRow(table, $"Listener {Named(world, option.Person)}", Invariant($"score {option.Score:0.00}"), option.Chance, picked ? trace.ListenerRoll : null, picked);
        }

        foreach (TopicOption option in trace.Topics)
        {
            string relevance = Invariant($"relevance {option.Belief.Relevance:0.00} × {option.RelevanceWeight:0.0} = {option.RelevanceTerm:0.00}");
            string why = Why(relevance, option.Pulls, option.Score, namesChoice: true);
            bool picked = ReferenceEquals(option.Belief.Claim, trace.Held);
            PickRow(table, $"Topic: {Describe(world, option.Belief.Claim)}", why, option.Chance, picked ? trace.TopicRoll : null, picked);
        }

        RollRow(table, "Color toward harsher blame", trace.HarsherBlame, "nobody to blame");
        RollRow(table, "Claim I saw it myself", trace.ClaimWitnessed, $"{Named(world, teller.Id)} saw it");
        string noKeep = trace.Told.Witness == teller.Id ? $"{Named(world, teller.Id)} is the witness" : "no witness to keep";
        RollRow(table, "Keep the witness", trace.KeepWitness, noKeep);
        RollRow(table, "Believe the teller", trace.Believe, trace.Believe is null ? $"already holds this story and {Weighed(world, trace)}" : null);
        return table;
    }

    // What a listener who already held the story made of it.
    private static string Weighed(World world, ConversationTrace trace) => trace.Hearing switch
    {
        Hearing.SameTeller => "heard it from them before",
        Hearing.Surer => "is surer",
        Hearing.KeptOwn => "keeps their own, having seen it",
        Hearing.TookTellers => $"takes {Named(world, trace.TellerBefore.Id)}'s version",
        Hearing.ThinksLying => $"thinks {Named(world, trace.TellerBefore.Id)} is lying",
        _ => throw new ArgumentOutOfRangeException(nameof(trace), trace.Hearing, "A new story is believed or not, never weighed"),
    };

    /// <summary>
    /// What the conversation was, in sentences that replace its log line: who told whom what, how the told version
    /// differs from the teller's, and what the listener made of it, with the numbers the rules compared. People are
    /// written by id and name, never by a pronoun the simulation doesn't record.
    /// </summary>
    public static string Prose(World world, ConversationTrace trace)
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
            : StoryOf(trace.TellerBefore, trace.Held) is { HeardFrom.Count: > 0 } held
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
        BeliefSnapshot? before = StoryOf(trace.ListenerBefore, trace.Told);
        BeliefSnapshot? after = StoryOf(trace.ListenerAfter, trace.Told);
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

    // The table, then its notes indented under it.
    private static IRenderable Story(World world, ConversationTrace trace) => new Rows(
        StoryNotes(world, trace).Select(note => (IRenderable)new Text($" {note}", _dim)).Prepend(StoryTable(world, trace)));

    private static Table StoryTable(World world, ConversationTrace trace)
    {
        string listener = Named(world, trace.ListenerBefore.Id);
        Table table = Titled(_storyTitle, ["Version", "Story", "Witness"], numbers: ["Imp", "Conf", "Rel", "Sev", "Blame"]);
        table.AddRow(
            Bold($"What {listener} hears"),
            Plain(Describe(world, trace.Told)),
            Changed(Witness(world, trace.Held), Witness(world, trace.Told)),
            Empty(),
            Empty(),
            Empty(),
            Moved(trace.Held.Severity, trace.Told.Severity),
            Moved(trace.Held.Blame, trace.Told.Blame));

        BeliefSnapshot? before = StoryOf(trace.ListenerBefore, trace.Told);
        BeliefSnapshot? after = StoryOf(trace.ListenerAfter, trace.Told);
        switch (trace.Hearing)
        {
            case Hearing.NotBelieved:
                string would = Invariant($"doesn't believe it, holds nothing; would have taken confidence {trace.Offered.Value:0.00}");
                table.AddRow(Bold($"What {listener} holds"), Plain(would), Empty(), Empty(), Empty(), Empty(), Empty(), Empty());
                break;
            case Hearing.Believed:
                HeldRow($"What {listener} now holds, new", after!, after!);
                break;
            case Hearing.Surer:
                HeldRow($"What {listener} now holds, surer", before!, after!);
                break;
            case Hearing.TookTellers:
                HeldRow($"What {listener} now holds, {Named(world, trace.TellerBefore.Id)}'s version", before!, after!);
                break;
            default:
                HeldRow($"What {listener} holds, unchanged:\n{Weighed(world, trace)}", before!, before!);
                break;
        }

        return table;

        void HeldRow(string version, BeliefSnapshot was, BeliefSnapshot now) => table.AddRow(
            Bold(version),
            Changed(Describe(world, was.Claim), Describe(world, now.Claim), stacked: true),
            Changed(Witness(world, was.Claim), Witness(world, now.Claim)),
            Moved(was.Importance, now.Importance),
            Moved(was.Confidence, now.Confidence),
            Moved(was.Relevance, now.Relevance),
            Moved(was.Claim.Severity, now.Claim.Severity),
            Moved(was.Claim.Blame, now.Claim.Blame));
    }

    // Why the told version differs from the teller's, a line each: the witness, the teller's reach, and each dial's
    // parts as the reach × an opinion, or why it didn't move.
    private static IEnumerable<string> StoryNotes(World world, ConversationTrace trace)
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

    private static Table ChangesTable(World world, ConversationTrace trace)
    {
        Table table = Titled(_changesTitle, ["What changed"]).HideHeaders();
        string[] changes = [.. Changes(world, trace.ListenerBefore, trace.ListenerAfter), .. Changes(world, trace.TellerBefore, trace.TellerAfter)];
        foreach (string change in changes)
        {
            table.AddRow(Plain(change));
        }

        // A listener who already held the story may feel no different about anyone; that is said, not left blank.
        PersonSnapshot listener = trace.ListenerBefore;
        if (StoryOf(listener, trace.Told) is not null && !RelationChanges(world, listener, trace.ListenerAfter).Any())
        {
            table.AddRow(Plain($"{Named(world, listener.Id)} already held this story: nothing in how they feel about anyone changed"));
        }
        else if (changes.Length == 0)
        {
            table.AddRow(new Text("no change", _dim));
        }

        return table;
    }

    // The holder's version of the claim's story, or null when they hold none.
    private static BeliefSnapshot? StoryOf(PersonSnapshot holder, Claim claim) =>
        holder.Beliefs.FirstOrDefault(belief => belief.Claim.Story == claim.Story);

    /// <summary>
    /// What changed in one person between two snapshots, as lines: only what a conversation can touch, which is
    /// beliefs, plus the opinion, trust and impressions of each relation. The telling itself is the trace's heading, so
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

    // How one person's relations moved: someone met, or an opinion, trust or impressions changed.
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

    /// <summary>A person's or thing's id as the trace prints it: "p3" or "t5", and "?" when unknown.</summary>
    private static string Id(PersonOrThing who) => who switch
    {
        { Person: { } person } => $"p{person.Value}",
        { Thing: { } thing } => $"t{thing.Value}",
        _ => "?",
    };

    // A person or thing by id and name, "p1 Bram" or "t3 Grey Heron", and "someone" when unknown.
    private static string Named(World world, PersonOrThing who) => who switch
    {
        { Person: { } person } => $"{Id(who)} {Name(world, person)}",
        { Thing: { } thing } => $"{Id(who)} {world.Thing(thing).Name}",
        _ => "someone",
    };

    private static string Named(World world, PersonId person) => Named(world, PersonOrThing.Of(person));

    // The claim as a sentence by id and name, without its time, dials or witness: "p5 Fen sank t3 Grey Heron".
    private static string Describe(World world, Claim claim) => ClaimText.Describe(claim, who => Named(world, who));

    // A yes-or-no choice: rolled, or dimmed with why it wasn't.
    private static void RollRow(Table table, string choice, Roll? roll, string? notRolled)
    {
        if (roll is not { } made)
        {
            table.AddRow(new Text(choice, _dim), new Text($"not rolled, {notRolled}", _dim), Empty(), Empty(), new Text("—", _dim));
            return;
        }

        table.AddRow(
            Plain(choice),
            Plain(Why(Invariant($"context {made.Context:0.00}"), made.Pulls, made.Score, namesChoice: false)),
            Number(made.Chance),
            Number(made.Value),
            made.Yes ? new Text("✓ yes", _yes) : new Text("✗ no", _no));
    }

    // A score's parts, one per line so a long row never wraps: the part beside the traits and the score, then each
    // non-zero trait pull, largest first, as "Honest -0.70 × -0.8 = +0.56". A topic's pulls come from several choices,
    // so each names its own.
    private static string Why(string lead, IEnumerable<TraitPull> pulls, double score, bool namesChoice)
    {
        IEnumerable<string> lines = pulls
            .Where(pull => pull.Value != 0)
            .OrderByDescending(pull => Math.Abs(pull.Value))
            .Select(pull => (namesChoice ? $"{ChoiceName(pull.Choice)}: " : "") + Invariant($"{pull.Axis} {pull.Trait:0.00} × {pull.Weight:0.0} = {pull.Value:+0.00;-0.00}"));
        return string.Join('\n', lines.Prepend(Invariant($"{lead}; score {score:0.00}")));
    }

    // One option of a weighted pick; the roll shows on the option it picked.
    private static void PickRow(Table table, string option, string why, double chance, double? roll, bool picked) =>
        table.AddRow(
            Plain(option),
            Plain(why),
            Number(chance),
            roll is { } value ? Number(value) : Empty(),
            picked ? new Text("picked", _picked) : Empty());

    // Text columns, then right-aligned number columns that never wrap, then text columns after them. A table is as wide
    // as its content, so a short one doesn't stretch across the terminal.
    private static Table Titled(string title, string[] columns, string[]? numbers = null, string[]? after = null)
    {
        var table = new Table().Title(title, new Style(decoration: Decoration.Bold)).Border(TableBorder.Rounded).BorderStyle(_dim);
        foreach (string column in columns)
        {
            table.AddColumn(Column(column));
        }

        foreach (string column in numbers ?? [])
        {
            table.AddColumn(Column(column).RightAligned().NoWrap());
        }

        foreach (string column in after ?? [])
        {
            table.AddColumn(Column(column));
        }

        return table;
    }

    private static TableColumn Column(string name) => new(new Text(name, new Style(decoration: Decoration.Bold)));

    // Opinion, trust, ties and the strongest impressions as trait words: "opinion -0.08, trust 0.72, sibling; sees them as generous (+0.84)".
    private static string Feeling(RelationSnapshot relation)
    {
        string ties = relation.Ties == Ties.None ? "" : $", {relation.Ties.ToString().ToLowerInvariant()}";
        string[] strongest = [.. relation.Impressions
            .Where(pair => pair.Value != 0)
            .OrderByDescending(pair => Math.Abs(pair.Value)).ThenBy(pair => pair.Key)
            .Take(_strongestImpressions)
            .Select(pair => Invariant($"{TraitWords.Label(pair.Key, pair.Value)} ({pair.Value:+0.00;-0.00})"))];
        string seen = strongest.Length == 0 ? "" : $"; sees them as {string.Join(", ", strongest)}";
        return Invariant($"opinion {relation.Opinion:0.00}, trust {relation.Trust:0.00}{ties}{seen}");
    }

    private static string Impressions(RelationSnapshot relation) =>
        relation.Impressions.Count == 0
            ? "none"
            : string.Join(", ", relation.Impressions.OrderBy(pair => pair.Key).Select(pair => Invariant($"{pair.Key} {pair.Value:+0.00;-0.00;0.00}")));

    // A belief in a line, its numbers named as the legend names them.
    private static string Belief(World world, BeliefSnapshot belief) =>
        Invariant($"{Describe(world, belief.Claim)}, {Witness(world, belief.Claim)}, at {Clock.At(belief.Claim.Tick)} ") +
        Invariant($"(Imp {belief.Importance:0.00}, Conf {belief.Confidence:0.00}, Rel {belief.Relevance:0.00}, ") +
        Invariant($"Sev {belief.Claim.Severity:0.00}, Blame {belief.Claim.Blame:0.00})");

    // The claim's source: "hearsay", or its witness and what they did to know of it.
    private static string Witness(World world, Claim claim) =>
        claim.Witness is { } witness ? $"{Named(world, witness)} {ClaimText.WitnessVerb(claim)} it" : "hearsay";

    // A text that changed, as "old → new", or on two lines when both are long.
    private static IRenderable Changed(string from, string to, bool stacked = false) =>
        from == to ? Plain(to) : new Text($"{from}{(stacked ? "\n" : " ")}→ {to}", new Style(Color.Yellow));

    private static IRenderable Moved(double from, double to) =>
        from == to ? Number(to) : new Text(Invariant($"{from:0.00} → {to:0.00}"), new Style(Color.Yellow));

    private static Text Number(double value) => new(value.ToString("0.00", CultureInfo.InvariantCulture));

    private static Text Plain(string text) => new(text);

    private static Text Bold(string text) => new(text, new Style(decoration: Decoration.Bold));

    private static Text Empty() => new("");

    private static string ChoiceName(Choice choice) => choice switch
    {
        Choice.TellOthersBusiness => "others' business",
        Choice.TellScandal => "scandal",
        _ => choice.ToString(),
    };

    private static string Name(World world, PersonId id) => world.Person(id).Name;

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
