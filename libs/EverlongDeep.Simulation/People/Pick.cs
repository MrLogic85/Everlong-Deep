namespace EverlongDeep.Simulation.People;

/// <summary>An option drawn from several: the one picked, the roll, and each option's chance.</summary>
internal readonly record struct Pick(int Index, double Value, IReadOnlyList<double> Chances);
