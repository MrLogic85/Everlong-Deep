using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.People;

public class FamiliarityTests
{
    private readonly RecordedLog _log = new();
    private readonly Person _mara = new(new PersonId(0), "Mara", new TraitProfile(new Dictionary<TraitAxis, double>()), new ThingId(0));
    private readonly Person _pell = new(new PersonId(1), "Pell", new TraitProfile(new Dictionary<TraitAxis, double>()), new ThingId(0));

    [Fact]
    public void Rise_AddsLessTheBetterTheyKnowEachOther_AndNeverPassesOne()
    {
        Relation maraToPell = _mara.Meet(_pell.Id, opinion: 0, trust: 0.5);

        Familiarity.Rise(maraToPell);
        double first = maraToPell.Familiarity;
        Familiarity.Rise(maraToPell);
        first.ShouldBeGreaterThan(0);
        (maraToPell.Familiarity - first).ShouldBeLessThan(first);

        maraToPell.Familiarity = 1;
        Familiarity.Rise(maraToPell);
        maraToPell.Familiarity.ShouldBe(1);
    }

    [Fact]
    public void Step_FadesTheLivingsFamiliarity_TowardTheDeadToo_NeverBelowZero_AndTheDeadsNever()
    {
        Relation maraToPell = _mara.Meet(_pell.Id, opinion: 0, trust: 0.5);
        Relation pellToMara = _pell.Meet(_mara.Id, opinion: 0, trust: 0.5);
        maraToPell.Familiarity = 0.5;
        pellToMara.Familiarity = 0.5;
        World world = Tavern();
        world.Kill(_pell.Id);

        world.Step(World.PeriodTicks, _log);
        maraToPell.Familiarity.ShouldBeLessThan(0.5);
        pellToMara.Familiarity.ShouldBe(0.5);

        // Faded out, they are still known, at 0.
        maraToPell.Familiarity = 1e-6;
        world.Step(World.PeriodTicks, _log);
        maraToPell.Familiarity.ShouldBe(0);
        _mara.RelationTo(_pell.Id).ShouldBeSameAs(maraToPell);
    }

    private World Tavern() =>
        new(seed: 1, new Random(1), [new Thing(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5)], [_mara, _pell]);
}
