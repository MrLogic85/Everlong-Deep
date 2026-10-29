namespace EverlongDeep.Simulation;

/// <summary>Draws from a range of tunable values, the same way wherever one is drawn.</summary>
internal static class RandomRanges
{
    /// <summary>A value drawn evenly from <paramref name="range"/>.</summary>
    public static double Between(this Random random, (double Min, double Max) range) =>
        range.Min + (random.NextDouble() * (range.Max - range.Min));
}
