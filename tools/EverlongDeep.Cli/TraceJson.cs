using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Storyteller;
using static EverlongDeep.Storyteller.TraceNames;

namespace EverlongDeep.Cli;

/// <summary>
/// A conversation's trace as one line of JSON for an LLM to make a story from: everything the trace holds, each key
/// spelled out, each person or thing by id and name, and the scale of every number, so the line stands alone.
/// </summary>
public static class TraceJson
{
    // Arrows, quotes and dashes stay as they are: the line is pasted into a prompt, never into a web page.
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // The numbers only the line shows as numbers; the tables show them as words or in a sum. Each scale is said once,
    // here or in TraceText.Scales, and the other names it.
    private static readonly (string Name, string Scale)[] _jsonScales =
    [
        ("traits", "who a person is, each -1–1: the trait's name is its + end, and below 0 leans to its opposite, as traitsThatShow words it"),
        ("impressions", "how the holder sees the other on a trait, -1–1, read as traits are"),
        ("context", "the part of a score the situation gives, before any trait"),
        ("pulls", "each trait's part of a score, as score says: traitValue × weight = pull; relevanceWeight is the weight of a topic's relevance"),
        ("chance", "how likely a choice was to say yes, or an option to be picked, 0–1"),
        ("confidenceOffered", "the confidence this telling gives the listener: tellersConfidence × listenersTrust; what a listener new to the story takes, or would have taken, and what one who holds it weighs their own against"),
    ];

    /// <summary>The line for <paramref name="trace"/>, in a run of <paramref name="seed"/>.</summary>
    public static string Line(World world, int seed, ConversationTrace trace)
    {
        PersonSnapshot teller = trace.TellerBefore;
        PersonSnapshot listener = trace.ListenerBefore;
        ListenerConfidence offered = trace.Offered;
        var line = new
        {
            Seed = seed,
            Start = Clock.At(trace.StartTick),
            End = Clock.At(trace.EndTick),
            Length = Clock.Length(trace.EndTick - trace.StartTick),
            Summary = TraceProse.Of(world, trace),
            Teller = Person(world, teller, listener.Id, trace),
            Listener = Person(world, listener, teller.Id, trace),
            Held = Claim(world, trace.Held, trace.StartTick),
            Told = Claim(world, trace.Told, trace.StartTick),
            HowItWasTold = TraceText.StoryNotes(world, trace),
            Choices = Choices(world, trace),
            ConfidenceOffered = new { Confidence = Round(offered.Value), TellersConfidence = Round(offered.Teller), ListenersTrust = Round(offered.Trust) },
            Outcome = TraceText.Outcome(world, trace),
            Changes = TraceText.Changes(world, trace),
            Scales = TraceText.Scales.Select(scale => (scale.Name, scale.Scale)).Concat(_jsonScales).ToDictionary(),
        };
        return JsonSerializer.Serialize(line, _options);
    }

    // A speaker: who they are, how they feel about the other and about each person the story names, and every belief.
    private static object Person(World world, PersonSnapshot person, PersonId other, ConversationTrace trace) => new
    {
        Person = Named(world, person.Id),
        Place = Named(world, PersonOrThing.Of(person.Place)),
        Traits = Enum.GetValues<TraitAxis>().ToDictionary(axis => axis.ToString().ToLowerInvariant(), axis => Round(person.Traits[axis])),
        TraitsThatShow = TraitWords.Shown(person.Traits),
        Relations = TraceText.FeltToward(person.Id, other, trace.Told).Select(to => Relation(world, person, to)),
        Beliefs = person.Beliefs.Select(belief => new
        {
            Story = Claim(world, belief.Claim, trace.StartTick),
            Importance = Round(belief.Importance),
            Confidence = Round(belief.Confidence),
            Relevance = Round(belief.Relevance),
            HeardFrom = belief.HeardFrom.Select(entry => new { From = Named(world, entry.From), At = Clock.At(entry.Tick) }),
            ToldTo = belief.ToldTo.Select(entry => new { To = Named(world, entry.To), At = Clock.At(entry.Tick) }),
        }),
    };

