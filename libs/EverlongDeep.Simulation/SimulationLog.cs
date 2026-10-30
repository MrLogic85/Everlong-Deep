namespace EverlongDeep.Simulation;

/// <summary>The run's log of plain template lines.</summary>
public sealed class SimulationLog
{
    private readonly List<LogLine> _lines = [];

    public IReadOnlyList<LogLine> Lines => _lines;

    public void Write(long tick, string text) => _lines.Add(new LogLine(tick, text));
}
