namespace EverlongDeep.Simulation;

/// <summary>The run's log: a record of each thing that happened, in the order it happened.</summary>
public sealed class SimulationLog
{
    private readonly List<LogRecord> _records = [];

    public IReadOnlyList<LogRecord> Records => _records;

    public void Write(LogRecord record) => _records.Add(record);
}
