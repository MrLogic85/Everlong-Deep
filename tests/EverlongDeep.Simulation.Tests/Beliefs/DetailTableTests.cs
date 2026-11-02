using EverlongDeep.Simulation.Beliefs;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public class DetailTableTests
{
    // Each of the two chances says yes below DetailTable.DrawnChance; the draws after them shuffle the table.
    [Theory]
    [InlineData(0.99, 0.99, 0)]
    [InlineData(0, 0.99, 1)]
    [InlineData(0, 0, 2)]
    public void Draw_EachChanceThatSaysYes_AddsADifferentDetailFromTheActsTable(double first, double second, int expected)
    {
        foreach (Act act in Enum.GetValues<Act>())
        {
            ValueList<Detail> drawn = DetailTable.Draw(act, new ScriptedThenFixedRandom(0, first, second));

            drawn.Count.ShouldBe(expected);
            drawn.ShouldBeUnique();
            drawn.ShouldAllBe(detail => DetailTable.Of(act).Contains(detail));
        }
    }
}
