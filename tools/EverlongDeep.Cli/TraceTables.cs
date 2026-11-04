using System.Globalization;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Storyteller;
using Spectre.Console;
using Spectre.Console.Rendering;
using static EverlongDeep.Cli.TraceText;
using static EverlongDeep.Storyteller.TraceNames;

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

    private static readonly Style _dim = new(decoration: Decoration.Dim);
    private static readonly Style _yes = new(Color.Green);
    private static readonly Style _no = new(Color.Red);
    private static readonly Style _picked = new(Color.Aqua, decoration: Decoration.Bold);

    /// <summary>The scale of every number column and the roll rule, printed once per traced run.</summary>
    public static IRenderable Legend()
    {
        Table table = Titled(_legendTitle, ["Column", "Scale"]).HideHeaders();
        foreach ((_, string column, string scale) in Scales)
        {
            table.AddRow(Bold(column), Plain(scale));
        }

        return table;
    }

    /// <summary>The rule that opens a trace, before its log line: the time, who told whom and the outcome.</summary>
    public static Rule Heading(World world, ConversationTrace trace)
    {
        string teller = Markup.Escape(Named(world, trace.TellerBefore.Id));
        string listener = Markup.Escape(Named(world, trace.ListenerBefore.Id));
        string color = trace.Hearing switch
        {
            Hearing.Believed or Hearing.Surer or Hearing.TookTellers => "green",
            Hearing.NotBelieved or Hearing.ThinksLying => "red",
            _ => "dim",
        };
        string outcome = $"[{color}]{Markup.Escape(Outcome(world, trace))}[/]";
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
        foreach ((PersonSnapshot from, PersonSnapshot other) in new[] { (teller, listener), (listener, teller) })
        {
            foreach (PersonId to in FeltToward(from.Id, other.Id, told))
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

    private static Table Choices(World world, ConversationTrace trace)
    {
        Table table = Titled(_choicesTitle, ["Choice", "Why"], numbers: ["Chance", "Roll"], after: ["Result"]);
        foreach (TraceChoice choice in TraceChoices.Of(world, trace))
        {
            switch (choice)
            {
                case ListenerChoice listener:
                    ListenerOption option = listener.Option;
                    string listenerWhy = Invariant(
                        $"opinion {option.Opinion:0.00} + trust {option.Trust:0.00} + familiarity {option.Familiarity:0.00} = {option.Score:0.00}");
                    PickRow(table, $"{listener.Name} {listener.Person}", listenerWhy, option.Chance, listener.Roll, listener.Picked);
                    break;
                case StrangerChoice stranger:
                    table.AddRow(Plain($"{stranger.Name} {stranger.Person}"), Plain(StrangerChoice.Why), Empty(), Empty(), new Text("picked", _picked));
                    break;
                case TopicChoice topic:
                    TopicOption belief = topic.Option;
                    string relevance = Invariant($"relevance {belief.Belief.Relevance:0.00} × {belief.RelevanceWeight:0.0} = {belief.RelevanceTerm:0.00}");
                    string lead = belief.IsRetelling ? relevance + Invariant($", again: context {belief.Context:0.00}") : relevance;
                    string label = $"{topic.Name}: {topic.Story}{(belief.IsRetelling ? ", a retelling" : "")}";
                    PickRow(table, label, Why(lead, belief.Pulls, belief.Score, namesChoice: true), belief.Chance, topic.Roll, topic.Picked);
                    break;
                case RolledChoice rolled:
                    RollRow(table, rolled.Name, rolled.Roll, rolled.NotRolled);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(trace), choice, "A choice the table doesn't show");
            }
        }

        return table;
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

        BeliefSnapshot? before = trace.ListenerBefore.BeliefIn(trace.Told.Story);
        BeliefSnapshot? after = trace.ListenerAfter.BeliefIn(trace.Told.Story);
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
            case Hearing.Retold when before is null:
                table.AddRow(Bold($"What {listener} holds"), Plain("holds nothing; a retelling changes nothing"), Empty(), Empty(), Empty(), Empty(), Empty(), Empty());
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

    private static Table ChangesTable(World world, ConversationTrace trace)
    {
        Table table = Titled(_changesTitle, ["What changed"]).HideHeaders();
        string[] changes = [.. Changes(world, trace)];
        foreach (string change in changes)
        {
            table.AddRow(Plain(change));
        }

        if (changes.Length == 0)
        {
            table.AddRow(new Text("no change", _dim));
        }

        return table;
    }

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

    // A text that changed, as "old → new", or on two lines when both are long.
    private static IRenderable Changed(string from, string to, bool stacked = false) =>
        from == to ? Plain(to) : new Text($"{from}{(stacked ? "\n" : " ")}→ {to}", new Style(Color.Yellow));

    private static IRenderable Moved(double from, double to) =>
        from == to ? Number(to) : new Text(Invariant($"{from:0.00} → {to:0.00}"), new Style(Color.Yellow));

    private static Text Number(double value) => new(value.ToString("0.00", CultureInfo.InvariantCulture));

    private static Text Plain(string text) => new(text);

    private static Text Bold(string text) => new(text, new Style(decoration: Decoration.Bold));

    private static Text Empty() => new("");

    private static string Name(World world, PersonId id) => world.Person(id).Name;

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
