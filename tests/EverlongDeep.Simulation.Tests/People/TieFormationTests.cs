using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.People;

public class TieFormationTests
{
    private const double _betweenFriendMarks = (TieFormation.FriendFormOpinion + TieFormation.FriendBreakOpinion) / 2;
    private const double _betweenRivalMarks = (TieFormation.RivalFormOpinion + TieFormation.RivalBreakOpinion) / 2;

    private readonly SimulationLog _log = new();
    private readonly Person _mara = new(new PersonId(0), "Mara", new TraitProfile(new Dictionary<TraitAxis, double>()), new ThingId(0));
    private readonly Person _pell = new(new PersonId(1), "Pell", new TraitProfile(new Dictionary<TraitAxis, double>()), new ThingId(0));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StepHour_LastingWarmth_MakesAOneSidedFriend_AndACoolingOfOpinionOrTrustBreaksIt(bool opinionCools)
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

        // Between the break and form marks the tie holds.
        maraToPell.Opinion = _betweenFriendMarks;
        Step(world, TieFormation.HoldHours * 2);
        maraToPell.Ties.ShouldBe(Ties.Sibling | Ties.Friend);

        if (opinionCools)
        {
            maraToPell.Opinion = TieFormation.FriendBreakOpinion - 0.1;
        }
        else
        {
            maraToPell.Trust = TieFormation.FriendBreakTrust - 0.1;
        }

        Step(world, TieFormation.HoldHours);
        maraToPell.Ties.ShouldBe(Ties.Sibling);
    }

    [Fact]
    public void StepHour_WarmthThatLapses_RestartsTheHold()
    {
        Relation maraToPell = _mara.Meet(_pell.Id, TieFormation.FriendFormOpinion, TieFormation.FriendFormTrust);
        World world = Tavern();

        Step(world, TieFormation.HoldHours - 1);
        maraToPell.Opinion = _betweenFriendMarks;
        Step(world, 1);
        maraToPell.Opinion = TieFormation.FriendFormOpinion;
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
        maraToPell.Opinion = _betweenRivalMarks;
        Step(world, 1);
        maraToPell.Opinion = TieFormation.RivalFormOpinion;
        Step(world, TieFormation.HoldHours - 1);
        maraToPell.Ties.ShouldBe(Ties.None);

        Step(world, 1);
        maraToPell.Ties.ShouldBe(Ties.Rival);

        maraToPell.Opinion = _betweenRivalMarks;
        Step(world, TieFormation.HoldHours * 2);
        maraToPell.Ties.ShouldBe(Ties.Rival);

        maraToPell.Opinion = 0;
        Step(world, TieFormation.HoldHours);
        maraToPell.Ties.ShouldBe(Ties.None);
    }

    [Fact]
    public void StepHour_TheDeadFormNoTies_ButTheLivingStillFormThemTowardTheDead()
    {
        Relation maraToPell = _mara.Meet(_pell.Id, TieFormation.FriendFormOpinion, TieFormation.FriendFormTrust);
        Relation pellToMara = _pell.Meet(_mara.Id, TieFormation.FriendFormOpinion, TieFormation.FriendFormTrust);
        World world = Tavern();
        world.Kill(_pell.Id);

        Step(world, TieFormation.HoldHours);

        maraToPell.Ties.ShouldBe(Ties.Friend);
        (pellToMara.Ties, pellToMara.FriendChangeHours).ShouldBe((Ties.None, 0));
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
