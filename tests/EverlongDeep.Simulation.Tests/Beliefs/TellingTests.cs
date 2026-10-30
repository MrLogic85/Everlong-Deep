using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public class TellingTests
{
    private const double _tolerance = 1e-9;

    // The tick a telling in these tests is heard out at.
    private const int _heardOut = 240;

    private static readonly Thing _tavern = new(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);
    private static readonly Thing _ship = new(new ThingId(1), "Saltwren", ThingKind.Ship, 0.5);

    private readonly SimulationLog _log = new();

    // Bram, Jory and Fen are aboard the Saltwren, so only Mara and Pell can talk in the tavern.
    private readonly Person _mara = Someone(0, "Mara", _tavern, (TraitAxis.Talkative, 0.6));
    private readonly Person _pell = Someone(1, "Pell", _tavern, (TraitAxis.Adaptable, 0.6));
    private readonly Person _bram = Someone(2, "Bram", _ship);
    private readonly Person _jory = Someone(3, "Jory", _ship);
    private readonly Person _fen = Someone(4, "Fen", _ship);

    [Fact]
    public void BeginAndEnd_ABeliefReachesSomeoneWhoDidNotHoldIt_AsTheirOwn_Colored()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _mara.Meet(_bram.Id, opinion: -1, trust: 0.5);
        _mara.Meet(_jory.Id, opinion: 0.8, trust: 0.5);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        _mara.Believe(BramAttackedJory(witness: _mara.Id), confidence: 0.9, importance: 0.8);

        // Every draw says yes: Mara colors toward harsher blame and keeps herself as the witness.
        Tell(Tavern(new FixedRandom(0)), _mara, _pell);

        Belief heard = _pell.Beliefs.ShouldHaveSingleItem();
        heard.Holder.ShouldBe(_pell.Id);
        heard.Claim.Witness.ShouldBe(_mara.Id);
        heard.IsFirstHand.ShouldBeFalse();
        heard.Claim.Severity.ShouldBeGreaterThan(Claim.BaseSeverity(Act.Attacked));
        heard.Relevance.ShouldBe(heard.Importance, _tolerance);
    }

    [Fact]
    public void BeginAndEnd_AStranger_BecomesKnownBothWays()
    {
        _mara.Believe(BramAttackedJory(witness: null), confidence: 0.9, importance: 0.8);

        Tell(Tavern(new FixedRandom(0)), _mara, _pell);

        _mara.RelationTo(_pell.Id).ShouldNotBeNull().Impressions.ShouldNotBeEmpty();
        _pell.RelationTo(_mara.Id).ShouldNotBeNull().Impressions.ShouldNotBeEmpty();
        _pell.Beliefs.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Begin_NobodyIsToldTheirOwnStory_OrWhatTheySaw(bool bramSawIt)
    {
        // Bram ashore, in the tavern with Mara, who knows only a story he is in or saw.
        Person bram = Someone(2, "Bram", _tavern);
        _mara.Meet(bram.Id, opinion: 0.4, trust: 0.6);
        bram.Meet(_mara.Id, opinion: 0.4, trust: 0.6);
        Claim claim = bramSawIt
            ? new Claim(PersonOrThing.Of(_jory.Id), Act.Sank, PersonOrThing.Of(_ship.Id), _ship.Id, tick: -5 * 3_600, severity: 0.5, blame: 0.2, bram.Id)
            : BramAttackedJory(witness: null);
        _mara.Believe(claim, confidence: 0.9, importance: 0.8);
        var world = new World(seed: 0, new FixedRandom(0), [_tavern, _ship], [_mara, bram, _jory]);

        Telling.Begin(world, _mara, [bram], _log).ShouldBeNull();
    }

    [Fact]
    public void End_AListenerWhoDoesNotBelieveTheTeller_FormsNothing()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.1);
        _mara.Believe(BramAttackedJory(witness: _mara.Id), confidence: 0.9, importance: 0.8);

        // Talk, listener, topic and harsher blame say yes; believing, at low trust, says no.
        Tell(Tavern(new ScriptedRandom(0, 0, 0, 0, 0.99)), _mara, _pell);

        _pell.Beliefs.ShouldBeEmpty();
        _mara.Beliefs.Single().ToldTo.ShouldHaveSingleItem().To.ShouldBe(_pell.Id);
    }

    [Fact]
    public void End_ANewTellerOfAHeldStory_MakesTheListenerSurer_DrawsNothing_AndRemembersBothTellers()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        _mara.Believe(BramAttackedJory(witness: _mara.Id), confidence: 0.9, importance: 0.8);
        Belief own = _pell.Believe(BramAttackedJory(witness: null), confidence: 0.4, importance: 0.5);
        var fromBram = new HeardEntry(_bram.Id, Tick: 0, own.Claim);
        own.RememberHeard(fromBram);

        // Talk, listener, topic and harsher blame; hearing it draws nothing.
        Tell(Tavern(new ScriptedRandom(0, 0, 0, 0)), _mara, _pell);

        // Confidence and relevance gain alike.
        _pell.Beliefs.ShouldBe([own]);
        own.Claim.ShouldBe(BramAttackedJory(witness: null));
        own.Confidence.ShouldBeGreaterThan(0.4);
        (own.Relevance - 0.5).ShouldBe(own.Confidence - 0.4, _tolerance);
        own.HeardFrom.ShouldBe([fromBram, new HeardEntry(_mara.Id, Tick: _heardOut, _mara.Beliefs.Single().ToldTo.Single().Told)]);
        LastEnded().Outcome.ShouldBe(new HearingOutcome.Surer(0.4, own.Confidence));
    }

    [Fact]
    public void End_TheSameTellerAgain_MovesNothing_AndUpdatesTheirEntry()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        Belief told = _mara.Believe(BramAttackedJory(witness: null), confidence: 0.9, importance: 0.8);
        Belief own = _pell.Believe(BramAttackedJory(witness: null), confidence: 0.4, importance: 0.5);
        own.RememberHeard(new HeardEntry(_mara.Id, Tick: 0, BramAttackedJory(witness: _bram.Id)));
        ConversationTrace trace = Traced(Tavern(new FixedRandom(0)), _mara, _pell);

        trace.Hearing.ShouldBe(Hearing.SameTeller);
        (own.Claim, own.Confidence, own.Relevance).ShouldBe((BramAttackedJory(witness: null), 0.4, 0.5));
        own.HeardFrom.ShouldBe([new HeardEntry(_mara.Id, Tick: _heardOut, told.ToldTo.Single().Told)]);
    }

    [Fact]
    public void End_EachNewTellerOfTheSameStory_MakesTheListenerSurer_NeverCertain()
    {
        Belief own = _pell.Believe(BramAttackedJory(witness: null), confidence: 0.4, importance: 0.5);
        Person[] tellers =
        [
            .. Enumerable.Range(10, 10).Select(id => Someone(id, $"Teller {id}", _tavern, (TraitAxis.Talkative, 0.6))),
        ];
        var world = new World(seed: 0, new FixedRandom(0), [_tavern, _ship], [_mara, _pell, _bram, _jory, _fen, .. tellers]);

        double was = own.Confidence;
        foreach (Person teller in tellers)
        {
            teller.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
            _pell.Meet(teller.Id, opinion: 0.2, trust: 1);
            teller.Believe(BramAttackedJory(witness: null), confidence: 1, importance: 0.8);

            Tell(world, teller, _pell);

            own.Confidence.ShouldBeGreaterThan(was);
            own.Confidence.ShouldBeLessThan(1);
            was = own.Confidence;
        }

        own.HeardFrom.Count.ShouldBe(10);
    }

    [Fact]
    public void Begin_ATeller_NeverPicksAStoryTheyHeardFromTheListener()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        Belief heard = _mara.Believe(BramAttackedJory(witness: null), confidence: 0.9, importance: 0.8);
        heard.RememberHeard(new HeardEntry(_pell.Id, Tick: 0, heard.Claim));

        Telling.Begin(Tavern(new FixedRandom(0)), _mara, [_pell], _log).ShouldBeNull();
    }

    [Theory]
    [InlineData(0.6, 0.5, 1)]
    [InlineData(0.5, 0.5, 1)]
    [InlineData(0.5, 0.5, 0.05)]
    public void End_ACompetingVersion_HeldAtLeastAsSurely_IsRejected_AndTheTellerTrustedLess(
        double ownConfidence, double tellerConfidence, double trust)
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        Relation pellToMara = _pell.Meet(_mara.Id, opinion: 0.2, trust);
        _mara.Believe(FenAttackedJory(), confidence: tellerConfidence, importance: 0.8);
        Belief own = _pell.Believe(BramAttackedJory(witness: null), confidence: ownConfidence, importance: 0.5);

        ConversationTrace trace = Traced(Tavern(new FixedRandom(0)), _mara, _pell);

        (own.Claim, own.Confidence, own.Importance).ShouldBe((BramAttackedJory(witness: null), ownConfidence, 0.5));
        pellToMara.Trust.ShouldBe(Math.Max(0, trust - Telling.LiarTrustDrop), _tolerance);
        trace.Ended.Outcome.ShouldBe(new HearingOutcome.ThinksLying(ownConfidence, trust, pellToMara.Trust));
    }

    [Fact]
    public void End_ACompetingVersion_BelievedMoreSurely_IsTaken_WithItsConfidence_AndImportanceJudgedAgain()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        Relation pellToMara = _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.8);
        _pell.Meet(_fen.Id, opinion: 0, trust: 0.5).Ties = Ties.Sibling;
        _mara.Believe(FenAttackedJory(), confidence: 0.9, importance: 0.8);
        Belief own = _pell.Believe(BramAttackedJory(witness: null), confidence: 0.3, importance: 0.1);
        own.Relevance = 0.4;

        ConversationTrace trace = Traced(Tavern(new FixedRandom(0)), _mara, _pell);

        trace.Ended.Outcome.ShouldBe(new HearingOutcome.TookTellers(trace.Offered.Value));
        own.Claim.Actor.ShouldBe(PersonOrThing.Of(_fen.Id));
        own.Claim.ShouldBe(_mara.Beliefs.Single().ToldTo.Single().Told);
        own.Confidence.ShouldBe(trace.Offered.Value, _tolerance);
        // Fen is Pell's sibling, which the version Pell held didn't name.
        own.Importance.ShouldBe(Claim.BaseSeverity(Act.Attacked) + Importance.TieStake, _tolerance);
        own.Relevance.ShouldBe(0.4);
        pellToMara.Trust.ShouldBe(0.8);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void End_AnUnknownActorOnEitherSide_IsTheSameStory(bool listenerKnowsTheActor)
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        Claim known = FenAttackedJory();
        Claim unknown = new(PersonOrThing.Unknown, Act.Attacked, PersonOrThing.Of(_jory.Id), _tavern.Id, tick: -5 * 3_600, severity: 0.5, blame: 0, witness: null);
        _mara.Believe(listenerKnowsTheActor ? unknown : known, confidence: 0.9, importance: 0.8);
        Belief own = _pell.Believe(listenerKnowsTheActor ? known : unknown, confidence: 0.4, importance: 0.5);
        Claim kept = own.Claim;

        Tell(Tavern(new FixedRandom(0)), _mara, _pell);

        own.Claim.ShouldBe(kept);
        own.Confidence.ShouldBeGreaterThan(0.4);
    }

    [Fact]
    public void End_AWitnessHearingTheSameStory_StaysAsTheyWere()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        Relation pellToMara = _pell.Meet(_mara.Id, opinion: 0.2, trust: 1);
        _mara.Believe(BramAttackedJory(witness: null), confidence: 1, importance: 0.8);
        Belief seen = _pell.Believe(BramAttackedJory(witness: _pell.Id), confidence: 0.1, importance: 0.5);

        ConversationTrace trace = Traced(Tavern(new FixedRandom(0)), _mara, _pell);

        trace.Hearing.ShouldBe(Hearing.KeptOwn);
        (seen.Claim, seen.Confidence, seen.Relevance).ShouldBe((BramAttackedJory(witness: _pell.Id), 0.1, 0.5));
        pellToMara.Trust.ShouldBe(1);
    }

    [Fact]
    public void End_AWitness_KeepsWhatTheySaw_AndTrustsLessWhoeverContradictsIt()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        Relation pellToMara = _pell.Meet(_mara.Id, opinion: 0.2, trust: 1);
        _mara.Believe(FenAttackedJory(), confidence: 1, importance: 0.8);
        Belief seen = _pell.Believe(BramAttackedJory(witness: _pell.Id), confidence: 0.1, importance: 0.5);

        // Mara's version would win against a hearsay holder this unsure; Pell saw it.
        ConversationTrace trace = Traced(Tavern(new FixedRandom(0)), _mara, _pell);

        trace.Hearing.ShouldBe(Hearing.ThinksLying);
        (seen.Claim, seen.Confidence).ShouldBe((BramAttackedJory(witness: _pell.Id), 0.1));
        pellToMara.Trust.ShouldBe(1 - Telling.LiarTrustDrop, _tolerance);
    }

    [Fact]
    public void BeginAndEnd_BothRememberTheTelling_AndTheTellerHasNothingNewForTheListenerAfterwards()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        Belief story = _mara.Believe(BramAttackedJory(witness: _mara.Id), confidence: 0.9, importance: 0.8);
        World world = Tavern(new FixedRandom(0));

        Tell(world, _mara, _pell);

        // She colored it toward harsher blame, so what he heard is not what she holds.
        ToldEntry told = story.ToldTo.ShouldHaveSingleItem();
        told.ShouldBe(new ToldEntry(_pell.Id, Tick: _heardOut, story.Claim, _pell.Beliefs.Single().Claim));
        told.Told.ShouldNotBe(story.Claim);
        _pell.Beliefs.Single().HeardFrom.ShouldBe([new HeardEntry(_mara.Id, Tick: _heardOut, told.Told)]);

        Telling.Begin(world, _mara, [_pell], _log).ShouldBeNull();
    }

    [Fact]
    public void Begin_ATellerWhoDeclinesAStranger_TellsSomeoneTheyKnow()
    {
        Person fen = Someone(4, "Fen", _tavern);
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        _mara.Believe(BramAttackedJory(witness: _mara.Id), confidence: 0.9, importance: 0.8);
        var world = new World(seed: 0, new ScriptedRandom(0, 0.99, 0, 0, 0), [_tavern, _ship], [_mara, _pell, _bram, _jory, fen]);

        Conversation conversation = Telling.Begin(world, _mara, [_pell, fen], _log).ShouldNotBeNull();

        conversation.Listener.ShouldBe(_pell);
        conversation.IsStranger.ShouldBeFalse();
        _mara.RelationTo(fen.Id).ShouldBeNull();
    }

    [Fact]
    public void Color_AHaterRaisesBlame_AndALikerOfTheTargetRaisesSeverity()
    {
        Claim claim = BramAttackedJory(witness: null);
        Person fen = Someone(4, "Fen", _tavern, (TraitAxis.Creative, 1), (TraitAxis.Honest, -1));
        fen.Meet(_bram.Id, opinion: -1, trust: 0.5);
        fen.Meet(_jory.Id, opinion: 1, trust: 0.5);

        Claim colored = Telling.Color(fen, claim, harsherBlame: false).Told;
        colored.Severity.ShouldBeGreaterThan(claim.Severity);
        colored.Blame.ShouldBeGreaterThan(claim.Blame);
        Telling.Color(fen, claim, harsherBlame: true).Told.Blame.ShouldBeGreaterThan(colored.Blame);

        // Someone who knows neither changes nothing without the harsher pull.
        Claim plain = Telling.Color(_pell, claim, harsherBlame: false).Told;
        plain.Severity.ShouldBe(claim.Severity);
        plain.Blame.ShouldBe(claim.Blame);
    }

    [Fact]
    public void Color_KeepsDialsInRange_AndLeavesHarmlessActsBlameless()
    {
        Person fen = Someone(4, "Fen", _tavern, (TraitAxis.Creative, 1), (TraitAxis.Honest, -1));
        fen.Meet(_bram.Id, opinion: 1, trust: 0.5);
        fen.Meet(_jory.Id, opinion: -1, trust: 0.5);
        var helped = new Claim(
            PersonOrThing.Of(_bram.Id), Act.Helped, PersonOrThing.Of(_jory.Id), _tavern.Id, tick: -5 * 3_600, severity: 0, blame: 0, witness: null);
        var excused = new Claim(
            PersonOrThing.Of(_bram.Id), Act.Attacked, PersonOrThing.Of(_jory.Id), _tavern.Id, tick: -5 * 3_600, severity: 0.1, blame: Claim.HarmfulBlame.Min, witness: null);

        Claim coloredHelp = Telling.Color(fen, helped, harsherBlame: true).Told;
        coloredHelp.Severity.ShouldBe(0);
        coloredHelp.Blame.ShouldBe(0);
        Telling.Color(fen, excused, harsherBlame: false).Told.Blame.ShouldBe(Claim.HarmfulBlame.Min);

        // A hater of the actor who likes the victim tops out both dials.
        fen.RelationTo(_bram.Id)!.Opinion = -1;
        fen.RelationTo(_jory.Id)!.Opinion = 1;
        var atrocious = new Claim(
            PersonOrThing.Of(_bram.Id), Act.Attacked, PersonOrThing.Of(_jory.Id), _tavern.Id, tick: -5 * 3_600, severity: 1, blame: Claim.HarmfulBlame.Max, witness: null);
        Claim worse = Telling.Color(fen, atrocious, harsherBlame: true).Told;
        worse.Severity.ShouldBe(1);
        worse.Blame.ShouldBe(Claim.HarmfulBlame.Max);
    }

    [Theory]
    [InlineData(0.0, 0.0, "teller")]
    [InlineData(0.99, 0.0, "witness")]
    [InlineData(0.99, 0.99, "none")]
    public void RetoldWitness_ClaimsIt_KeepsIt_OrDropsIt_ToHearsay(double claimSample, double keepSample, string expected)
    {
        Claim heard = BramAttackedJory(witness: _jory.Id);

        PersonId? witness = Telling.RetoldWitness(_mara, heard, new ScriptedRandom(claimSample, keepSample), []);

        witness.ShouldBe(expected switch
        {
            "teller" => _mara.Id,
            "witness" => _jory.Id,
            _ => (PersonId?)null,
        });
    }

    [Fact]
    public void RetoldWitness_AWitnessKeepsThemself_AndHearsayWithoutOneDrawsOnlyTheClaim()
    {
        Telling.RetoldWitness(_mara, BramAttackedJory(witness: _mara.Id), new ScriptedRandom(), []).ShouldBe(_mara.Id);
        Telling.RetoldWitness(_mara, BramAttackedJory(witness: null), new ScriptedRandom(0.99), []).ShouldBeNull();
    }

    [Fact]
    public void BeginAndEnd_Traced_AStrangerMetAndBelieved_RecordsEveryRollAndWhatChanged()
    {
        _mara.Believe(BramAttackedJory(witness: null), confidence: 0.9, importance: 0.8);

        // Every draw says yes: Mara approaches Pell, colors harsher and claims she saw it, and Pell believes her.
        ConversationTrace trace = Traced(Tavern(new FixedRandom(0)), _mara, _pell);

        (trace.StartTick, trace.EndTick).ShouldBe((0, _heardOut));
        trace.Talk.Yes.ShouldBeTrue();
        trace.ApproachStranger.ShouldNotBeNull().Yes.ShouldBeTrue();
        trace.KnownListeners.ShouldBeEmpty();
        trace.ListenerRoll.ShouldBeNull();
        TopicOption topic = trace.Topics.ShouldHaveSingleItem();
        topic.Chance.ShouldBe(1, _tolerance);
        trace.Held.ShouldBe(BramAttackedJory(witness: null));
        trace.Told.Witness.ShouldBe(_mara.Id);
        trace.HarsherBlame.ShouldNotBeNull().Yes.ShouldBeTrue();
        trace.ClaimWitnessed.ShouldNotBeNull().Yes.ShouldBeTrue();
        trace.KeepWitness.ShouldBeNull();
        trace.Believe.ShouldNotBeNull().Yes.ShouldBeTrue();
        trace.Ended.Outcome.ShouldBeOfType<HearingOutcome.Believed>().Confidence.ShouldBe(trace.Offered.Value);

        // Before is before the introduction; after holds the new relations and the belief.
        trace.TellerBefore.RelationTo(_pell.Id).ShouldBeNull();
        trace.ListenerBefore.RelationTo(_mara.Id).ShouldBeNull();
        trace.ListenerBefore.Beliefs.ShouldBeEmpty();
        trace.TellerAfter.RelationTo(_pell.Id).ShouldNotBeNull().Impressions.ShouldNotBeEmpty();
        trace.ListenerAfter.RelationTo(_mara.Id).ShouldNotBeNull().Impressions.ShouldNotBeEmpty();
        trace.ListenerAfter.Beliefs.ShouldHaveSingleItem().Claim.ShouldBe(trace.Told);

        // The log holds what the teller told and how they stood with the listener as it ended.
        ConversationEnded ended = LastEnded();
        ended.ShouldBeSameAs(trace.Ended);
        Belief told = _mara.Beliefs.Single();
        Relation maraToPell = _mara.RelationTo(_pell.Id).ShouldNotBeNull();
        (ended.Teller, ended.Listener, ended.IsStranger, ended.Told, ended.Confidence, ended.Relevance, ended.Opinion, ended.Trust)
            .ShouldBe((_mara.Id, _pell.Id, true, trace.Told, told.Confidence, told.Relevance, maraToPell.Opinion, maraToPell.Trust));

        // Each side's pulls are their own traits: Mara has only Talkative, and Pell only Adaptable.
        ended.Pulls.ShouldNotBeEmpty();
        ended.Pulls.ShouldAllBe(pull => pull.Value == _mara.Traits[pull.Axis]);
        ValueList<TraitValue> believing = ended.Outcome.ShouldBeOfType<HearingOutcome.Believed>().Pulls;
        believing.ShouldNotBeEmpty();
        believing.ShouldAllBe(pull => pull.Value == _pell.Traits[pull.Axis]);
    }

    [Fact]
    public void BeginAndEnd_Traced_EachRoll_RecordsItsContextAndWhoseTraits_AndTopicsTheScoresTheyWereDrawnBy()
    {
        Person ilse = Someone(
            0, "Ilse", _tavern, (TraitAxis.Talkative, 0.6), (TraitAxis.Confident, 0.4), (TraitAxis.Curious, -0.3), (TraitAxis.Kind, -0.5), (TraitAxis.Honest, -0.7));
        ilse.Believe(BramAttackedJory(witness: null), confidence: 0.9, importance: 0.8);
        var seen = new Claim(PersonOrThing.Of(_bram.Id), Act.WasSeenAt, PersonOrThing.Of(_tavern.Id), _tavern.Id, tick: -5 * 3_600, severity: 0, blame: 0, witness: null);
        ilse.Believe(seen, confidence: 0.9, importance: 0.3);

        // Every draw says yes: Ilse approaches Pell, colors harsher and claims she saw it, and Pell believes her.
        ConversationTrace trace = Traced(new World(seed: 0, new FixedRandom(0), [_tavern, _ship], [ilse, _pell, _bram, _jory, _fen]), ilse, _pell);

        double trust = trace.ListenerAfter.RelationTo(ilse.Id).ShouldNotBeNull().Trust;
        Roll[] rolls = [trace.Talk, trace.ApproachStranger!.Value, trace.HarsherBlame!.Value, trace.ClaimWitnessed!.Value, trace.Believe!.Value];
        rolls.Select(roll => roll.Context).ShouldBe([0, Telling.ApproachStrangerContext, 0, Telling.ClaimWitnessedContext, (2 * trust) - 1]);

        // The teller decides all but believing, which is the listener's.
        rolls.Select(roll => roll.Traits).ShouldBe([ilse.Traits, ilse.Traits, ilse.Traits, ilse.Traits, _pell.Traits]);
        trace.Topics.Select(topic => topic.Chance).ShouldBe(TraitEffects.Chances([.. trace.Topics.Select(topic => topic.Score)]), _tolerance);
    }

    [Fact]
    public void BeginAndEnd_Traced_AKnownListenerWhoHoldsIt_RollsNothingThatWasNotDrawn()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        _mara.Believe(BramAttackedJory(witness: _mara.Id), confidence: 0.9, importance: 0.8);
        _pell.Believe(BramAttackedJory(witness: null), confidence: 0.5, importance: 0.5);

        // Talk, the listener, the topic and harsher blame: Mara saw it, and Pell already holds it.
        ConversationTrace trace = Traced(Tavern(new ScriptedRandom(0, 0, 0, 0)), _mara, _pell);

        trace.ApproachStranger.ShouldBeNull();
        ListenerOption known = trace.KnownListeners.ShouldHaveSingleItem();
        (known.Person, known.Chance).ShouldBe((_pell.Id, 1));
        trace.ListenerRoll.ShouldBe(0);
        trace.ClaimWitnessed.ShouldBeNull();
        trace.KeepWitness.ShouldBeNull();
        trace.Believe.ShouldBeNull();
        trace.Hearing.ShouldBe(Hearing.Surer);
        BeliefSnapshot before = trace.ListenerBefore.Beliefs.ShouldHaveSingleItem();
        BeliefSnapshot after = trace.ListenerAfter.Beliefs.ShouldHaveSingleItem();
        after.Claim.ShouldBe(before.Claim);
        after.Confidence.ShouldBeGreaterThan(before.Confidence);
        before.HeardFrom.ShouldBeEmpty();
        after.HeardFrom.ShouldBe([new HeardEntry(_mara.Id, Tick: _heardOut, trace.Told)]);
        trace.ListenerAfter.RelationTo(_mara.Id).ShouldNotBeNull().Opinion.ShouldBe(0.2);
    }

    [Fact]
    public void BeginAndEnd_Traced_AHeardWitnessKept_AndABlamelessStoryDisbelieved()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        var seen = new Claim(PersonOrThing.Of(_bram.Id), Act.WasSeenAt, PersonOrThing.Of(_tavern.Id), _tavern.Id, tick: -5 * 3_600, severity: 0, blame: 0, _jory.Id);
        _mara.Believe(seen, confidence: 0.9, importance: 0.8);

        // Talk, the listener, the topic; Mara doesn't claim she saw it but keeps Jory, and Pell doesn't believe her.
        ConversationTrace trace = Traced(Tavern(new ScriptedRandom(0, 0, 0, 0.99, 0, 0.99)), _mara, _pell);

        trace.HarsherBlame.ShouldBeNull();
        trace.ClaimWitnessed.ShouldNotBeNull().Yes.ShouldBeFalse();
        trace.KeepWitness.ShouldNotBeNull().Yes.ShouldBeTrue();
        trace.Told.Witness.ShouldBe(_jory.Id);
        trace.Believe.ShouldNotBeNull().Yes.ShouldBeFalse();
        trace.Hearing.ShouldBe(Hearing.NotBelieved);
        trace.ListenerAfter.Beliefs.ShouldBeEmpty();
        _log.Records[^1].ShouldBeSameAs(trace.Ended);
    }

    [Fact]
    public void BeginAndEnd_Traced_RecordsEachColoringPart_WhichSumToEachDialsChange_AndTheConfidenceTheListenerTakes()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _mara.Meet(_bram.Id, opinion: -0.5, trust: 0.5);
        _mara.Meet(_jory.Id, opinion: 0.5, trust: 0.5);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        _mara.Believe(BramAttackedJory(witness: _mara.Id), confidence: 0.9, importance: 0.8);

        // Every draw says yes: harsher blame, and Pell believes it.
        ConversationTrace trace = Traced(Tavern(new FixedRandom(0)), _mara, _pell);

        Coloring coloring = trace.Coloring;
        (coloring.TargetSource, coloring.TargetOpinion, coloring.ActorSource, coloring.ActorOpinion, coloring.Harsher)
            .ShouldBe((OpinionSource.Known, 0.5, OpinionSource.Known, -0.5, true));
        (coloring.SeverityCapped, coloring.BlameCapped).ShouldBe((false, false));
        trace.Told.Severity.ShouldBe(trace.Held.Severity + coloring.SeverityPart, _tolerance);
        trace.Told.Blame.ShouldBe(trace.Held.Blame + coloring.BlamePart + coloring.HarsherPart, _tolerance);

        trace.Offered.ShouldBe(new ListenerConfidence(0.9, 0.6));
        trace.ListenerAfter.Beliefs.ShouldHaveSingleItem().Confidence.ShouldBe(trace.Offered.Value, _tolerance);
    }

    [Fact]
    public void BeginAndEnd_Traced_ADialPastTheEndOfItsRange_IsRecordedAsCapped()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _mara.Meet(_bram.Id, opinion: -1, trust: 0.5);
        _mara.Meet(_jory.Id, opinion: 1, trust: 0.5);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        var atrocious = new Claim(
            PersonOrThing.Of(_bram.Id), Act.Attacked, PersonOrThing.Of(_jory.Id), _tavern.Id, tick: -5 * 3_600, severity: 1, blame: 1, witness: _mara.Id);
        _mara.Believe(atrocious, confidence: 0.9, importance: 0.8);

        ConversationTrace capped = Traced(Tavern(new FixedRandom(0)), _mara, _pell);

        (capped.Coloring.SeverityCapped, capped.Coloring.BlameCapped).ShouldBe((true, true));
        (capped.Told.Severity, capped.Told.Blame).ShouldBe((1, Claim.HarmfulBlame.Max));
    }

    [Fact]
    public void End_AStoryThatDoesNotMatterToTheListener_IsBelievedAsAMemoryAtOnce()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        var seen = new Claim(PersonOrThing.Of(_bram.Id), Act.WasSeenAt, PersonOrThing.Of(_tavern.Id), _tavern.Id, tick: -5 * 3_600, severity: 0, blame: 0, witness: null);
        _mara.Believe(seen, confidence: 0.9, importance: 0.8);

        Tell(Tavern(new FixedRandom(0)), _mara, _pell);

        _pell.Beliefs.ShouldHaveSingleItem().IsMemory.ShouldBeTrue();
        LastEnded().Outcome.ShouldBeOfType<HearingOutcome.Believed>().IsMemory.ShouldBeTrue();
    }

    [Fact]
    public void BeginAndEnd_AStoryAboutTheDead_IsToldAndBelieved()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        _mara.Believe(BramAttackedJory(witness: _mara.Id), confidence: 0.9, importance: 0.8);
        World world = Tavern(new FixedRandom(0));
        world.Kill(_jory.Id);

        Tell(world, _mara, _pell);

        _pell.Beliefs.ShouldHaveSingleItem().Claim.Target.ShouldBe(PersonOrThing.Of(_jory.Id));
    }

    [Fact]
    public void Begin_Untraced_KeepsNoTrace()
    {
        _mara.Believe(BramAttackedJory(witness: null), confidence: 0.9, importance: 0.8);

        Telling.Begin(Tavern(new FixedRandom(0)), _mara, [_pell], _log).ShouldNotBeNull().Trace.ShouldBeNull();
    }

    private static Person Someone(int id, string name, Thing place, params (TraitAxis Axis, double Value)[] traits) =>
        new(new PersonId(id), name, new TraitProfile(traits.ToDictionary(trait => trait.Axis, trait => trait.Value)), place.Id);

    private Claim BramAttackedJory(PersonId? witness) =>
        new(PersonOrThing.Of(_bram.Id), Act.Attacked, PersonOrThing.Of(_jory.Id), _tavern.Id, tick: -5 * 3_600, severity: 0.5, blame: 0.2, witness);

    // The competing version: another known actor for the same act and target.
    private Claim FenAttackedJory() =>
        new(PersonOrThing.Of(_fen.Id), Act.Attacked, PersonOrThing.Of(_jory.Id), _tavern.Id, tick: -5 * 3_600, severity: 0.5, blame: 0.2, witness: null);

    // Begins at tick 0 and hears it out at _heardOut; the teller must have something to say.
    private void Tell(World world, Person teller, params Person[] free)
    {
        Conversation conversation = Telling.Begin(world, teller, free, _log).ShouldNotBeNull();
        world.AdvanceTo(_heardOut, _log);
        Telling.End(world, conversation, _log);
    }

    private ConversationEnded LastEnded() => _log.Records[^1].ShouldBeOfType<ConversationEnded>();

    private ConversationTrace Traced(World world, Person teller, params Person[] free)
    {
        ConversationTrace? traced = null;
        world.TraceConversations(trace => traced = trace);
        Tell(world, teller, free);
        return traced.ShouldNotBeNull();
    }

    private World Tavern(Random random) => new(seed: 0, random, [_tavern, _ship], [_mara, _pell, _bram, _jory, _fen]);
}
