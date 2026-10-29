using System.Globalization;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace EverlongDeep.Cli;

/// <summary>
/// Renders a conversation's trace as tables: the ids it names, both people and their beliefs before, whom those were
/// heard from and told to, every choice and its roll, the story, and what changed.
/// </summary>
public static class TraceTables
{
    public const string IdsTitle = "Ids";
    public const string BeforeTitle = "Before";
    public const string BeliefsTitle = "Beliefs";
    public const string TalkTitle = "Heard and told";
    public const string ChoicesTitle = "Choices";
    public const string StoryTitle = "Story";
    public const string ChangesTitle = "Changes";

    private static readonly Style _dim = new(decoration: Decoration.Dim);
    private static readonly Style _yes = new(Color.Green);
    private static readonly Style _no = new(Color.Red);
    private static readonly Style _picked = new(Color.Aqua, decoration: Decoration.Bold);

    /// <summary>The trace, opened by a rule naming the time, who told whom and the outcome, and closed by a bare rule.</summary>
    public static IRenderable Render(World world, ConversationTrace trace) => new Rows(
        Heading(trace),
        IdsTable(world, trace),
        Before(world, trace),
        Beliefs(world, trace),
        Talk(world, trace),
        Choices(world, trace),
        Story(world, trace),
        ChangesTable(world, trace),
        new Rule().RuleStyle(_dim));

    private static Rule Heading(ConversationTrace trace)
    {
        string teller = trace.TellerBefore.Name;
        string listener = trace.ListenerBefore.Name;
        string outcome = trace.Hearing switch
        {
            Hearing.Believed => $"[green]{Markup.Escape(listener)} believes it[/]",
            Hearing.NotBelieved => $"[red]{Markup.Escape(listener)} doesn't believe it[/]",
            Hearing.Surer or Hearing.TookTellers => $"[green]{Markup.Escape(listener)} {Weighed(trace)}[/]",
            Hearing.ThinksLying => $"[red]{Markup.Escape(listener)} {Weighed(trace)}[/]",
            _ => $"[dim]{Markup.Escape(listener)} {Weighed(trace)}[/]",
        };
        string time = $"{World.ClockAt(trace.StartMinute)}–{World.ClockAt(trace.EndMinute)} ({trace.EndMinute - trace.StartMinute} min)";
        return new Rule($"[bold]{time}[/] · {Markup.Escape(teller)} tells {Markup.Escape(listener)} · {outcome}").LeftJustified();
    }

    // Every person and thing the trace names, so a reader such as an LLM narrator can refer to them by id.
    private static Table IdsTable(World world, ConversationTrace trace)
    {
        Table table = Titled(IdsTitle, ["Id", "Name", "Kind"]);
        IEnumerable<PersonOrThing> named = Pair(trace)
            .SelectMany(pair => pair.Person.Beliefs.Select(belief => belief.Claim).Append(trace.Told))
            .SelectMany(claim => new[] { claim.Actor, claim.Target, PersonOrThing.Of(claim.Place) }
                .Concat(claim.Witness is { } witness ? [PersonOrThing.Of(witness)] : []))
            .Concat(Pair(trace).Select(pair => PersonOrThing.Of(pair.Person.Id)))
            .Concat(Pair(trace).SelectMany(pair => pair.Person.Beliefs).SelectMany(Talked).Select(PersonOrThing.Of))
            .Concat(trace.KnownListeners.Select(option => PersonOrThing.Of(option.Person)));
        foreach ((string id, string name, string kind) in IdRows(world, named))
        {
            table.AddRow(Bold(id), Plain(name), Plain(kind));
        }

        return table;
    }

    /// <summary>One row per person or thing named, once each and never unknown: people first, then things, each by id.</summary>
    public static IEnumerable<(string Id, string Name, string Kind)> IdRows(World world, IEnumerable<PersonOrThing> named) =>
        named.Where(who => !who.IsUnknown).Distinct()
            .OrderBy(who => who.Person is null).ThenBy(who => who.Person?.Value ?? who.Thing!.Value.Value)
            .Select(who => who.Person is { } person
                ? (Id(who), Name(world, person), "person")
                : (Id(who), world.Thing(who.Thing!.Value).Name, world.Thing(who.Thing!.Value).Kind.ToString().ToLowerInvariant()));

    private static Table Before(World world, ConversationTrace trace)
    {
        Table table = Titled(BeforeTitle, ["Person", "Labels", "Feels for the other"]);
        foreach ((PersonSnapshot person, PersonSnapshot other) in Pair(trace))
        {
            string labels = string.Join(", ", person.Traits.Labels);
            string feeling = person.RelationTo(other.Id) is { } relation ? Relation(relation) : "strangers";
            table.AddRow(Bold(person.Name), Plain(labels.Length == 0 ? "none show" : labels), Plain(feeling));
        }

        return table;
    }

