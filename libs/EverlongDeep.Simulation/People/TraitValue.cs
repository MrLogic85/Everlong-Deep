namespace EverlongDeep.Simulation.People;

/// <summary>A person's <paramref name="Value"/> on <paramref name="Axis"/>, −1 to 1.</summary>
public readonly record struct TraitValue(TraitAxis Axis, double Value);
