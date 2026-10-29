using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public class ImportanceTests
{
    private const double _tolerance = 1e-9;

    private static readonly Thing _tavern = new(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);
    private static readonly Thing _logbook = new(new ThingId(1), "logbook", ThingKind.Item, 0.2);

    private readonly Person _mara = Someone(0, "Mara");
    private readonly Person _bram = Someone(1, "Bram");
    private readonly Person _jory = Someone(2, "Jory");

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Judge_AStoryAboutKin_ActorOrTarget_MattersMoreThanTheSameStoryAboutStrangers(int kin)
    {
        Claim attack = BramAttackedJory(severity: 0.5);
        World world = Tavern();

        Importance.Judge(world, _mara, attack).ShouldBe(Claim.BaseSeverity(Act.Attacked), _tolerance);

        _mara.Meet(new PersonId(kin), opinion: 0, trust: 0.5).Ties = Ties.Sibling;
        Importance.Judge(world, _mara, attack).ShouldBe(Claim.BaseSeverity(Act.Attacked) + Importance.TieStake, _tolerance);
    }

    [Theory]
    [InlineData(0.6, Importance.StrongOpinionStake)]
    [InlineData(-0.6, Importance.StrongOpinionStake)]
    [InlineData(0.5, 0)]
    public void Judge_AStrongOpinionOfTheActorOrTarget_EitherWay_AddsItsStake(double opinion, double stake)
    {
        _mara.Meet(_bram.Id, opinion, trust: 0.5);
        _mara.Meet(_jory.Id, opinion: 0, trust: 0.5);

        Importance.Judge(Tavern(), _mara, BramAttackedJory(severity: 0.5))
            .ShouldBe(Claim.BaseSeverity(Act.Attacked) + stake, _tolerance);

        // The same opinion of the target instead.
        _mara.RelationTo(_bram.Id)!.Opinion = 0;
        _mara.RelationTo(_jory.Id)!.Opinion = opinion;

        Importance.Judge(Tavern(), _mara, BramAttackedJory(severity: 0.5))
            .ShouldBe(Claim.BaseSeverity(Act.Attacked) + stake, _tolerance);
    }

    [Fact]
    public void Judge_TheHoldersOwnStory_AddsItsStake_AndStaysAtMostOne()
    {
        Claim seen = BramAttackedJory(severity: 0.5).WithWitness(_mara.Id);
        _mara.Meet(_bram.Id, opinion: -1, trust: 0).Ties = Ties.Rival;

        Importance.Judge(Tavern(), _jory, seen).ShouldBe(Claim.BaseSeverity(Act.Attacked) + Importance.OwnStoryStake, _tolerance);
        Importance.Judge(Tavern(), _mara, seen).ShouldBe(1);
    }

    [Fact]
    public void Judge_IgnoresTheTellersSeverity_AndScalesHarmToAThingByItsImportance()
    {
        Claim robbed = new(
            PersonOrThing.Unknown, Act.Robbed, PersonOrThing.Of(_logbook.Id), _tavern.Id, hour: -5, severity: 1, blame: 0, witness: null);

        Importance.Judge(Tavern(), _mara, robbed).ShouldBe(Claim.TableSeverity(Act.Robbed, _logbook), _tolerance);
        Importance.Judge(Tavern(), _mara, BramAttackedJory(severity: 0)).ShouldBe(Claim.BaseSeverity(Act.Attacked), _tolerance);
    }

    private static Person Someone(int id, string name) =>
        new(new PersonId(id), name, new TraitProfile(new Dictionary<TraitAxis, double>()), _tavern.Id);

    private Claim BramAttackedJory(double severity) =>
        new(PersonOrThing.Of(_bram.Id), Act.Attacked, PersonOrThing.Of(_jory.Id), _tavern.Id, hour: -5, severity, blame: 0.2, witness: null);

    private World Tavern() => new(seed: 0, new FixedRandom(0), [_tavern, _logbook], [_mara, _bram, _jory]);
}
