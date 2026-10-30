using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public class FadingTests
{
    private static readonly Thing _tavern = new(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);

    private const double _tolerance = 1e-12;

    private readonly SimulationLog _log = new();
    private readonly Person _mara = Someone(0, "Mara");
    private readonly Person _bram = Someone(1, "Bram");
    private readonly Person _jory = Someone(2, "Jory");
    private readonly Person _pell = Someone(3, "Pell");

    [Fact]
    public void Step_AnUnimportantStory_BecomesAMemoryBeforeAnImportantOne_LoggedOnce()
    {
        Belief idle = _mara.Believe(BramHelpedJory(), confidence: 0.5, importance: 0.1);
        Belief gripping = _mara.Believe(BramAttackedJory(), confidence: 0.5, importance: 0.9);
        World world = Tavern();

        Fading.Step(world, World.PeriodTicks, _log);
        double idleLoss = 0.1 - idle.Relevance;
        double grippingLoss = 0.9 - gripping.Relevance;
        grippingLoss.ShouldBeGreaterThan(0);
        idleLoss.ShouldBeGreaterThan(grippingLoss);

        FadeUntil(world, () => idle.IsMemory);
        idle.Relevance.ShouldBe(0);
        gripping.IsMemory.ShouldBeFalse();
        _log.Records.ShouldHaveSingleItem().ShouldBe(new BecameMemory(world.Tick, _mara.Id, idle.Claim, idle.Importance, idle.Confidence));

        // A memory is still held, and the important story fades on alone.
        FadeUntil(world, () => gripping.IsMemory);
        _mara.Beliefs.ShouldBe([idle, gripping]);
        gripping.Relevance.ShouldBe(0);
        _log.Records.Select(record => record.ShouldBeOfType<BecameMemory>().Claim).ShouldBe([idle.Claim, gripping.Claim]);
    }

    [Fact]
    public void Step_AStoryOfNoImportance_WithRelevanceLeft_StillFades()
    {
        Belief idle = _mara.Believe(BramHelpedJory(), confidence: 0.5, importance: 0);
        idle.Relevance = 0.1;
        World world = Tavern();

        FadeUntil(world, () => idle.IsMemory);

        _log.Records.ShouldHaveSingleItem().ShouldBeOfType<BecameMemory>().Claim.ShouldBe(idle.Claim);
    }

    [Fact]
    public void WorldStep_FadesOncePerPeriod_WhateverTheStepSize()
    {
        Belief idle = _mara.Believe(BramHelpedJory(), confidence: 0.5, importance: 0.5);
        double perPeriod = Fading.Rate(idle) * World.PeriodTicks;

        // Alone, Mara has nobody to talk to, so only fading changes her belief.
        var world = new World(seed: 0, new FixedRandom(0), [_tavern], [_mara]);

        world.Step(World.PeriodTicks - 1, _log);
        idle.Relevance.ShouldBe(0.5);
        world.Step(1, _log);
        idle.Relevance.ShouldBe(0.5 - perPeriod, _tolerance);

        // A step that crosses two period boundaries fades twice.
        world.Step(2 * World.PeriodTicks, _log);
        idle.Relevance.ShouldBe(0.5 - (3 * perPeriod), _tolerance);
    }

    [Fact]
    public void Step_TheDeadsBeliefsStayAsTheyWere_AndBeliefsAboutTheDeadStillFade()
    {
        Belief aboutTheDead = _mara.Believe(BramAttackedJory(), confidence: 0.5, importance: 0.5);
        Belief heldByTheDead = _jory.Believe(BramAttackedJory(), confidence: 0.5, importance: 0.5);
        World world = Tavern();
        world.Kill(_jory.Id);

        Fading.Step(world, World.PeriodTicks, _log);

        aboutTheDead.Relevance.ShouldBeLessThan(0.5);
        heldByTheDead.Relevance.ShouldBe(0.5);
    }

    [Fact]
    public void Begin_ATellerWithOnlyMemories_KeepsQuiet_WithoutADraw()
    {
        _mara.Meet(_pell.Id, opinion: 0.5, trust: 0.5);
        _mara.Believe(BramHelpedJory(), confidence: 0.9, importance: 0.5).Relevance = 0;

        Telling.Begin(new World(seed: 0, new ScriptedRandom(), [_tavern], [_mara, _bram, _jory, _pell]), _mara, [_pell], _log).ShouldBeNull();
    }

    [Fact]
    public void Begin_AMemory_IsNeverPickedAsATopic()
    {
        _mara.Meet(_pell.Id, opinion: 0.5, trust: 0.5);
        _mara.Believe(BramHelpedJory(), confidence: 0.9, importance: 0.5).Relevance = 0;
        Belief told = _mara.Believe(BramAttackedJory(), confidence: 0.9, importance: 0.1);

        Conversation conversation = Telling.Begin(Tavern(), _mara, [_pell], _log).ShouldNotBeNull();

        conversation.Topic.ShouldBe(told);
    }

    private void FadeUntil(World world, Func<bool> done)
    {
        for (int period = 0; !done(); period++)
        {
            period.ShouldBeLessThan(1000);
            Fading.Step(world, World.PeriodTicks, _log);
        }
    }

    private static Person Someone(int id, string name) =>
        new(new PersonId(id), name, new TraitProfile(new Dictionary<TraitAxis, double>()), _tavern.Id);

    private Claim BramHelpedJory() =>
        new(PersonOrThing.Of(_bram.Id), Act.Helped, PersonOrThing.Of(_jory.Id), _tavern.Id, tick: -5 * 3_600, severity: 0.1, blame: 0, witness: null);

    private Claim BramAttackedJory() =>
        new(PersonOrThing.Of(_bram.Id), Act.Attacked, PersonOrThing.Of(_jory.Id), _tavern.Id, tick: -5 * 3_600, severity: 0.5, blame: 0.2, witness: null);

    private World Tavern() => new(seed: 0, new FixedRandom(0), [_tavern], [_mara, _bram, _jory, _pell]);
}
