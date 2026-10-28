using System.Globalization;
using System.Text;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;

namespace EverlongDeep.Cli;

/// <summary>Prints a conversation's trace as a block: who decided what and why, and what changed.</summary>
public static class TraceText
{
    public const string Separator = "---";

    public static string Format(World world, ConversationTrace trace)
    {
        var text = new StringBuilder();
        PersonSnapshot teller = trace.TellerBefore;
        PersonSnapshot listener = trace.ListenerBefore;

        Line(text, $"{World.ClockAt(trace.StartMinute)}–{World.ClockAt(trace.EndMinute)} ({trace.EndMinute - trace.StartMinute} min): {teller.Name} decided to talk to {listener.Name}");
        Line(text, $"  Talk now: {Rolled(trace.Talk)}");
        Line(text, $"");

        Person(text, world, teller);
        Person(text, world, listener);

        Line(text, $"Listener:");
        Line(text, $"  Approach a stranger: {RolledOrNot(trace.ApproachStranger, "no stranger here")}");
        if (trace.KnownListeners.Count == 0)
        {
            Line(text, $"  {listener.Name}, a stranger, picked at random");
        }
        else
        {
            foreach (ListenerOption option in trace.KnownListeners)
            {
                Line(text, $"  {Name(world, option.Person)}: score {option.Score:0.00}, chance {option.Chance:0.00}");
            }

            Line(text, $"  roll {trace.ListenerRoll:0.00} → {listener.Name}");
        }

        Line(text, $"");
        Line(text, $"Topic:");
        foreach (TopicOption option in trace.Topics)
        {
            string pulls = option.Pulls.Count == 0
                ? "no trait pulls"
                : string.Join(", ", option.Pulls.Select(pull => Invariant($"{ChoiceName(pull.Choice)} {pull.Pull:+0.00;-0.00;0.00}")));
            Line(text, $"  \"{Story(world, option.Belief.Claim, teller.Id)}\": relevance {option.Belief.Relevance:0.00}, {pulls}; score {option.Score:0.00}, chance {option.Chance:0.00}");
        }

        Line(text, $"  roll {trace.TopicRoll:0.00} → \"{Story(world, trace.Held, teller.Id)}\"");
        Line(text, $"");

        Line(text, $"{teller.Name} tells \"{Story(world, trace.Told, listener.Id)}\"");
        Line(text, $"  Coloring: reach {trace.Reach:0.00}; severity {trace.Held.Severity:0.00} → {trace.Told.Severity:0.00}, blame {trace.Held.Blame:0.00} → {trace.Told.Blame:0.00}");
        Line(text, $"  Color toward harsher blame: {RolledOrNot(trace.HarsherBlame, "nobody to blame")}");
        Line(text, $"  Claim I saw it myself: {RolledOrNot(trace.ClaimWitnessed, $"{teller.Name} saw it")}");
        string noKeep = trace.Told.Witness == teller.Id ? $"{teller.Name} is the witness" : "no witness to keep";
        Line(text, $"  Keep the witness: {RolledOrNot(trace.KeepWitness, noKeep)}");
        Line(text, $"");

        Line(text, $"{listener.Name}:");
        Line(text, $"  {(trace.Believe is { } believe
            ? $"Believe the teller: {Rolled(believe)}"
            : "already holds a story with the same act and target; nothing rolled")}");

        Line(text, $"");
        Line(text, $"Changes:");
        string[] changes = [.. Changes(world, listener, trace.ListenerAfter), .. Changes(world, teller, trace.TellerAfter)];
        foreach (string change in changes.DefaultIfEmpty("no change"))
        {
            Line(text, $"  {change}");
        }

        text.Append(Separator);
        return text.ToString();
    }

    private static void Person(StringBuilder text, World world, PersonSnapshot person)
    {
        Line(text, $"{person.Name}, in {world.Thing(person.Place).Name}:");
        Line(text, $"  Traits: {string.Join(", ", Enum.GetValues<TraitAxis>().Select(axis => Invariant($"{axis} {person.Traits[axis]:+0.00;-0.00;0.00}")))}");
        if (person.Relations.Count == 0)
        {
            Line(text, $"  Knows nobody");
        }

        foreach (RelationSnapshot relation in person.Relations)
        {
            Line(text, $"  → {Relation(world, relation)}");
        }

        if (person.Beliefs.Count == 0)
        {
            Line(text, $"  Believes nothing");
        }

        foreach (BeliefSnapshot belief in person.Beliefs)
        {
            Line(text, $"  • {Belief(world, belief, person.Id)}");
        }

        Line(text, $"");
    }

    /// <summary>
    /// What changed in one person between two snapshots, as lines: only what a conversation can touch, which is
    /// beliefs, and opinion, trust and impressions of each relation.
    /// </summary>
    public static IEnumerable<string> Changes(World world, PersonSnapshot before, PersonSnapshot after)
    {
        foreach (BeliefSnapshot added in after.Beliefs.Skip(before.Beliefs.Count))
        {
            yield return $"{after.Name} now believes: {Belief(world, added, after.Id)}";
        }

        foreach (RelationSnapshot now in after.Relations)
        {
            if (before.RelationTo(now.Other) is not { } was)
            {
                yield return $"{after.Name} met {Relation(world, now)}";
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

    private static string Relation(World world, RelationSnapshot relation)
    {
        string ties = relation.Ties == Ties.None ? "" : $", {relation.Ties}";
        return Invariant($"{Name(world, relation.Other)}: opinion {relation.Opinion:0.00}, trust {relation.Trust:0.00}{ties}; impressions {Impressions(relation)}");
    }

    private static string Impressions(RelationSnapshot relation) =>
        relation.Impressions.Count == 0
            ? "none"
            : string.Join(", ", relation.Impressions.OrderBy(pair => pair.Key).Select(pair => Invariant($"{pair.Key} {pair.Value:+0.00;-0.00;0.00}")));

    private static string Belief(World world, BeliefSnapshot belief, PersonId holder) =>
        Invariant($"{Story(world, belief.Claim, holder)}, hour {belief.Claim.Hour} ") +
        Invariant($"(confidence {belief.Confidence:0.00}, relevance {belief.Relevance:0.00}, ") +
        Invariant($"severity {belief.Claim.Severity:0.00}, blame {belief.Claim.Blame:0.00})");

    // The claim with its source as the holder would put it.
    private static string Story(World world, Claim claim, PersonId holder) =>
        ClaimText.Describe(world, claim) + claim.Witness switch
        {
            null => ", hearsay",
            { } witness when witness == holder => ", saw it",
            { } witness => $", {Name(world, witness)} saw it",
        };

    private static string Rolled(Roll roll) =>
        Invariant($"score {roll.Score:0.00}, chance {roll.Chance:0.00}, roll {roll.Value:0.00} → {(roll.Yes ? "yes" : "no")}");

    private static string RolledOrNot(Roll? roll, string why) => roll is { } made ? Rolled(made) : $"not rolled, {why}";

    private static string ChoiceName(Choice choice) => choice switch
    {
        Choice.TellOthersBusiness => "others' business",
        Choice.TellScandal => "scandal",
        _ => choice.ToString(),
    };

    private static string Name(World world, PersonId id) => world.Person(id).Name;

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    private static void Line(StringBuilder text, FormattableString line) => text.Append(Invariant(line)).Append('\n');
}
