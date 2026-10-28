namespace EverlongDeep.Simulation.People;

/// <summary>A yes-or-no choice as drawn: its score, the chance that gave, and the roll that decided it.</summary>
public readonly record struct Roll(double Score, double Chance, double Value)
{
    public bool Yes => Value < Chance;
}
