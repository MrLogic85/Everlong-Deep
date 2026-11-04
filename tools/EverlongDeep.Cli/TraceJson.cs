using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;
using EverlongDeep.Storyteller;

namespace EverlongDeep.Cli;

/// <summary>
/// A conversation's trace as one line of JSON for an LLM to make the scene from: who the two are, what they believe,
/// how the teller shaped the story, and what it changed. People, places, objects and stories are ids, named once in
/// <c>ids</c>; a story's id comes from its act and target, so every version of one story has the same id.
/// </summary>
public static class TraceJson
{
    // Arrows, quotes and dashes stay as they are: the line is pasted into a prompt, never into a web page.
    private static readonly JsonSerializerOptions _options = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>The line for <paramref name="trace"/>.</summary>
    public static string Line(World world, ConversationTrace trace)
    {
        var ids = new Ids(world);
        PersonSnapshot teller = trace.TellerBefore;
        PersonSnapshot listener = trace.ListenerBefore;
        (PersonSnapshot Holder, BeliefSnapshot Belief)[] listed = [.. TraceText.ListedBeliefs(teller, listener, trace.Told)];
        var line = new JsonObject
        {
            ["place"] = ids.Of(teller.Place),
            ["conversation_length"] = Clock.Length(trace.EndTick - trace.StartTick),
            ["teller"] = Person(ids, teller, listener.Id, trace, listed),
            ["listener"] = Person(ids, listener, teller.Id, trace, listed),
            ["told_belief"] = Ids.Story(trace.Held.Story),
            ["told_story"] = Claim(ids, trace.Told, trace.StartTick),
            ["teller_choices"] = TellerChoices(ids, trace),
        };
        if (trace.Believe is { } believe)
        {
            line["listener_choices"] = new JsonArray(Rolled("believe the teller", believe));
        }

        line["outcome"] = Outcome(ids, trace);
        line["ids"] = ids.Table;
        return line.ToJsonString(_options);
    }

    // A speaker: their traits that show, how they feel about the other and each person the story names, and the
    // beliefs the Beliefs table lists for them.
    private static JsonObject Person(
        Ids ids, PersonSnapshot person, PersonId other, ConversationTrace trace, (PersonSnapshot Holder, BeliefSnapshot Belief)[] listed) => new()
        {
            ["id"] = ids.Of(person.Id),
            ["traits"] = Words(person.Traits.Shown.Select(trait => (trait.Axis, trait.Value))),
            ["relations"] = Array(TraceText.FeltToward(person.Id, other, trace.Told).Select(who => Relation(ids, person, who))),
            ["beliefs"] = Array(listed.Where(pair => pair.Holder.Id == person.Id).Select(pair => Belief(ids, pair.Belief, trace.StartTick))),
        };

    // "is_my" says what who is to the holder: a relation to Fen with "is_my" ["child"] means Fen is the holder's child.
    private static JsonObject Relation(Ids ids, PersonSnapshot from, PersonId who)
    {
        var json = new JsonObject { ["who"] = ids.Of(who) };
        if (from.RelationTo(who) is not { } relation)
        {
            json["knows"] = false;
            return json;
        }

        if (relation.Ties != Ties.None)
        {
            json["is_my"] = Array(Enum.GetValues<Ties>().Where(tie => tie != Ties.None && relation.Ties.HasFlag(tie)).Select(tie => tie.ToString().ToLowerInvariant()));
        }

        json["opinion"] = Round(relation.Opinion);
        json["trust"] = Round(relation.Trust);
        json["familiarity"] = Round(relation.Familiarity);
        json["sees_them_as"] = Words(TraceText.StrongestImpressions(relation).Select(pair => (pair.Key, pair.Value)));
        return json;
    }

    private static JsonObject Belief(Ids ids, BeliefSnapshot belief, long now)
    {
        JsonObject json = Claim(ids, belief.Claim, now);
        json["importance"] = Round(belief.Importance);
        json["confidence"] = Round(belief.Confidence);
        json["relevance"] = Round(belief.Relevance);
        json["heard_from"] = Array(belief.HeardFrom.Select(entry => new JsonObject { ["who"] = ids.Of(entry.From), ["ago"] = Clock.Ago(now, entry.Tick) }));
        json["told_to"] = Array(belief.ToldTo.Select(entry => new JsonObject { ["who"] = ids.Of(entry.To), ["ago"] = Clock.Ago(now, entry.Tick) }));
        return json;
    }

