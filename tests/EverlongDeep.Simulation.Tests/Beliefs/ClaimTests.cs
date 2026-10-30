using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public class ClaimTests
{
    private static readonly ThingId _tavern = new(0);
    private static readonly PersonOrThing _bram = PersonOrThing.Of(new PersonId(2));
    private static readonly PersonOrThing _jory = PersonOrThing.Of(new PersonId(3));

    private static readonly Claim _told = new(_bram, Act.Attacked, _jory, _tavern, tick: -5 * 3_600, severity: 0.5, blame: 0.2, witness: null);

    [Fact]
    public void Story_AnotherVersionOfTheSameActAndTarget_IsTheSameStory()
    {
        var retold = new Claim(PersonOrThing.Unknown, Act.Attacked, _jory, new ThingId(1), tick: -9 * 3_600, severity: 0.9, blame: 0, new PersonId(0));

        retold.Story.ShouldBe(_told.Story);
    }

    [Fact]
    public void Story_AnotherActOrTarget_IsAnotherStory()
    {
        new Claim(_bram, Act.Betrayed, _jory, _tavern, tick: -5 * 3_600, severity: 0.5, blame: 0.2, witness: null).Story.ShouldNotBe(_told.Story);
        new Claim(_bram, Act.Attacked, _bram, _tavern, tick: -5 * 3_600, severity: 0.5, blame: 0.2, witness: null).Story.ShouldNotBe(_told.Story);
    }
}
