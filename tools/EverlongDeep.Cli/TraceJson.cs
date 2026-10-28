using System.Text.Json;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;

namespace EverlongDeep.Cli;

/// <summary>
/// Writes a run's conversations as JSON. What a reader would otherwise have to work out from the simulation's rules,
/// such as which story a claim tells or how a telling ended, is written out from the simulation's own answers.
/// </summary>
public static class TraceJson
{
    /// <summary>Changes whenever the shape does; a viewer refuses any other.</summary>
    public const int Version = 1;

    public static void Write(Stream stream, World world, IReadOnlyList<string> log, IReadOnlyList<ConversationTrace> traces)
    {
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteNumber("version", Version);
            json.WriteNumber("seed", world.Seed);
            Array(json, "things", world.Things, thing =>
            {
                json.WriteNumber("id", thing.Id.Value);
                json.WriteString("name", thing.Name);
                json.WriteString("kind", thing.Kind.ToString());
            });
            Array(json, "people", world.People, person =>
            {
                json.WriteNumber("id", person.Id.Value);
                json.WriteString("name", person.Name);
                json.WriteNumber("place", person.Place.Value);
                json.WriteStartArray("labels");
                foreach (string label in person.Traits.Labels)
                {
                    json.WriteStringValue(label);
                }

                json.WriteEndArray();
                json.WriteStartObject("traits");
                foreach (TraitAxis axis in Enum.GetValues<TraitAxis>())
                {
                    json.WriteNumber(axis.ToString(), person.Traits[axis]);
                }

                json.WriteEndObject();
            });
            json.WriteStartArray("log");
            foreach (string line in log)
            {
                json.WriteStringValue(line);
            }

            json.WriteEndArray();
            Array(json, "stories", Stories(traces).OrderBy(story => story.Key, StringComparer.Ordinal), story =>
            {
                json.WriteString("key", story.Key);
                json.WriteString("act", story.Value.Act.ToString());
                NumberOrNull(json, "targetPerson", story.Value.Target.Person?.Value);
                NumberOrNull(json, "targetThing", story.Value.Target.Thing?.Value);
            });
            Array(json, "conversations", traces, trace => Conversation(json, world, trace));
            json.WriteEndObject();
        }
    }

    // A story as a key, such as "Sank/thing 1".
    private static string StoryKey(Story story) => story.Target switch
    {
        { Person: { } person } => $"{story.Act}/person {person.Value}",
        { Thing: { } thing } => $"{story.Act}/thing {thing.Value}",
        _ => $"{story.Act}/unknown",
    };

    // How the listener took it: believed, disbelieved, or already held so nothing was rolled.
    private static string Outcome(ConversationTrace trace) => trace.Believe switch
    {
        null => "alreadyHeld",
        { Yes: true } => "believed",
        _ => "disbelieved",
    };

    // Each story the conversations name, by what every version of it shares; a version's own wording is on the claim.
    private static Dictionary<string, Story> Stories(IReadOnlyList<ConversationTrace> traces)
    {
        var stories = new Dictionary<string, Story>();
        IEnumerable<Claim> claims = traces
            .SelectMany(trace => new[] { trace.TellerBefore, trace.ListenerBefore, trace.TellerAfter, trace.ListenerAfter })
            .SelectMany(snapshot => snapshot.Beliefs.Select(belief => belief.Claim))
            .Concat(traces.SelectMany(trace => new[] { trace.Held, trace.Told }));
        foreach (Claim claim in claims)
        {
            stories.TryAdd(StoryKey(claim.Story), claim.Story);
        }

        return stories;
    }

    private static void Conversation(Utf8JsonWriter json, World world, ConversationTrace trace)
    {
        json.WriteNumber("logLine", trace.LogLine);
        json.WriteNumber("start", trace.StartMinute);
        json.WriteNumber("end", trace.EndMinute);
        json.WriteNumber("teller", trace.TellerBefore.Id.Value);
        json.WriteNumber("listener", trace.ListenerBefore.Id.Value);
        json.WriteString("story", StoryKey(trace.Held.Story));
        json.WriteString("outcome", Outcome(trace));
        Roll(json, "talk", trace.Talk);
        Roll(json, "approachStranger", trace.ApproachStranger);
        Array(json, "knownListeners", trace.KnownListeners, option =>
        {
            json.WriteNumber("person", option.Person.Value);
            json.WriteNumber("score", option.Score);
            json.WriteNumber("chance", option.Chance);
        });
        NumberOrNull(json, "listenerRoll", trace.ListenerRoll);
        Array(json, "topics", trace.Topics, option =>
        {
            json.WriteString("story", StoryKey(option.Belief.Claim.Story));
            json.WriteString("text", ClaimText.Describe(world, option.Belief.Claim));
            json.WriteNumber("relevance", option.Belief.Relevance);
            json.WriteStartObject("pulls");
            foreach ((Choice choice, double pull) in option.Pulls)
            {
                json.WriteNumber(choice.ToString(), pull);
            }

            json.WriteEndObject();
            json.WriteNumber("score", option.Score);
            json.WriteNumber("chance", option.Chance);
            json.WriteBoolean("picked", option.Belief.Claim == trace.Held);
        });
        json.WriteNumber("topicRoll", trace.TopicRoll);
        ClaimObject(json, world, "held", trace.Held);
        ClaimObject(json, world, "told", trace.Told);
        json.WriteNumber("reach", trace.Reach);
        Roll(json, "harsherBlame", trace.HarsherBlame);
        Roll(json, "claimWitnessed", trace.ClaimWitnessed);
        Roll(json, "keepWitness", trace.KeepWitness);
        Roll(json, "believe", trace.Believe);
        json.WriteStartObject("before");
        Snapshot(json, world, "teller", trace.TellerBefore);
        Snapshot(json, world, "listener", trace.ListenerBefore);
        json.WriteEndObject();
        json.WriteStartObject("after");
        Snapshot(json, world, "teller", trace.TellerAfter);
        Snapshot(json, world, "listener", trace.ListenerAfter);
        json.WriteEndObject();
    }

    // Traits never change, so a snapshot leaves them to "people".
    private static void Snapshot(Utf8JsonWriter json, World world, string name, PersonSnapshot snapshot)
    {
        json.WriteStartObject(name);
        json.WriteNumber("place", snapshot.Place.Value);
        Array(json, "relations", snapshot.Relations, relation =>
        {
            json.WriteNumber("other", relation.Other.Value);
            json.WriteNumber("opinion", relation.Opinion);
            json.WriteNumber("trust", relation.Trust);
            json.WriteString("ties", relation.Ties.ToString());
            json.WriteStartObject("impressions");
            foreach ((TraitAxis axis, double value) in relation.Impressions.OrderBy(pair => pair.Key))
            {
                json.WriteNumber(axis.ToString(), value);
            }

            json.WriteEndObject();
        });
        Array(json, "beliefs", snapshot.Beliefs, belief =>
        {
            ClaimFields(json, world, belief.Claim);
            json.WriteNumber("confidence", belief.Confidence);
            json.WriteNumber("relevance", belief.Relevance);
        });
        json.WriteEndObject();
    }

    private static void ClaimObject(Utf8JsonWriter json, World world, string name, Claim claim)
    {
        json.WriteStartObject(name);
        ClaimFields(json, world, claim);
        json.WriteEndObject();
    }

    // Each version has its own wording, since versions of a story differ in actor, place and witness.
    private static void ClaimFields(Utf8JsonWriter json, World world, Claim claim)
    {
        json.WriteString("story", StoryKey(claim.Story));
        json.WriteString("text", ClaimText.Describe(world, claim));
        NumberOrNull(json, "actorPerson", claim.Actor.Person?.Value);
        NumberOrNull(json, "actorThing", claim.Actor.Thing?.Value);
        json.WriteNumber("place", claim.Place.Value);
        NumberOrNull(json, "witness", claim.Witness?.Value);
        json.WriteNumber("severity", claim.Severity);
        json.WriteNumber("blame", claim.Blame);
        json.WriteNumber("hour", claim.Hour);
    }

    private static void Roll(Utf8JsonWriter json, string name, Roll? roll)
    {
        if (roll is not { } made)
        {
            json.WriteNull(name);
            return;
        }

        json.WriteStartObject(name);
        json.WriteNumber("score", made.Score);
        json.WriteNumber("chance", made.Chance);
        json.WriteNumber("roll", made.Value);
        json.WriteBoolean("yes", made.Yes);
        json.WriteEndObject();
    }

    private static void NumberOrNull(Utf8JsonWriter json, string name, double? value)
    {
        if (value is { } number)
        {
            json.WriteNumber(name, number);
        }
        else
        {
            json.WriteNull(name);
        }
    }

    private static void Array<T>(Utf8JsonWriter json, string name, IEnumerable<T> items, Action<T> write)
    {
        json.WriteStartArray(name);
        foreach (T item in items)
        {
            json.WriteStartObject();
            write(item);
            json.WriteEndObject();
        }

        json.WriteEndArray();
    }
}