    private static JsonObject Claim(Ids ids, Claim claim, long now) => new()
    {
        ["id"] = Ids.Story(claim.Story),
        ["actor"] = claim.Actor.IsUnknown ? "unknown" : ids.Of(claim.Actor),
        ["act"] = ClaimText.Verb(claim.Act),
        ["target"] = ids.Of(claim.Target),
        ["place"] = ids.Of(claim.Place),
        ["ago"] = Clock.Ago(now, claim.Tick),
        ["witness"] = claim.Witness is { } witness ? ids.Of(witness) : "hearsay",
        ["details"] = Array(claim.Details.Select(ClaimText.Phrase)),
        ["severity"] = Round(claim.Severity),
        ["blame"] = Round(claim.Blame),
    };

    // What shaped the told story: why this story, how its dials moved, and each witness or detail choice rolled.
    private static JsonArray TellerChoices(Ids ids, ConversationTrace trace)
    {
        TopicOption topic = TraceText.PickedTopic(trace);
        Coloring coloring = trace.Coloring;
        var choices = new JsonArray
        {
            new JsonObject { ["choice"] = "tell this story", ["chance"] = Round(topic.Chance), ["pulls"] = Pulls(topic.Pulls) },
            Dial(ids, "color severity", trace.Held.Severity, trace.Told.Severity, coloring.SeverityCapped, coloring.TargetSource,
                coloring.TargetOpinion, "target", trace.Held.Target),
            Dial(ids, "color blame", trace.Held.Blame, trace.Told.Blame, coloring.BlameCapped, coloring.ActorSource,
                coloring.ActorOpinion, "actor", trace.Held.Actor),
        };
        foreach ((string choice, Roll? roll) in new[]
        {
            ("color toward harsher blame", trace.HarsherBlame),
            ("claim I saw it myself", trace.ClaimWitnessed),
            ("keep the witness", trace.KeepWitness),
        })
        {
            if (roll is { } made)
            {
                choices.Add(Rolled(choice, made));
            }
        }

        foreach ((Detail detail, Roll kept) in TraceText.KeptDetails(trace))
        {
            choices.Add(Rolled("keep detail", kept, ClaimText.Phrase(detail)));
        }

        return choices;
    }

    // How a dial moved as told, and the teller's opinion that moved it, or why none did.
    private static JsonObject Dial(
        Ids ids, string choice, double held, double told, bool capped, OpinionSource source, double opinion, string role, PersonOrThing who)
    {
        var json = new JsonObject { ["choice"] = choice, ["from"] = Round(held), ["to"] = Round(told) };
        if (source == OpinionSource.Known)
        {
            json[role] = ids.Of(who);
            json[$"opinion_of_{role}"] = Round(opinion);
        }
        else
        {
            json["reason"] = source switch
            {
                OpinionSource.Thing => $"the {role} is a thing",
                OpinionSource.Self => $"the teller is the {role}",
                OpinionSource.NeverMet => $"the teller doesn't know the {role}",
                OpinionSource.Unknown => "nobody knows who did it",
                OpinionSource.NoHarm => "the act does no harm",
                _ => throw new ArgumentOutOfRangeException(nameof(source), source, null),
            };
        }

        if (capped)
        {
            json["capped"] = true;
        }

        return json;
    }

    private static JsonObject Rolled(string choice, Roll roll, string? detail = null)
    {
        var json = new JsonObject { ["choice"] = choice };
        if (detail is not null)
        {
            json["detail"] = detail;
        }

        json["chance"] = Round(roll.Chance);
        json["result"] = roll.Yes ? "yes" : "no";
        json["pulls"] = Pulls(roll.Pulls);
        return json;
    }

    // Each trait's push toward yes, by the word the person's own value leans to; a trait pulling on several choices,
    // as a topic's can, is summed.
    private static JsonObject Pulls(IEnumerable<TraitPull> pulls)
    {
        var json = new JsonObject();
        foreach (IGrouping<string, TraitPull> trait in pulls.Where(pull => pull.Value != 0)
            .GroupBy(pull => TraitWords.Label(pull.Axis, pull.Trait))
            .OrderByDescending(trait => Math.Abs(trait.Sum(pull => pull.Value))))
        {
            json[trait.Key] = Round(trait.Sum(pull => pull.Value));
        }

        return json;
    }

