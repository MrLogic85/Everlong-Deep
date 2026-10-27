using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Things;

public class ThingTests
{
    [Theory]
    [InlineData(ThingKind.Place, true)]
    [InlineData(ThingKind.Ship, true)]
    [InlineData(ThingKind.Item, false)]
    public void CanHoldPeople_OnlyPlacesAndShips(ThingKind kind, bool expected)
    {
        new Thing(new ThingId(0), "Anything", kind, importance: 0.5).CanHoldPeople.ShouldBe(expected);
    }
}
