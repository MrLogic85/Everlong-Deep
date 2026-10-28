using System.Globalization;
using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// One telling: a teller picks a listener and a belief and colors it, and when the conversation ends the listener
/// forms their own (<c>docs/08 Social Model.md</c> → "Telling").
/// </summary>
internal static class Telling
{
    /// <summary>How much a belief's relevance weighs against the traits when picking what to tell.</summary>
    public const double TopicRelevanceWeight = 2;

    /// <summary>The furthest one telling moves a dial, reached only by the most coloring teller.</summary>
    public const double ColorReach = 0.3;

    // Contexts of the yes-or-no choices: approaching a stranger and claiming to have seen it are rare without the
    // traits for them, and keeping the witness is the plain way to retell.
    public const double ApproachStrangerContext = -2;
    public const double ClaimWitnessedContext = -2;
    public const double KeepWitnessContext = 1;

    private const int _loggedPulls = 3;

    /// <summary>
    /// <paramref name="teller"/> considers talking to one of <paramref name="free"/>, the people in their place who
    /// are not in a conversation. Returns the conversation they start, or null if they keep quiet.
    /// </summary>
    public static Conversation? Begin(World world, Person teller, IEnumerable<Person> free)
    {
        if (teller.Beliefs.Count == 0)
        {
            return null;
        }

        Random random = world.Random;
        var pulls = new Dictionary<TraitAxis, double>();
        Roll talk = Decide(teller, Choice.Talk, context: 0, random, pulls);
        if (!talk.Yes)
        {
            return null;
        }

        // Taken before anything changes, so the trace can show what the conversation did.
        ConversationTrace? trace = world.OnTraced is null ? null : new ConversationTrace(world.Minute, PersonSnapshot.Of(teller), talk);
        if (PickListener(teller, free, random, pulls, trace) is not (Person listener, bool isStranger))
        {
            return null;
        }

        if (trace is not null)
        {
            trace.ListenerBefore = PersonSnapshot.Of(listener);
        }

        // Nobody is told their own story or what they saw themselves.
        Belief[] topics = [.. teller.Beliefs.Where(belief => !belief.Claim.Involves(listener.Id))];
        if (topics.Length == 0)
        {
            return null;
        }

        double[] scores = [.. topics.Select(belief => TopicScore(teller, belief))];
        Pick pick = TraitEffects.Draw(scores, random);
        Belief topic = topics[pick.Index];
        if (trace is not null)
        {
            trace.Topics =
            [
                .. topics.Select((belief, i) => new TopicOption(
                    BeliefSnapshot.Of(belief),
                    [.. TopicChoices(teller, belief).Select(choice => (choice, TraitEffects.Pull(teller.Traits, choice)))],
                    scores[i],
                    pick.Chances[i])),
            ];
            trace.TopicRoll = pick.Value;
        }

        foreach (Choice choice in TopicChoices(teller, topic))
        {
            TraitEffects.AddPulls(teller.Traits, choice, pulls);
        }

        if (isStranger)
        {
            Meeting.Introduce(teller, listener, random);
        }

        Claim told = Tell(teller, topic.Claim, random, pulls, trace);
        return new Conversation(teller, listener, topic, told, isStranger, world.Minute, pulls, trace);
    }

