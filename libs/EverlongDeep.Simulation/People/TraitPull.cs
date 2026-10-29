namespace EverlongDeep.Simulation.People;

/// <summary>One trait's pull on a choice: the choice's weight for the axis times the person's value on it.</summary>
public readonly record struct TraitPull(Choice Choice, TraitAxis Axis, double Weight, double Trait)
{
    public double Value => Weight * Trait;
}