    private static Table Beliefs(World world, ConversationTrace trace)
    {
        Table table = Titled(BeliefsTitle, ["Id", "Holder", "Story", "Witness"], numbers: ["Imp", "Conf", "Rel", "Sev", "Blame"]);
        table.Columns[0].NoWrap = true;
        foreach ((PersonSnapshot person, _) in Pair(trace))
        {
            if (person.Beliefs.Count == 0)
            {
                table.AddRow(Empty(), Bold(person.Name), new Text("believes nothing", _dim));
            }

            foreach (BeliefSnapshot belief in person.Beliefs)
            {
                table.AddRow(
                    Plain(BeliefId(person.Id, belief.Claim)),
                    Bold(person.Name),
                    Plain(Invariant($"{ClaimText.Describe(world, belief.Claim)}, hour {belief.Claim.Hour}")),
                    Plain(Witness(world, belief.Claim, person.Id)),
                    Number(belief.Importance),
                    Number(belief.Confidence),
                    Number(belief.Relevance),
                    Number(belief.Claim.Severity),
                    Number(belief.Claim.Blame));
            }
        }

        return table;
    }

    // Whom each belief was heard from and told to, before this conversation.
    private static Table Talk(World world, ConversationTrace trace)
    {
        Table table = Titled(TalkTitle, ["Belief", "Heard from or told to"]);
        table.Columns[0].NoWrap = true;
        foreach ((PersonSnapshot person, _) in Pair(trace))
        {
            foreach (BeliefSnapshot belief in person.Beliefs)
            {
                foreach (string entry in Entries(world, belief))
                {
                    table.AddRow(Plain(BeliefId(person.Id, belief.Claim)), Plain(entry));
                }
            }
        }

        if (table.Rows.Count == 0)
        {
            table.AddRow(Empty(), new Text("nobody yet", _dim));
        }

        return table;
    }

    private static Table Choices(World world, ConversationTrace trace)
    {
        PersonSnapshot teller = trace.TellerBefore;
        PersonSnapshot listener = trace.ListenerBefore;
        Table table = Titled(ChoicesTitle, ["Choice", "Why"], numbers: ["Chance", "Roll"], after: ["Result"]);

        RollRow(table, "Talk now", trace.Talk, null);
        RollRow(table, "Approach a stranger", trace.ApproachStranger, "no stranger here");
        if (trace.KnownListeners.Count == 0)
        {
            table.AddRow(Plain($"Listener {listener.Name}"), Plain("a stranger, picked at random"), Empty(), Empty(), new Text("picked", _picked));
        }

        foreach (ListenerOption option in trace.KnownListeners)
        {
            bool picked = option.Person == listener.Id;
            PickRow(table, $"Listener {Name(world, option.Person)}", Invariant($"score {option.Score:0.00}"), option.Chance, picked ? trace.ListenerRoll : null, picked);
        }

        foreach (TopicOption option in trace.Topics)
        {
            string pulls = option.Pulls.Count == 0
                ? "no trait pulls"
                : string.Join(", ", option.Pulls.Select(pull => Invariant($"{ChoiceName(pull.Choice)} {pull.Pull:+0.00;-0.00;0.00}")));
            string why = Invariant($"relevance {option.Belief.Relevance:0.00}, {pulls}; score {option.Score:0.00}");
            bool picked = ReferenceEquals(option.Belief.Claim, trace.Held);
            PickRow(table, $"Topic {BeliefId(teller.Id, option.Belief.Claim)}: {ClaimText.Describe(world, option.Belief.Claim)}", why, option.Chance, picked ? trace.TopicRoll : null, picked);
        }

        RollRow(table, "Color toward harsher blame", trace.HarsherBlame, "nobody to blame");
        RollRow(table, "Claim I saw it myself", trace.ClaimWitnessed, $"{teller.Name} saw it");
        string noKeep = trace.Told.Witness == teller.Id ? $"{teller.Name} is the witness" : "no witness to keep";
        RollRow(table, "Keep the witness", trace.KeepWitness, noKeep);
        RollRow(table, "Believe the teller", trace.Believe, trace.Believe is null ? $"already holds this story and {Weighed(trace)}" : null);
        return table;
    }

