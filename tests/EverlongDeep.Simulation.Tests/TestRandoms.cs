namespace EverlongDeep.Simulation.Tests;

/// <summary>Every draw returns the same sample: 0 says yes to every choice and picks the first option.</summary>
internal sealed class FixedRandom(double sample) : Random
{
    protected override double Sample() => sample;
}

/// <summary>Returns the given samples in order and fails on one draw more, so a test pins the draw order.</summary>
internal sealed class ScriptedRandom(params double[] samples) : Random
{
    private readonly Queue<double> _samples = new(samples);

    protected override double Sample() => _samples.Dequeue();
}
