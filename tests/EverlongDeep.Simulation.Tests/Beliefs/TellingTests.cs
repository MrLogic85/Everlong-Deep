using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public class TellingTests
{
    private const double _tolerance = 1e-9;

    private static readonly Thing _tavern = new(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);
    private static readonly Thing _ship = new(new ThingId(1), "Saltwren", ThingKind.Ship, 0.5);

    private readonly SimulationLog _log = new();

    // Bram and Jory are aboard the Saltwren, so only Mara and Pell can talk in the tavern.
    private readonly Person _mara = Someone(0, "Mara", _tavern, (TraitAxis.Talkative, 0.6));
    private readonly Person _pell = Someone(1, "Pell", _tavern, (TraitAxis.Adaptable, 0.6));
    private readonly Person _bram = Someone(2, "Bram", _ship);
    private readonly Person _jory = Someone(3, "Jory", _ship);

    [Fact]
    public void StepHour_ABeliefReachesSomeoneWhoDidNotHoldIt_AsTheirOwn_ColoredAndWeightedByTrust()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _mara.Meet(_bram.Id, opinion: -1, trust: 0.5);
        _mara.Meet(_jory.Id, opinion: 0.8, trust: 0.5);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        _mara.Believe(BramAttackedJory(witness: _mara.Id), confidence: 0.9, relevance: 0.8);
        World world = Tavern(new FixedRandom(0));

        world.StepHour(_log);

        // Every draw says yes: Mara colors toward harsher blame and keeps herself as the witness.
        Belief heard = _pell.Beliefs.ShouldHaveSingleItem();
        heard.Holder.ShouldBe(_pell.Id);
        heard.Claim.Witness.ShouldBe(_mara.Id);
        heard.IsFirstHand.ShouldBeFalse();
        heard.Confidence.ShouldBe(0.9 * 0.6, _tolerance);
        heard.Relevance.ShouldBe(heard.Claim.Severity, _tolerance);
        _log.Lines.ShouldBe(
        [
            "Hour 1: Mara tells Pell (opinion 0.40, trust 0.60): Bram attacked Jory at The Drowned Lantern, Mara saw it " +
            "(confidence 0.90, severity 0.62, blame 0.50); why: relevance 0.80; pulls talkative; " +
            "Pell believes it (confidence 0.54; adaptable)",
        ]);
    }

    [Fact]
    public void StepHour_ABeliefFormedThisHour_IsNotRetoldUntilTheNext()
    {
        // Mara knows both; Fen is a stranger to Pell, so Pell, who can't tell Mara her own story, approaches Fen.
        Person fen = Someone(4, "Fen", _tavern);
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _mara.Meet(fen.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        fen.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        _mara.Believe(BramAttackedJory(witness: null), confidence: 0.9, relevance: 0.8);
        var world = new World(seed: 0, new FixedRandom(0), [_tavern, _ship], [_mara, _pell, _bram, _jory, fen]);

        world.StepHour(_log);
        _log.Lines.ShouldHaveSingleItem().ShouldStartWith("Hour 1: Mara tells Pell");

        world.StepHour(_log);
        _log.Lines.Skip(1).Select(line => line[..line.IndexOf(" (", StringComparison.Ordinal)])
            .ShouldBe(["Hour 2: Mara tells Pell", "Hour 2: Pell tells Fen"]);
    }

    [Fact]
    public void StepHour_AStrangerTold_BecomesKnownBothWays()
    {
        _mara.Believe(BramAttackedJory(witness: null), confidence: 0.9, relevance: 0.8);
        World world = Tavern(new FixedRandom(0));

        world.StepHour(_log);

        _mara.RelationTo(_pell.Id).ShouldNotBeNull().Impressions.ShouldNotBeEmpty();
        _pell.RelationTo(_mara.Id).ShouldNotBeNull().Impressions.ShouldNotBeEmpty();
        _pell.Beliefs.ShouldHaveSingleItem();
        _log.Lines.ShouldHaveSingleItem().ShouldStartWith("Hour 1: Mara tells Pell (a stranger met, ");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StepHour_NobodyIsToldTheirOwnStory_OrWhatTheySaw(bool bramSawIt)
    {
        // Bram ashore, in the tavern with Mara, who knows only a story he is in or saw.
        Person bram = Someone(2, "Bram", _tavern);
        _mara.Meet(bram.Id, opinion: 0.4, trust: 0.6);
        bram.Meet(_mara.Id, opinion: 0.4, trust: 0.6);
        Claim claim = bramSawIt
            ? new Claim(PersonOrThing.Of(_jory.Id), Act.Sank, PersonOrThing.Of(_ship.Id), _ship.Id, hour: -5, severity: 0.5, blame: 0.2, bram.Id)
            : BramAttackedJory(witness: null);
        _mara.Believe(claim, confidence: 0.9, relevance: 0.8);
        var world = new World(seed: 0, new FixedRandom(0), [_tavern, _ship], [_mara, bram, _jory]);

        world.StepHour(_log);

        bram.Beliefs.ShouldBeEmpty();
        _log.Lines.ShouldBeEmpty();
    }

    [Fact]
    public void StepHour_AListenerWhoDoesNotBelieveTheTeller_FormsNothing()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.1);
        _mara.Believe(BramAttackedJory(witness: _mara.Id), confidence: 0.9, relevance: 0.8);

        // Talk, listener, topic and harsher blame say yes; believing, at chance 0.38 from trust 0.1, says no.
        Tavern(new ScriptedRandom(0, 0, 0, 0, 0.99)).StepHour(_log);

        _pell.Beliefs.ShouldBeEmpty();
        _log.Lines.ShouldHaveSingleItem().ShouldEndWith("; Pell doesn't believe it (adaptable)");
    }

    [Fact]
    public void StepHour_AListenerWhoAlreadyHoldsTheStory_KeepsTheirOwn_AndDrawsNothing()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        _mara.Believe(BramAttackedJory(witness: _mara.Id), confidence: 0.9, relevance: 0.8);
        Belief own = _pell.Believe(BramAttackedJory(witness: _mara.Id), confidence: 0.4, relevance: 0.5);

        // Pell talks and picks Mara too, but his only story is one she saw, so he has nothing to tell her.
        Tavern(new ScriptedRandom(0, 0, 0, 0, 0, 0)).StepHour(_log);

        _pell.Beliefs.ShouldBe([own]);
        own.Confidence.ShouldBe(0.4);
        _log.Lines.ShouldHaveSingleItem().ShouldEndWith("; Pell already holds it");
    }

    [Fact]
    public void StepHour_ATellerWhoDeclinesAStranger_TellsSomeoneTheyKnow()
    {
        Person fen = Someone(4, "Fen", _tavern);
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        _mara.Believe(BramAttackedJory(witness: _mara.Id), confidence: 0.9, relevance: 0.8);
        var world = new World(seed: 0, new ScriptedRandom(0, 0.99, 0, 0, 0, 0), [_tavern, _ship], [_mara, _pell, _bram, _jory, fen]);

        world.StepHour(_log);

        _mara.RelationTo(fen.Id).ShouldBeNull();
        _pell.Beliefs.ShouldHaveSingleItem();
        _log.Lines.ShouldHaveSingleItem().ShouldStartWith("Hour 1: Mara tells Pell (opinion ");
    }

    [Fact]
    public void StepHour_TheMoreRelevantBelief_IsTheLikelierTopic()
    {
        _mara.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(_mara.Id, opinion: 0.2, trust: 0.6);
        _mara.Believe(BramAttackedJory(witness: null), confidence: 0.9, relevance: 1);
        _mara.Believe(
            new Claim(PersonOrThing.Of(_jory.Id), Act.Helped, PersonOrThing.Of(_bram.Id), _tavern.Id, hour: -5, severity: 0.1, blame: 0, witness: null),
            confidence: 0.9, relevance: 0);

        // Scores 2 and 0 give the first a 0.88 share, so a roll of 0.8 picks it where even shares would not.
        // Then: harsher blame yes, "I saw it myself" no, believe yes.
        Tavern(new ScriptedRandom(0, 0, 0.8, 0, 0.99, 0)).StepHour(_log);

        _pell.Beliefs.ShouldHaveSingleItem().Claim.Act.ShouldBe(Act.Attacked);
    }

    [Fact]
    public void StepHour_AnUnkindPessimist_LeansToTheScandal()
    {
        Person fen = Someone(4, "Fen", _tavern, (TraitAxis.Kind, -1), (TraitAxis.Optimistic, -1));
        fen.Meet(_pell.Id, opinion: 0.4, trust: 0.6);
        _pell.Meet(fen.Id, opinion: 0.2, trust: 0.6);
        fen.Believe(
            new Claim(PersonOrThing.Of(_jory.Id), Act.Helped, PersonOrThing.Of(_bram.Id), _tavern.Id, hour: -5, severity: 0.5, blame: 0, witness: null),
            confidence: 0.9, relevance: 0.5);
        fen.Believe(BramAttackedJory(witness: null), confidence: 0.9, relevance: 0.5);

        // The scandal's pull of 1.2 gives it a 0.77 share, so a topic roll of 0.4 picks it where even shares would not.
        // Then: harsher blame yes, "I saw it myself" no, believe yes.
        var world = new World(seed: 0, new ScriptedRandom(0, 0, 0.4, 0, 0.99, 0), [_tavern, _ship], [_pell, _bram, _jory, fen]);
        world.StepHour(_log);

        _pell.Beliefs.ShouldHaveSingleItem().Claim.Act.ShouldBe(Act.Attacked);
        _log.Lines.ShouldHaveSingleItem().ShouldContain("pulls unkind, pessimistic");
    }

    [Fact]
    public void Colored_AHaterRaisesBlame_AndALikerOfTheTargetRaisesSeverity_AsFarAsTheirPullTakesThem()
    {
        Claim claim = BramAttackedJory(witness: null);
        Person fen = Someone(4, "Fen", _tavern, (TraitAxis.Creative, 1), (TraitAxis.Honest, -1));
        fen.Meet(_bram.Id, opinion: -1, trust: 0.5);
        fen.Meet(_jory.Id, opinion: 1, trust: 0.5);
        double reach = Telling.ColorReach * TraitEffects.Chance(1.2);

        Claim colored = Telling.Colored(fen, claim, harsherBlame: false);
        colored.Severity.ShouldBe(claim.Severity + reach, _tolerance);
        colored.Blame.ShouldBe(claim.Blame + reach, _tolerance);
        Telling.Colored(fen, claim, harsherBlame: true).Blame.ShouldBe(claim.Blame + (2 * reach), _tolerance);

        // Someone who knows neither changes nothing without the harsher pull.
        Claim plain = Telling.Colored(_pell, claim, harsherBlame: false);
        plain.Severity.ShouldBe(claim.Severity);
        plain.Blame.ShouldBe(claim.Blame);
    }

    [Fact]
    public void Colored_KeepsDialsInRange_AndLeavesHarmlessActsBlameless()
    {
        Person fen = Someone(4, "Fen", _tavern, (TraitAxis.Creative, 1), (TraitAxis.Honest, -1));
        fen.Meet(_bram.Id, opinion: 1, trust: 0.5);
        fen.Meet(_jory.Id, opinion: -1, trust: 0.5);
        var helped = new Claim(
            PersonOrThing.Of(_bram.Id), Act.Helped, PersonOrThing.Of(_jory.Id), _tavern.Id, hour: -5, severity: 0.1, blame: 0, witness: null);
        var excused = new Claim(
            PersonOrThing.Of(_bram.Id), Act.Attacked, PersonOrThing.Of(_jory.Id), _tavern.Id, hour: -5, severity: 0.1, blame: -0.1, witness: null);

        Claim coloredHelp = Telling.Colored(fen, helped, harsherBlame: true);
        coloredHelp.Severity.ShouldBe(0);
        coloredHelp.Blame.ShouldBe(0);
        Telling.Colored(fen, excused, harsherBlame: false).Blame.ShouldBe(Claim.HarmfulBlame.Min);

        // A hater of the actor who likes the victim tops out both dials.
        fen.RelationTo(_bram.Id)!.Opinion = -1;
        fen.RelationTo(_jory.Id)!.Opinion = 1;
        var atrocious = new Claim(
            PersonOrThing.Of(_bram.Id), Act.Attacked, PersonOrThing.Of(_jory.Id), _tavern.Id, hour: -5, severity: 0.95, blame: 0.95, witness: null);
        Claim worse = Telling.Colored(fen, atrocious, harsherBlame: true);
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
    public void StepHour_SameSeed_GivesTheSameTalk()
    {
        static string[] Talk(int seed)
        {
            var log = new SimulationLog();
            World world = WorldBuilder.Tavern(seed, 8, log);
            for (int hour = 0; hour < 48; hour++)
            {
                world.StepHour(log);
            }

            return [.. log.Lines.Where(line => line.Contains(" tells "))];
        }

        string[] first = Talk(7);
        first.ShouldNotBeEmpty();
        first.ShouldContain(line => line.Contains("a stranger met"));
        Talk(7).ShouldBe(first);
    }

    private static Person Someone(int id, string name, Thing place, params (TraitAxis Axis, double Value)[] traits) =>
        new(new PersonId(id), name, new TraitProfile(traits.ToDictionary(trait => trait.Axis, trait => trait.Value)), place.Id);

    private Claim BramAttackedJory(PersonId? witness) =>
        new(PersonOrThing.Of(_bram.Id), Act.Attacked, PersonOrThing.Of(_jory.Id), _tavern.Id, hour: -5, severity: 0.5, blame: 0.2, witness);

    private World Tavern(Random random) => new(seed: 0, random, [_tavern, _ship], [_mara, _pell, _bram, _jory]);
}