    // What the conversation changed, one entry per person and what it is about, each value a change: "+0.19".
    private static JsonArray Outcome(Ids ids, ConversationTrace trace)
    {
        var outcome = new JsonArray();
        foreach ((PersonSnapshot before, PersonSnapshot after) in new[] { (trace.ListenerBefore, trace.ListenerAfter), (trace.TellerBefore, trace.TellerAfter) })
        {
            foreach (BeliefSnapshot now in after.Beliefs)
            {
                BeliefSnapshot? was = before.BeliefIn(now.Claim.Story);
                List<KeyValuePair<string, JsonNode?>> change = [];
                if (was?.Claim != now.Claim)
                {
                    change.Add(new("now_holds", now.Claim == trace.Told ? "told_story" : Claim(ids, now.Claim, trace.StartTick)));
                }

                Changed(change, "importance", was?.Importance ?? 0, now.Importance);
                Changed(change, "confidence", was?.Confidence ?? 0, now.Confidence);
                Changed(change, "relevance", was?.Relevance ?? 0, now.Relevance);
                if (change.Count > 0)
                {
                    outcome.Add(Entry(ids.Of(after.Id), Ids.Story(now.Claim.Story), change));
                }
            }

            foreach (RelationSnapshot now in after.Relations)
            {
                RelationSnapshot? was = before.RelationTo(now.Other);
                List<KeyValuePair<string, JsonNode?>> change = [];
                if (was is null)
                {
                    change.Add(new("met", true));
                }

                Changed(change, "opinion", was?.Opinion ?? 0, now.Opinion);
                Changed(change, "trust", was?.Trust ?? 0, now.Trust);
                Changed(change, "familiarity", was?.Familiarity ?? 0, now.Familiarity);

                // Each impression by the word it now leans to, as in relations, so "+" always means they see more of it.
                var seen = new JsonObject();
                foreach (TraitAxis axis in now.Impressions.Keys.Union(was?.Impressions.Keys ?? []).Order())
                {
                    double seenNow = now.Impressions.GetValueOrDefault(axis);
                    double toward = seenNow >= 0 ? 1 : -1;
                    Changed(seen, TraitWords.Label(axis, seenNow), toward * (was?.Impressions.GetValueOrDefault(axis) ?? 0), toward * seenNow);
                }

                if (seen.Count > 0)
                {
                    change.Add(new("sees_them_as", seen));
                }

                // The other's id is made only for a change, so the table names nobody the line doesn't show.
                if (change.Count > 0)
                {
                    outcome.Add(Entry(ids.Of(after.Id), ids.Of(now.Other), change));
                }
            }
        }

        return outcome;

        static JsonObject Entry(string who, string what, List<KeyValuePair<string, JsonNode?>> change) =>
            new([new("who", who), new("what", what), .. change]);
    }

    // A value's change, signed, when it rounds to one.
    private static void Changed(ICollection<KeyValuePair<string, JsonNode?>> json, string key, double was, double now)
    {
        double change = Round(now - was);
        if (change != 0)
        {
            json.Add(new(key, change.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)));
        }
    }

    // Traits as the words their values lean to, with how far: "cowardly": 0.93.
    private static JsonObject Words(IEnumerable<(TraitAxis Axis, double Value)> traits)
    {
        var json = new JsonObject();
        foreach ((TraitAxis axis, double value) in traits)
        {
            json[TraitWords.Label(axis, value)] = Round(Math.Abs(value));
        }

        return json;
    }

    private static JsonArray Array(IEnumerable<JsonNode> nodes) => new([.. nodes]);

    private static JsonArray Array(IEnumerable<string> values) => new([.. values.Select(value => (JsonNode)value)]);

    private static double Round(double value) => Math.Round(value, 2);

    /// <summary>
    /// The ids a line uses, "#P:" for a person, "#L:" for a place, "#O:" for a ship or item and "#S:" for a story, each
    /// eight hex digits of a hash, so ids that differ by one number still look nothing alike. Every id but a story's
    /// is named in <see cref="Table"/>, in the order the line first uses it, a person with their pronouns.
    /// </summary>
    private sealed class Ids(World world)
    {
        public JsonObject Table { get; } = new();

        public string Of(PersonId person)
        {
            Person named = world.Person(person);
            return Named($"P:{person.Value}", named.Name, "person", Pronouns.Of(named.Gender).ToString());
        }

        public string Of(ThingId id)
        {
            Thing thing = world.Thing(id);
            string prefix = thing.Kind == ThingKind.Place ? "L" : "O";
            return Named($"{prefix}:{id.Value}", thing.Name, thing.Kind.ToString().ToLowerInvariant());
        }

        public string Of(PersonOrThing who) => who.Person is { } person ? Of(person) : Of(who.Thing!.Value);

        public static string Story(Story story)
        {
            string target = story.Target.Person is { } person ? $"P:{person.Value}" : $"T:{story.Target.Thing!.Value.Value}";
            return $"#S:{Hash($"S:{story.Act}:{target}")}";
        }

        // What each id was made from, so two that hash alike are caught rather than merged.
        private readonly Dictionary<string, string> _keys = [];

        private string Named(string key, string name, string kind, string? pronouns = null)
        {
            string id = $"#{key[0]}:{Hash(key)}";
            if (_keys.TryAdd(id, key))
            {
                Table[id] = new JsonObject { ["name"] = name, ["kind"] = kind };
                if (pronouns is not null)
                {
                    Table[id]!["pronouns"] = pronouns;
                }
            }

            Debug.Assert(_keys[id] == key, $"{key} and {_keys[id]} hash to the same id");
            return id;
        }

        private static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..8];
    }
}