    private static object Relation(World world, PersonSnapshot from, PersonId to) => from.RelationTo(to) is not { } relation
        ? new { To = Named(world, to), Knows = false }
        : (object)new
        {
            To = Named(world, to),
            Knows = true,
            Opinion = Round(relation.Opinion),
            Trust = Round(relation.Trust),
            Familiarity = Round(relation.Familiarity),
            Ties = Enum.GetValues<Ties>().Where(tie => tie != Ties.None && relation.Ties.HasFlag(tie)).Select(tie => tie.ToString().ToLowerInvariant()),
            Impressions = relation.Impressions.Where(pair => pair.Value != 0).OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key.ToString().ToLowerInvariant(), pair => Round(pair.Value)),
        };

    private static object Claim(World world, Claim claim, long now) => new
    {
        Sentence = Describe(world, claim),
        Actor = Named(world, claim.Actor),
        Act = ClaimText.Verb(claim.Act),
        Target = Named(world, claim.Target),
        Place = Named(world, PersonOrThing.Of(claim.Place)),
        When = Clock.At(claim.Tick),
        Ago = Clock.Ago(now, claim.Tick),
        Source = Witness(world, claim),
        Details = claim.Details.Select(ClaimText.Phrase),
        Severity = Round(claim.Severity),
        Blame = Round(claim.Blame),
    };

    // Every choice, as the Choices table shows them: rolled, picked from options, or not made and why.
    private static IEnumerable<object> Choices(World world, ConversationTrace trace) => TraceChoices.Of(world, trace).Select(choice => choice switch
    {
        ListenerChoice listener => (object)new
        {
            Choice = listener.Name,
            listener.Person,
            Opinion = Round(listener.Option.Opinion),
            Trust = Round(listener.Option.Trust),
            Familiarity = Round(listener.Option.Familiarity),
            Score = Round(listener.Option.Score),
            Chance = Round(listener.Option.Chance),
            Roll = Round(listener.Roll),
            Result = listener.Picked ? "picked" : "not picked",
        },
        StrangerChoice stranger => new { Choice = stranger.Name, stranger.Person, Why = StrangerChoice.Why, Result = "picked" },
        TopicChoice topic => new
        {
            Choice = topic.Name,
            topic.Story,
            Retelling = topic.Option.IsRetelling,
            Relevance = Round(topic.Option.Belief.Relevance),
            RelevanceWeight = Round(topic.Option.RelevanceWeight),
            Context = Round(topic.Option.Context),
            Pulls = Pulls(topic.Option.Pulls, namesChoice: true),
            Score = Round(topic.Option.Score),
            Chance = Round(topic.Option.Chance),
            Roll = Round(topic.Roll),
            Result = topic.Picked ? "picked" : "not picked",
        },
        RolledChoice rolled => Rolled(rolled.Name, rolled.Roll, rolled.NotRolled),
        _ => throw new ArgumentOutOfRangeException(nameof(trace), choice, "A choice the line doesn't hold"),
    });

    // A yes-or-no choice: its parts, chance, roll and result, or why it wasn't rolled.
    private static object Rolled(string choice, Roll? roll, string? notRolled) => roll is not { } made
        ? new { Choice = choice, NotRolled = notRolled }
        : (object)new
        {
            Choice = choice,
            Context = Round(made.Context),
            Pulls = Pulls(made.Pulls, namesChoice: false),
            Score = Round(made.Score),
            Chance = Round(made.Chance),
            Roll = Round(made.Value),
            Result = made.Yes ? "yes" : "no",
        };

    // Each non-zero trait pull, largest first, as the trait's value × the choice's weight; a topic's pulls come from
    // several choices, so each names its own.
    private static IEnumerable<object> Pulls(IEnumerable<TraitPull> pulls, bool namesChoice) => pulls
        .Where(pull => pull.Value != 0)
        .OrderByDescending(pull => Math.Abs(pull.Value))
        .Select(pull => new
        {
            For = namesChoice ? TraceText.ChoiceName(pull.Choice) : null,
            Trait = pull.Axis.ToString().ToLowerInvariant(),
            TraitValue = Round(pull.Trait),
            Weight = Round(pull.Weight),
            Pull = Round(pull.Value),
        });

    private static double Round(double value) => Math.Round(value, 2);

    private static double? Round(double? value) => value is { } made ? Round(made) : null;
}