    // What a listener who already held the story made of it.
    private static string Weighed(ConversationTrace trace) => trace.Hearing switch
    {
        Hearing.SameTeller => "heard it from them before",
        Hearing.Surer => "is surer",
        Hearing.KeptOwn => "keeps their own, having seen it",
        Hearing.TookTellers => $"takes {trace.TellerBefore.Name}'s version",
        Hearing.ThinksLying => $"thinks {trace.TellerBefore.Name} is lying",
        _ => throw new ArgumentOutOfRangeException(nameof(trace), trace.Hearing, "A new story is believed or not, never weighed"),
    };

    private static Table Story(World world, ConversationTrace trace)
    {
        Table table = Titled(StoryTitle, ["As", "Belief", "Story", "Witness"], numbers: ["Sev", "Blame"]);
        table.Columns[1].NoWrap = true;
        table.Caption(Invariant($"coloring could move a dial by up to {trace.Reach:0.00}"), _dim);
        table.AddRow(
            Bold("held"),
            Plain(BeliefId(trace.TellerBefore.Id, trace.Held)),
            Plain(ClaimText.Describe(world, trace.Held)),
            Plain(Witness(world, trace.Held, trace.TellerBefore.Id)),
            Number(trace.Held.Severity),
            Number(trace.Held.Blame));
        table.AddRow(
            Bold("told"),
            Plain(BeliefId(trace.ListenerBefore.Id, trace.Told)),
            Plain(ClaimText.Describe(world, trace.Told)),
            Plain(Witness(world, trace.Told, trace.ListenerBefore.Id)),
            Moved(trace.Held.Severity, trace.Told.Severity),
            Moved(trace.Held.Blame, trace.Told.Blame));
        return table;
    }

    private static Table ChangesTable(World world, ConversationTrace trace)
    {
        Table table = Titled(ChangesTitle, ["What changed"]).HideHeaders();
        string[] changes = [.. Changes(world, trace.ListenerBefore, trace.ListenerAfter), .. Changes(world, trace.TellerBefore, trace.TellerAfter)];
        if (changes.Length == 0)
        {
            table.AddRow(new Text("no change", _dim));
        }

        foreach (string change in changes)
        {
            table.AddRow(Plain(change));
        }

        return table;
    }

    /// <summary>
    /// What changed in one person between two snapshots, as lines: only what a conversation can touch, which is
    /// beliefs, plus the opinion, trust and impressions of each relation.
    /// </summary>
    public static IEnumerable<string> Changes(World world, PersonSnapshot before, PersonSnapshot after)
    {
        foreach (BeliefSnapshot added in after.Beliefs.Skip(before.Beliefs.Count))
        {
            yield return $"{after.Name} now believes: {Belief(world, added, after.Id)}";
        }

        foreach (BeliefSnapshot now in after.Beliefs)
        {
            BeliefSnapshot? was = before.Beliefs.FirstOrDefault(belief => belief.Claim.Story == now.Claim.Story);
            foreach (string change in BeliefChanges(world, after, was, now))
            {
                yield return change;
            }

            foreach (string entry in Entries(world, now).Except(was is null ? [] : Entries(world, was)))
            {
                yield return $"{after.Name} remembers {BeliefId(after.Id, now.Claim)}: {entry}";
            }
        }

        foreach (RelationSnapshot now in after.Relations)
        {
            if (before.RelationTo(now.Other) is not { } was)
            {
                yield return $"{after.Name} met {Name(world, now.Other)}: {Relation(now)}";
                continue;
            }

            string name = Name(world, now.Other);
            if (now.Opinion != was.Opinion)
            {
                yield return Invariant($"{after.Name}'s opinion of {name}: {was.Opinion:0.00} → {now.Opinion:0.00}");
            }

            if (now.Trust != was.Trust)
            {
                yield return Invariant($"{after.Name}'s trust in {name}: {was.Trust:0.00} → {now.Trust:0.00}");
            }

            if (!now.Impressions.OrderBy(pair => pair.Key).SequenceEqual(was.Impressions.OrderBy(pair => pair.Key)))
            {
                yield return $"{after.Name}'s impressions of {name}: {Impressions(was)} → {Impressions(now)}";
            }
        }
    }

    // How a belief the person already held moved: its claim, importance, confidence and relevance.
    private static IEnumerable<string> BeliefChanges(World world, PersonSnapshot after, BeliefSnapshot? was, BeliefSnapshot now)
    {
        if (was is null)
        {
            yield break;
        }

        string id = BeliefId(after.Id, now.Claim);
        if (now.Claim != was.Claim)
        {
            yield return $"{after.Name} now believes {id}: {Belief(world, now, after.Id)}";
        }

        if (now.Importance != was.Importance)
        {
            yield return Invariant($"{after.Name}'s importance of {id}: {was.Importance:0.00} → {now.Importance:0.00}");
        }

        if (now.Confidence != was.Confidence)
        {
            yield return Invariant($"{after.Name}'s confidence in {id}: {was.Confidence:0.00} → {now.Confidence:0.00}");
        }

        if (now.Relevance != was.Relevance)
        {
            yield return Invariant($"{after.Name}'s relevance of {id}: {was.Relevance:0.00} → {now.Relevance:0.00}");
        }
    }

