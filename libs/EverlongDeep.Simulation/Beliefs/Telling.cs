using System.Diagnostics;
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

    /// <summary>The share k of the gap to certainty that a new teller of the same story closes, at full confidence and trust.</summary>
    public const double ReinforcementShare = 0.5;

    /// <summary>How much less a listener trusts a teller whose competing version they reject.</summary>
    public const double LiarTrustDrop = 0.1;

    private const int _loggedPulls = 3;

    /// <summary>
    /// <paramref name="teller"/> considers talking to one of <paramref name="free"/>, the people in their place who
    /// are not in a conversation. Returns the conversation they start, or null if they keep quiet.
    /// </summary>
    public static Conversation? Begin(World world, Person teller, IEnumerable<Person> free)
    {
        if (teller.Beliefs.All(belief => belief.IsMemory))
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

        // Nobody is told a memory, their own story or what they saw themselves, a story they told the teller, nor again
        // a version they were told.
        Belief[] topics =
        [
            .. teller.Beliefs.Where(belief =>
                !belief.IsMemory && !belief.Claim.Involves(listener.Id) && !belief.WasHeardFrom(listener.Id)
                && !belief.WasToldUnchanged(listener.Id)),
        ];
        if (topics.Length == 0)
        {
            return null;
        }

        // Gathered once, so the trace shows the parts each topic was drawn by.
        TraitPull[][] topicPulls = [.. topics.Select(belief => TopicPulls(teller, belief).ToArray())];
        double[] scores = [.. topics.Select((belief, i) => TopicOption.ScoreOf(TopicRelevanceWeight, belief.Relevance, topicPulls[i]))];
        Pick pick = TraitEffects.Draw(scores, random);
        Belief topic = topics[pick.Index];
        if (trace is not null)
        {
            trace.Topics =
            [
                .. topics.Select((belief, i) =>
                    new TopicOption(BeliefSnapshot.Of(belief), TopicRelevanceWeight, topicPulls[i], pick.Chances[i])),
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
        topic.RememberTold(new ToldEntry(listener.Id, world.Minute, topic.Claim, told));
        string heard = Hear(world, teller, listener, new HeardEntry(teller.Id, world.Minute, told), topic.Confidence, trace);
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
    /// target and blame against their opinion of the actor, as far as their pull to color takes them; with the parts
    /// that moved them, so the trace explains exactly what the claim was told with.
    /// </summary>
    public static (Claim Told, Coloring Coloring) Color(Person teller, Claim claim, bool harsherBlame)
    {
        (OpinionSource targetSource, double targetOpinion) = OpinionOf(teller, claim.Target);
        (OpinionSource actorSource, double actorOpinion) = Claim.IsHarmful(claim.Act) ? OpinionOf(teller, claim.Actor) : (OpinionSource.NoHarm, 0);
        var parts = new Coloring(Reach(teller), targetSource, targetOpinion, actorSource, actorOpinion, claim.HasBlame && harsherBlame);
        double severity = claim.Severity + parts.SeverityPart;
        double blame = claim.Blame + parts.BlamePart + parts.HarsherPart;
        (double min, double max) = Claim.HarmfulBlame;
        parts = parts with { SeverityCapped = severity is < 0 or > 1, BlameCapped = blame < min || blame > max };
        return (claim.WithDials(Math.Clamp(severity, 0, 1), claim.HasBlame ? Math.Clamp(blame, min, max) : claim.Blame), parts);
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

    // A story new to the listener is believed or not by chance; one they hold is weighed against theirs, with no roll.
    private static string Hear(World world, Person teller, Person listener, HeardEntry heard, double tellerConfidence, ConversationTrace? trace)
    {
        Claim told = heard.Heard;
        Relation listenerSide = listener.RelationTo(teller.Id)!;
        var offered = new ListenerConfidence(tellerConfidence, listenerSide.Trust);
        trace?.Offered = offered;
        double confidence = offered.Value;
        if (listener.Beliefs.FirstOrDefault(belief => belief.Claim.Story == told.Story) is { } held)
        {
            (Hearing weighed, string said) = Weigh(world, teller, listener, held, heard, confidence, listenerSide);
            held.RememberHeard(heard);
            trace?.Hearing = weighed;
            return said;
        }

        var pulls = new Dictionary<TraitAxis, double>();
        Roll believe = Decide(listener, Choice.Believe, (2 * listenerSide.Trust) - 1, world.Random, pulls);
        trace?.Believe = believe;
        trace?.Hearing = believe.Yes ? Hearing.Believed : Hearing.NotBelieved;
        if (!believe.Yes)
        {
            return $"{listener.Name} doesn't believe it ({Labels(listener, pulls)})";
        }

        Belief formed = listener.Believe(told, confidence, Importance.Judge(world, listener, told));
        formed.RememberHeard(heard);

        // A story that doesn't matter to them is a memory from the start, never fading into one.
        string memory = formed.IsMemory ? ", a memory at once" : "";
        return string.Create(CultureInfo.InvariantCulture, $"{listener.Name} believes it{memory} (confidence {confidence:0.00}; {Labels(listener, pulls)})");
    }

    // "Same story or another": the version told against the one held, before the telling joins the heard-from entries.
    private static (Hearing Hearing, string Said) Weigh(
        World world, Person teller, Person listener, Belief held, HeardEntry heard, double confidence, Relation listenerSide)
    {
        if (held.WasHeardFrom(teller.Id))
        {
            return (Hearing.SameTeller, $"{listener.Name} heard it from {teller.Name} before");
        }

        Claim told = heard.Heard;
        bool sameStory = told.Actor.IsUnknown || held.Claim.Actor.IsUnknown || told.Actor == held.Claim.Actor;
        if (sameStory && held.IsFirstHand)
        {
            return (Hearing.KeptOwn, $"{listener.Name} keeps their own, having seen it");
        }

        if (sameStory)
        {
            double was = held.Confidence;
            double gain = (1 - was) * ReinforcementShare * confidence;
            held.Confidence = was + gain;
            held.Relevance = Math.Min(1, held.Relevance + gain);
            return (Hearing.Surer, string.Create(
                CultureInfo.InvariantCulture, $"{listener.Name} is surer (confidence {was:0.00} → {held.Confidence:0.00})"));
        }

        if (held.IsFirstHand || held.Confidence >= confidence)
        {
            double trust = listenerSide.Trust;
            listenerSide.Trust = Math.Max(0, trust - LiarTrustDrop);
            return (Hearing.ThinksLying, string.Create(
                CultureInfo.InvariantCulture,
                $"{listener.Name} thinks {teller.Name} is lying (confidence {held.Confidence:0.00}; trust {trust:0.00} → {listenerSide.Trust:0.00})"));
        }

        Debug.Assert(told.Story == held.Claim.Story, "A taken version tells the same story");
        held.Claim = told;
        held.Confidence = confidence;
        held.Importance = Importance.Judge(world, listener, told);
        return (Hearing.TookTellers, string.Create(
            CultureInfo.InvariantCulture, $"{listener.Name} takes {teller.Name}'s version (confidence {confidence:0.00})"));
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

    private static IEnumerable<TraitPull> TopicPulls(Person teller, Belief belief) =>
        TopicChoices(teller, belief).SelectMany(choice => TraitEffects.Pulls(teller.Traits, choice));

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
        (Claim colored, Coloring coloring) = Color(teller, claim, harsher?.Yes ?? false);
        Claim told = colored.WithWitness(RetoldWitness(teller, claim, random, pulls, trace));
        if (trace is not null)
        {
            trace.Held = claim;
            trace.Told = told;
            trace.Coloring = coloring;
            trace.HarsherBlame = harsher;
        }

        return told;
    }

    private static double Reach(Person teller) => ColorReach * TraitEffects.Chance(TraitEffects.Pull(teller.Traits, Choice.ColorDials));

    private static Roll Decide(Person person, Choice choice, double context, Random random, Dictionary<TraitAxis, double> pulls)
    {
        TraitEffects.AddPulls(person.Traits, choice, pulls);
        return TraitEffects.Decide(person.Traits, choice, context, random);
    }

    // The traits that pulled most across this person's choices, either way, as "talkative, dishonest".
    private static string Labels(Person person, Dictionary<TraitAxis, double> pulls)
    {
        IReadOnlyList<TraitAxis> top = TraitEffects.TopPulls(pulls, _loggedPulls);
        return top.Count == 0 ? "none" : string.Join(", ", top.Select(axis => TraitTables.Label(axis, person.Traits[axis])));
    }

    private static string Source(World world, Claim claim) =>
        claim.Witness is { } witness ? $"{world.Person(witness).Name} {ClaimText.WitnessVerb(claim)} it" : "hearsay";

    // Only people are liked or disliked; a thing, an unknown, the teller or someone they never met weighs nothing.
    private static (OpinionSource Source, double Opinion) OpinionOf(Person teller, PersonOrThing who) => who switch
    {
        { Thing: not null } => (OpinionSource.Thing, 0),
        { Person: { } person } when person == teller.Id => (OpinionSource.Self, 0),
        { Person: { } person } => teller.RelationTo(person) is { } relation ? (OpinionSource.Known, relation.Opinion) : (OpinionSource.NeverMet, 0),
        _ => (OpinionSource.Unknown, 0),
    };
}