    /// <summary>The listener of <paramref name="conversation"/> has heard it out and forms their belief, or not.</summary>
    public static void End(World world, Conversation conversation, SimulationLog log)
    {
        (Person teller, Person listener, Belief topic, Claim told) =
            (conversation.Teller, conversation.Listener, conversation.Topic, conversation.Told);
        Relation tellerSide = teller.RelationTo(listener.Id)!;
        Relation listenerSide = listener.RelationTo(teller.Id)!;

        string met = conversation.IsStranger ? "a stranger met, " : "";
        ConversationTrace? trace = conversation.Trace;
        string heard = Hear(listener, told, topic.Confidence * listenerSide.Trust, listenerSide.Trust, world.Random, trace);
        log.Write(string.Create(
            CultureInfo.InvariantCulture,
            $"{world.Clock} {teller.Name} tells {listener.Name} " +
            $"({world.Minute - conversation.StartMinute} min; {met}opinion {tellerSide.Opinion:0.00}, trust {tellerSide.Trust:0.00}): " +
            $"{ClaimText.Describe(world, told)}, {Source(world, told)} " +
            $"(confidence {topic.Confidence:0.00}, severity {told.Severity:0.00}, blame {told.Blame:0.00}); " +
            $"why: relevance {topic.Relevance:0.00}; pulls {Labels(teller, conversation.Pulls)}; {heard}"));

        if (trace is not null)
        {
            trace.EndMinute = world.Minute;
            trace.LogLine = log.Lines.Count - 1;
            trace.TellerAfter = PersonSnapshot.Of(teller);
            trace.ListenerAfter = PersonSnapshot.Of(listener);
            world.OnTraced?.Invoke(trace);
        }
    }

    /// <summary>
    /// <paramref name="claim"/> as <paramref name="teller"/> tells it: severity moves toward their opinion of the
    /// target and blame against their opinion of the actor, as far as their pull to color takes them.
    /// </summary>
    public static Claim Colored(Person teller, Claim claim, bool harsherBlame)
    {
        double reach = Reach(teller);
        double severity = Math.Clamp(claim.Severity + (reach * OpinionOf(teller, claim.Target)), 0, 1);
        if (!claim.HasBlame)
        {
            return claim.WithDials(severity, claim.Blame);
        }

        double blame = claim.Blame - (reach * OpinionOf(teller, claim.Actor)) + (harsherBlame ? reach : 0);
        return claim.WithDials(severity, Math.Clamp(blame, Claim.HarmfulBlame.Min, Claim.HarmfulBlame.Max));
    }

    /// <summary>
    /// Who <paramref name="teller"/> says saw it: themselves if they did or claim to; otherwise the witness they
    /// heard of, unless they drop it and the story goes on as hearsay.
    /// </summary>
    public static PersonId? RetoldWitness(
        Person teller, Claim claim, Random random, Dictionary<TraitAxis, double> pulls, ConversationTrace? trace = null)
    {
        if (claim.Witness == teller.Id)
        {
            return teller.Id;
        }

        Roll claimed = Decide(teller, Choice.ClaimWitnessed, ClaimWitnessedContext, random, pulls);
        trace?.ClaimWitnessed = claimed;
        if (claimed.Yes)
        {
            return teller.Id;
        }

        if (claim.Witness is null)
        {
            return null;
        }

        Roll kept = Decide(teller, Choice.KeepWitness, KeepWitnessContext, random, pulls);
        trace?.KeepWitness = kept;
        return kept.Yes ? claim.Witness : null;
    }

    // Until a heard story is matched against the listener's own versions (#19), one they already hold leaves theirs as it is.
    private static string Hear(Person listener, Claim told, double confidence, double trust, Random random, ConversationTrace? trace)
    {
        if (listener.Beliefs.Any(belief => belief.Claim.Story == told.Story))
        {
            return $"{listener.Name} already holds it";
        }

        var pulls = new Dictionary<TraitAxis, double>();
        Roll believe = Decide(listener, Choice.Believe, (2 * trust) - 1, random, pulls);
        trace?.Believe = believe;
        if (!believe.Yes)
        {
            return $"{listener.Name} doesn't believe it ({Labels(listener, pulls)})";
        }

        listener.Believe(told, confidence, Belief.StartingRelevance(listener.Id, told));
        return string.Create(CultureInfo.InvariantCulture, $"{listener.Name} believes it (confidence {confidence:0.00}; {Labels(listener, pulls)})");
    }

