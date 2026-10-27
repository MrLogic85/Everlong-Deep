namespace EverlongDeep.Simulation;

/// <summary>The run's log of plain template lines.</summary>
public sealed class SimulationLog
{
    private readonly List<string> _lines = [];

    public IReadOnlyList<string> Lines => _lines;

    public void Write(string line) => _lines.Add(line);
}
