using System.Diagnostics;
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
    // traits for them, and keeping the witness is the plain way to pass it on.
    public const double ApproachStrangerContext = -2;
    public const double ClaimWitnessedContext = -2;
    public const double KeepWitnessContext = 1;

    /// <summary>What a retelling adds to its topic's score: a story new to the listener is the likelier, but a retelling stays possible.</summary>
    public const double TellAgainContext = -2;

    /// <summary>The share k of the gap to certainty that a new teller of the same story closes, at full confidence and trust.</summary>
    public const double ReinforcementShare = 0.5;

    /// <summary>How much less a listener trusts a teller whose competing version they reject.</summary>
    public const double LiarTrustDrop = 0.1;

    private const int _loggedPulls = 3;

    /// <summary>
    /// <paramref name="teller"/> considers talking to one of <paramref name="free"/>, the people in their place who
    /// are not in a conversation. Returns the conversation they start, and logs its start, or null if they keep quiet.
    /// </summary>
    public static Conversation? Begin(World world, Person teller, IEnumerable<Person> free, SimulationLog log)
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
        ConversationTrace? trace = world.OnTraced is null ? null : new ConversationTrace(PersonSnapshot.Of(teller), talk);
        if (PickListener(teller, free, random, pulls, trace) is not (Person listener, bool isStranger))
        {
            return null;
        }

        if (trace is not null)
        {
            trace.ListenerBefore = PersonSnapshot.Of(listener);
        }

        // Nobody is told a memory, or their own story or what they saw themselves; a story with nothing new for them is
        // a retelling, the less likely.
        Belief[] topics = [.. teller.Beliefs.Where(belief => !belief.IsMemory && !belief.Claim.Involves(listener.Id))];
        if (topics.Length == 0)
        {
            return null;
        }

        // Gathered once, so the trace shows the parts each topic was drawn by.
        HearingOutcome.Retold?[] retellings = [.. topics.Select(belief => RetellingOf(belief, listener))];
        TraitPull[][] topicPulls = [.. topics.Select((belief, i) => TopicPulls(teller, belief, retellings[i] is not null).ToArray())];
        double[] scores =
        [
            .. topics.Select((belief, i) =>
                TopicOption.ScoreOf(TopicRelevanceWeight, belief.Relevance, retellings[i] is not null, topicPulls[i])),
        ];
        Pick pick = TraitEffects.Draw(scores, random);
        Belief topic = topics[pick.Index];
        if (trace is not null)
        {
            trace.Topics =
            [
                .. topics.Select((belief, i) =>
                    new TopicOption(BeliefSnapshot.Of(belief), TopicRelevanceWeight, retellings[i] is not null, topicPulls[i], pick.Chances[i])),
            ];
            trace.TopicRoll = pick.Value;
        }

        HearingOutcome.Retold? retelling = retellings[pick.Index];
        foreach (Choice choice in TopicChoices(teller, topic, retelling is not null))
        {
            TraitEffects.AddPulls(teller.Traits, choice, pulls);
        }

        if (isStranger)
        {
            Meeting.Introduce(teller, listener, random);
        }

        Claim told = Tell(teller, topic.Claim, random, pulls, trace);
        log.Write(new ConversationStarted(world.Tick, teller.Id, listener.Id));
        return new Conversation(teller, listener, topic, told, retelling, isStranger, world.Tick, pulls, trace);
    }

    /// <summary>The listener of <paramref name="conversation"/> has heard it out and forms their belief, or not.</summary>
    public static void End(World world, Conversation conversation, SimulationLog log)
    {
        (Person teller, Person listener, Belief topic, Claim told) =
            (conversation.Teller, conversation.Listener, conversation.Topic, conversation.Told);
        Relation tellerSide = teller.RelationTo(listener.Id)!;

        ConversationTrace? trace = conversation.Trace;
        topic.RememberTold(new ToldEntry(listener.Id, world.Tick, topic.Claim, told));
        var heard = new HeardEntry(teller.Id, world.Tick, told);
        HearingOutcome outcome;
        if (conversation.Retelling is { } retold)
        {
            Retell(listener, retold, heard);
            outcome = retold;
        }
        else
        {
            outcome = Hear(world, teller, listener, heard, topic.Confidence, trace);
        }
        var ended = new ConversationEnded(
            world.Tick, teller.Id, listener.Id, conversation.StartTick, conversation.IsStranger, tellerSide.Opinion, tellerSide.Trust,
            told, topic.Confidence, topic.Relevance, Top(teller, conversation.Pulls), outcome);
        log.Write(ended);

        if (trace is not null)
        {
            trace.Ended = ended;
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

    // A teller with nothing new for the listener: they heard it from them, and an echo must not confirm a story to its
    // source, or told them this version before. For now a retelling changes neither side.
    private static HearingOutcome.Retold? RetellingOf(Belief belief, Person listener) =>
        belief.WasHeardFrom(listener.Id) ? new HearingOutcome.Retold(ToSource: true)
        : belief.WasToldUnchanged(listener.Id) ? new HearingOutcome.Retold(ToSource: false)
        : null;

    // Only the entries change; a listener who didn't believe it before isn't asked again.
    private static void Retell(Person listener, HearingOutcome.Retold retold, HeardEntry heard)
    {
        Belief? held = listener.Beliefs.FirstOrDefault(belief => belief.Claim.Story == heard.Heard.Story);
        Debug.Assert(!retold.ToSource || held is not null, "A source still holds the story they told");
        held?.RememberHeard(heard);
    }

    // A story new to the listener is believed or not by chance; one they hold is weighed against theirs, with no roll.
    private static HearingOutcome Hear(World world, Person teller, Person listener, HeardEntry heard, double tellerConfidence, ConversationTrace? trace)
    {
        Claim told = heard.Heard;
        Relation listenerSide = listener.RelationTo(teller.Id)!;
        var offered = new ListenerConfidence(tellerConfidence, listenerSide.Trust);
        trace?.Offered = offered;
        double confidence = offered.Value;
        if (listener.Beliefs.FirstOrDefault(belief => belief.Claim.Story == told.Story) is { } held)
        {
            HearingOutcome weighed = Weigh(world, teller, listener, held, heard, confidence, listenerSide);
            held.RememberHeard(heard);
            return weighed;
        }

        var pulls = new Dictionary<TraitAxis, double>();
        Roll believe = Decide(listener, Choice.Believe, (2 * listenerSide.Trust) - 1, world.Random, pulls);
        trace?.Believe = believe;
        if (!believe.Yes)
        {
            return new HearingOutcome.NotBelieved(Top(listener, pulls));
        }

        Belief formed = listener.Believe(told, confidence, Importance.Judge(world, listener, told));
        formed.RememberHeard(heard);

        // A story that doesn't matter to them is a memory from the start, never fading into one.
        return new HearingOutcome.Believed(confidence, formed.IsMemory, Top(listener, pulls));
    }

    // "Same story or another": the version told against the one held, before the telling joins the heard-from entries.
    private static HearingOutcome Weigh(
        World world, Person teller, Person listener, Belief held, HeardEntry heard, double confidence, Relation listenerSide)
    {
        if (held.WasHeardFrom(teller.Id))
        {
            return new HearingOutcome.SameTeller();
        }

        Claim told = heard.Heard;
        bool sameStory = told.Actor.IsUnknown || held.Claim.Actor.IsUnknown || told.Actor == held.Claim.Actor;
        if (sameStory && held.IsFirstHand)
        {
            return new HearingOutcome.KeptOwn();
        }

        if (sameStory)
        {
            double was = held.Confidence;
            double gain = (1 - was) * ReinforcementShare * confidence;
            held.Confidence = was + gain;
            held.Relevance = Math.Min(1, held.Relevance + gain);
            return new HearingOutcome.Surer(was, held.Confidence);
        }

        if (held.IsFirstHand || held.Confidence >= confidence)
        {
            double trust = listenerSide.Trust;
            listenerSide.Trust = Math.Max(0, trust - LiarTrustDrop);
            return new HearingOutcome.ThinksLying(held.Confidence, trust, listenerSide.Trust);
        }

        Debug.Assert(told.Story == held.Claim.Story, "A taken version tells the same story");
        held.Claim = told;
        held.Confidence = confidence;
        held.Importance = Importance.Judge(world, listener, told);
        return new HearingOutcome.TookTellers(confidence);
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

    private static IEnumerable<TraitPull> TopicPulls(Person teller, Belief belief, bool isRetelling) =>
        TopicChoices(teller, belief, isRetelling).SelectMany(choice => TraitEffects.Pulls(teller.Traits, choice));

    // The choices that pull on telling this belief, beside its relevance.
    private static IEnumerable<Choice> TopicChoices(Person teller, Belief belief, bool isRetelling)
    {
        if (isRetelling)
        {
            yield return Choice.TellAgain;
        }

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

    // The traits that pulled most across this person's choices, either way, with their values.
    private static ValueList<TraitValue> Top(Person person, Dictionary<TraitAxis, double> pulls) =>
        [.. TraitEffects.TopPulls(pulls, _loggedPulls).Select(axis => new TraitValue(axis, person.Traits[axis]))];

    // Only people are liked or disliked; a thing, an unknown, the teller or someone they never met weighs nothing.
    private static (OpinionSource Source, double Opinion) OpinionOf(Person teller, PersonOrThing who) => who switch
    {
        { Thing: not null } => (OpinionSource.Thing, 0),
        { Person: { } person } when person == teller.Id => (OpinionSource.Self, 0),
        { Person: { } person } => teller.RelationTo(person) is { } relation ? (OpinionSource.Known, relation.Opinion) : (OpinionSource.NeverMet, 0),
        _ => (OpinionSource.Unknown, 0),
    };
}