    // A stranger only by chance; otherwise someone known, the better liked and trusted the likelier.
    private static (Person Listener, bool IsStranger)? PickListener(
        Person teller, IEnumerable<Person> present, Random random, Dictionary<TraitAxis, double> pulls, ConversationTrace? trace)
    {
        (Person Person, Relation? Relation)[] others =
            [.. present.Where(person => person != teller).Select(person => (person, teller.RelationTo(person.Id)))];
        Person[] strangers = [.. others.Where(other => other.Relation is null).Select(other => other.Person)];
        if (strangers.Length > 0)
        {
            Roll approach = Decide(teller, Choice.ApproachStranger, ApproachStrangerContext, random, pulls);
            trace?.ApproachStranger = approach;
            if (approach.Yes)
            {
                return (strangers[random.Next(strangers.Length)], true);
            }
        }

        (Person Person, Relation? Relation)[] known = [.. others.Where(other => other.Relation is not null)];
        if (known.Length == 0)
        {
            return null;
        }

        double[] scores = [.. known.Select(other => other.Relation!.Opinion + other.Relation!.Trust)];
        Pick pick = TraitEffects.Draw(scores, random);
        if (trace is not null)
        {
            trace.KnownListeners = [.. known.Select((other, i) => new ListenerOption(other.Person.Id, scores[i], pick.Chances[i]))];
            trace.ListenerRoll = pick.Value;
        }

        return (known[pick.Index].Person, false);
    }

    private static double TopicScore(Person teller, Belief belief) =>
        (TopicRelevanceWeight * belief.Relevance) + TopicChoices(teller, belief).Sum(choice => TraitEffects.Pull(teller.Traits, choice));

    // The choices that pull on telling this belief, beside its relevance.
    private static IEnumerable<Choice> TopicChoices(Person teller, Belief belief)
    {
        if (!belief.Claim.Involves(teller.Id))
        {
            yield return Choice.TellOthersBusiness;
        }

        if (Claim.IsHarmful(belief.Claim.Act))
        {
            yield return Choice.TellScandal;
        }
    }

    private static Claim Tell(Person teller, Claim claim, Random random, Dictionary<TraitAxis, double> pulls, ConversationTrace? trace)
    {
        TraitEffects.AddPulls(teller.Traits, Choice.ColorDials, pulls);
        Roll? harsher = claim.HasBlame ? Decide(teller, Choice.ColorBlameHarsher, context: 0, random, pulls) : null;
        Claim told = Colored(teller, claim, harsher?.Yes ?? false).WithWitness(RetoldWitness(teller, claim, random, pulls, trace));
        if (trace is not null)
        {
            trace.Held = claim;
            trace.Told = told;
            trace.Reach = Reach(teller);
            trace.HarsherBlame = harsher;
        }

        return told;
    }

    private static double Reach(Person teller) => ColorReach * TraitEffects.Chance(TraitEffects.Pull(teller.Traits, Choice.ColorDials));

    private static Roll Decide(Person person, Choice choice, double context, Random random, Dictionary<TraitAxis, double> pulls)
    {
        TraitEffects.AddPulls(person.Traits, choice, pulls);
        return TraitEffects.Decide(context + TraitEffects.Pull(person.Traits, choice), random);
    }

    // The traits that pulled most across this person's choices, either way, as "talkative, dishonest".
    private static string Labels(Person person, Dictionary<TraitAxis, double> pulls)
    {
        IReadOnlyList<TraitAxis> top = TraitEffects.TopPulls(pulls, _loggedPulls);
        return top.Count == 0 ? "none" : string.Join(", ", top.Select(axis => TraitTables.Label(axis, person.Traits[axis])));
    }

    private static string Source(World world, Claim claim) =>
        claim.Witness is { } witness ? $"{world.Person(witness).Name} saw it" : "hearsay";

    // Only people are liked or disliked; a thing, an unknown or someone the teller never met weighs nothing.
    private static double OpinionOf(Person teller, PersonOrThing who) =>
        who.Person is { } person && teller.RelationTo(person) is { } relation ? relation.Opinion : 0;
}