    /// <summary>A belief's heard-from and told-to entries, one line each, as "from p1 Pell 00:40" and "to p4 Ilse 01:12".</summary>
    public static IEnumerable<string> Entries(World world, BeliefSnapshot belief) =>
        belief.HeardFrom.Select(entry => $"from {Named(world, entry.From)} {World.ClockAt(entry.Minute)}")
            .Concat(belief.ToldTo.Select(entry => $"to {Named(world, entry.To)} {World.ClockAt(entry.Minute)}"));

    private static IEnumerable<PersonId> Talked(BeliefSnapshot belief) =>
        belief.HeardFrom.Select(entry => entry.From).Concat(belief.ToldTo.Select(entry => entry.To));

    private static string Named(World world, PersonId person) => $"{Id(PersonOrThing.Of(person))} {Name(world, person)}";

    /// <summary>A person's or thing's id as the trace prints it: "p3" or "t5", and "?" when unknown.</summary>
    public static string Id(PersonOrThing who) => who switch
    {
        { Person: { } person } => $"p{person.Value}",
        { Thing: { } thing } => $"t{thing.Value}",
        _ => "?",
    };

    /// <summary>
    /// A belief's id: its holder and its story, "p3/Robbed/t5". A holder keeps one belief per story, so this names it
    /// across tellings even as its dials and witness change.
    /// </summary>
    public static string BeliefId(PersonId holder, Claim claim) =>
        $"{Id(PersonOrThing.Of(holder))}/{claim.Story.Act}/{Id(claim.Story.Target)}";

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
            Plain(Invariant($"score {made.Score:0.00}")),
            Number(made.Chance),
            Number(made.Value),
            made.Yes ? new Text("✓ yes", _yes) : new Text("✗ no", _no));
    }

    // One option of a weighted pick; the roll shows on the option it picked.
    private static void PickRow(Table table, string option, string why, double chance, double? roll, bool picked) =>
        table.AddRow(
            Plain(option),
            Plain(why),
            Number(chance),
            roll is { } value ? Number(value) : Empty(),
            picked ? new Text("picked", _picked) : Empty());

    private static IEnumerable<(PersonSnapshot Person, PersonSnapshot Other)> Pair(ConversationTrace trace)
    {
        yield return (trace.TellerBefore, trace.ListenerBefore);
        yield return (trace.ListenerBefore, trace.TellerBefore);
    }

    // Text columns, then right-aligned number columns that never wrap, then text columns after them.
    private static Table Titled(string title, string[] columns, string[]? numbers = null, string[]? after = null)
    {
        var table = new Table().Title(title, new Style(decoration: Decoration.Bold)).Border(TableBorder.Rounded).BorderStyle(_dim).Expand();
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

    private static string Relation(RelationSnapshot relation)
    {
        string ties = relation.Ties == Ties.None ? "" : $", {relation.Ties}";
        return Invariant($"opinion {relation.Opinion:0.00}, trust {relation.Trust:0.00}{ties}; impressions {Impressions(relation)}");
    }

    private static string Impressions(RelationSnapshot relation) =>
        relation.Impressions.Count == 0
            ? "none"
            : string.Join(", ", relation.Impressions.OrderBy(pair => pair.Key).Select(pair => Invariant($"{pair.Key} {pair.Value:+0.00;-0.00;0.00}")));

    private static string Belief(World world, BeliefSnapshot belief, PersonId holder) =>
        Invariant($"{ClaimText.Describe(world, belief.Claim)}, {Witness(world, belief.Claim, holder)}, hour {belief.Claim.Hour} ") +
        Invariant($"(importance {belief.Importance:0.00}, confidence {belief.Confidence:0.00}, relevance {belief.Relevance:0.00}, ") +
        Invariant($"severity {belief.Claim.Severity:0.00}, blame {belief.Claim.Blame:0.00})");

    // The claim's source as the holder would put it.
    private static string Witness(World world, Claim claim, PersonId holder) => claim.Witness switch
    {
        null => "hearsay",
        { } witness when witness == holder => $"{ClaimText.WitnessVerb(claim)} it",
        { } witness => $"{Name(world, witness)} {ClaimText.WitnessVerb(claim)} it",
    };

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
