using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.People;

public class TieFormationTests
{
    private readonly SimulationLog _log = new();
    private readonly Person _mara = new(new PersonId(0), "Mara", [Trait.Honest], new ThingId(0));
    private readonly Person _pell = new(new PersonId(1), "Pell", [Trait.Gossip], new ThingId(0));

    [Theory]
    [InlineData(0.2, 0.7, "Hour 96: Mara no longer counts Pell as a friend (opinion 0.20, trust 0.70)")]
    [InlineData(0.4, 0.2, "Hour 96: Mara no longer counts Pell as a friend (opinion 0.40, trust 0.20)")]
    public void StepHour_LastingWarmth_MakesAOneSidedFriend_AndACoolingBreaksIt(
        double coolOpinion, double coolTrust, string breakLine)
    {
        Relation maraToPell = _mara.Meet(
            _pell.Id, TieFormation.FriendFormOpinion, TieFormation.FriendFormTrust);
        maraToPell.Ties = Ties.Sibling;
        Relation pellToMara = _pell.Meet(_mara.Id, opinion: 0.8, trust: 0.1);
        World world = Tavern();

        Step(world, TieFormation.HoldHours - 1);
        maraToPell.Ties.ShouldBe(Ties.Sibling);

        Step(world, 1);
        maraToPell.Ties.ShouldBe(Ties.Sibling | Ties.Friend);
        pellToMara.Ties.ShouldBe(Ties.None);
        _log.Lines.ShouldBe(["Hour 24: Mara now counts Pell as a friend (opinion 0.60, trust 0.50)"]);

        // Between the break and form marks the tie holds.
        maraToPell.Opinion = 0.4;
        Step(world, TieFormation.HoldHours * 2);
        maraToPell.Ties.ShouldBe(Ties.Sibling | Ties.Friend);

        maraToPell.Opinion = coolOpinion;
        maraToPell.Trust = coolTrust;
        Step(world, TieFormation.HoldHours);
        maraToPell.Ties.ShouldBe(Ties.Sibling);
        _log.Lines[^1].ShouldBe(breakLine);
    }

    [Fact]
    public void StepHour_WarmthThatLapses_RestartsTheHold()
    {
        Relation maraToPell = _mara.Meet(_pell.Id, opinion: 0.8, trust: 0.7);
        World world = Tavern();

        Step(world, TieFormation.HoldHours - 1);
        maraToPell.Opinion = 0.5;
        Step(world, 1);
        maraToPell.Opinion = 0.8;
        Step(world, TieFormation.HoldHours - 1);
        maraToPell.Ties.ShouldBe(Ties.None);

        Step(world, 1);
        maraToPell.Ties.ShouldBe(Ties.Friend);
    }

    [Fact]
    public void StepHour_LastingHate_MakesARival_AndAThawBreaksIt()
    {
        Relation maraToPell = _mara.Meet(_pell.Id, TieFormation.RivalFormOpinion, trust: 0.9);
        World world = Tavern();

        // A lapse restarts the hold.
        Step(world, TieFormation.HoldHours - 1);
        maraToPell.Opinion = -0.5;
        Step(world, 1);
        maraToPell.Opinion = TieFormation.RivalFormOpinion;
        Step(world, TieFormation.HoldHours - 1);
        maraToPell.Ties.ShouldBe(Ties.None);

        Step(world, 1);
        maraToPell.Ties.ShouldBe(Ties.Rival);
        _log.Lines.ShouldBe(["Hour 48: Mara now counts Pell as a rival (opinion -0.60, trust 0.90)"]);

        maraToPell.Opinion = -0.4;
        Step(world, TieFormation.HoldHours * 2);
        maraToPell.Ties.ShouldBe(Ties.Rival);

        maraToPell.Opinion = 0;
        Step(world, TieFormation.HoldHours);
        maraToPell.Ties.ShouldBe(Ties.None);
        _log.Lines[^1].ShouldBe("Hour 120: Mara no longer counts Pell as a rival (opinion 0.00, trust 0.90)");
    }

    private World Tavern() =>
        new(seed: 1, new Random(1), [new Thing(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5)], [_mara, _pell]);

    private void Step(World world, int hours)
    {
        for (int i = 0; i < hours; i++)
        {
            world.StepHour(_log);
        }
    }
}
