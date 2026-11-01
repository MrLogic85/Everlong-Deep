namespace EverlongDeep.Simulation.Tests;

/// <summary>
/// A log whose listener keeps every record, so a test can read what a run wrote; it passes wherever the simulation
/// takes a <see cref="SimulationLog"/>.
/// </summary>
internal sealed class RecordedLog
{
    private readonly List<LogRecord> _records = [];

    public RecordedLog() => Log = new SimulationLog(_records.Add);

    public SimulationLog Log { get; }

    public IReadOnlyList<LogRecord> Records => _records;

    public static implicit operator SimulationLog(RecordedLog recorded) => recorded.Log;
}
