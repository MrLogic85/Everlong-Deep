using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public class BeliefTests
{
    private static readonly PersonId _mara = new(0);
    private static readonly PersonId _pell = new(1);
    private static readonly PersonId _bram = new(2);

    private static readonly Claim _held =
        new(PersonOrThing.Of(_bram), Act.Helped, PersonOrThing.Of(_pell), new ThingId(0), hour: -5, severity: 0.1, blame: 0, witness: null);

    private readonly Belief _belief = new(_mara, _held, confidence: 0.5, importance: 0.5);

    [Fact]
    public void Remember_KeepsTheLatestEntryPerPerson_Last()
    {
        _belief.RememberHeard(new HeardEntry(_pell, Minute: 1, _held));
        _belief.RememberHeard(new HeardEntry(_bram, Minute: 2, _held));
        _belief.RememberHeard(new HeardEntry(_pell, Minute: 3, _held));
        _belief.RememberTold(new ToldEntry(_pell, Minute: 4, _held, _held));
        _belief.RememberTold(new ToldEntry(_pell, Minute: 5, _held, _held));

        _belief.HeardFrom.Select(entry => (entry.From, entry.Minute)).ShouldBe([(_bram, 2), (_pell, 3)]);
        _belief.ToldTo.ShouldHaveSingleItem().Minute.ShouldBe(5);
    }

    [Fact]
    public void WasToldUnchanged_ComparesTheBeliefWithWhatWasHeld_NotWithTheColoredVersionTold()
    {
        Claim colored = _held.WithDials(0.4, 0);
        _belief.WasToldUnchanged(_pell).ShouldBeFalse();

        _belief.RememberTold(new ToldEntry(_pell, Minute: 1, Held: _held, Told: colored));
        _belief.WasToldUnchanged(_pell).ShouldBeTrue();
        _belief.WasToldUnchanged(_bram).ShouldBeFalse();

        // Told when they held another version, they have something new.
        _belief.RememberTold(new ToldEntry(_pell, Minute: 2, Held: colored, Told: colored));
        _belief.WasToldUnchanged(_pell).ShouldBeFalse();
    }
}
