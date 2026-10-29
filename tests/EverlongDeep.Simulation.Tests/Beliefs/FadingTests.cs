using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public class FadingTests
{
    private static readonly Thing _tavern = new(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);

    private readonly SimulationLog _log = new();
    private readonly Person _mara = Someone(0, "Mara");
    private readonly Person _bram = Someone(1, "Bram");
    private readonly Person _jory = Someone(2, "Jory");
    private readonly Person _pell = Someone(3, "Pell");

    [Fact]
    public void StepHour_AnUnimportantStory_BecomesAMemoryBeforeAnImportantOne_LoggedOnce()
    {
        Belief idle = _mara.Believe(BramHelpedJory(), confidence: 0.5, importance: 0.1);
        Belief gripping = _mara.Believe(BramAttackedJory(), confidence: 0.5, importance: 0.9);
        World world = Tavern();

        Fading.StepHour(world, _log);
        double idleLoss = 0.1 - idle.Relevance;
        double grippingLoss = 0.9 - gripping.Relevance;
        grippingLoss.ShouldBeGreaterThan(0);
        idleLoss.ShouldBeGreaterThan(grippingLoss);
        _log.Lines.ShouldBeEmpty();

        Fading.StepHour(world, _log);
        Fading.StepHour(world, _log);
        idle.IsMemory.ShouldBeTrue();
        idle.Relevance.ShouldBe(0);
        gripping.IsMemory.ShouldBeFalse();
        _log.Lines.ShouldBe(
        [
            "00:00 Mara no longer talks of it, a memory: Bram helped Jory at The Drowned Lantern (importance 0.10, confidence 0.50)",
        ]);

        // A memory is still held, and the important story fades on alone.
        for (int hour = 0; hour < 100; hour++)
        {
            Fading.StepHour(world, _log);
        }

        _mara.Beliefs.ShouldBe([idle, gripping]);
        gripping.Relevance.ShouldBe(0);
        _log.Lines.Count.ShouldBe(2);
        _log.Lines[1].ShouldStartWith("00:00 Mara no longer talks of it, a memory: Bram attacked Jory");
    }

    [Fact]
    public void WorldStepHour_FadesEveryBelief_AtTheHoursEnd()
    {
        Belief idle = _mara.Believe(BramHelpedJory(), confidence: 0.5, importance: 0.04);
        World world = Tavern();

        world.StepHour(_log);

        idle.IsMemory.ShouldBeTrue();
        _log.Lines.ShouldBe(
        [
            "01:00 Mara no longer talks of it, a memory: Bram helped Jory at The Drowned Lantern (importance 0.04, confidence 0.50)",
        ]);
    }

    [Fact]
    public void Begin_ATellerWithOnlyMemories_KeepsQuiet_WithoutADraw()
    {
        _mara.Meet(_pell.Id, opinion: 0.5, trust: 0.5);
        _mara.Believe(BramHelpedJory(), confidence: 0.9, importance: 0.5).Relevance = 0;

        Telling.Begin(new World(seed: 0, new ScriptedRandom(), [_tavern], [_mara, _bram, _jory, _pell]), _mara, [_pell]).ShouldBeNull();
    }

    [Fact]
    public void Begin_AMemory_IsNeverPickedAsATopic()
    {
        _mara.Meet(_pell.Id, opinion: 0.5, trust: 0.5);
        _mara.Believe(BramHelpedJory(), confidence: 0.9, importance: 0.5).Relevance = 0;
        Belief told = _mara.Believe(BramAttackedJory(), confidence: 0.9, importance: 0.1);

        Conversation conversation = Telling.Begin(Tavern(), _mara, [_pell]).ShouldNotBeNull();

        conversation.Topic.ShouldBe(told);
    }

    private static Person Someone(int id, string name) =>
        new(new PersonId(id), name, new TraitProfile(new Dictionary<TraitAxis, double>()), _tavern.Id);

    private Claim BramHelpedJory() =>
        new(PersonOrThing.Of(_bram.Id), Act.Helped, PersonOrThing.Of(_jory.Id), _tavern.Id, hour: -5, severity: 0.1, blame: 0, witness: null);

    private Claim BramAttackedJory() =>
        new(PersonOrThing.Of(_bram.Id), Act.Attacked, PersonOrThing.Of(_jory.Id), _tavern.Id, hour: -5, severity: 0.5, blame: 0.2, witness: null);

    private World Tavern() => new(seed: 0, new FixedRandom(0), [_tavern], [_mara, _bram, _jory, _pell]);
}
