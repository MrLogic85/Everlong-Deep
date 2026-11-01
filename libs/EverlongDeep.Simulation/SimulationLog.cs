namespace EverlongDeep.Simulation;

/// <summary>
/// The run's log: hands each record of a thing that happened to <paramref name="onWritten"/> as it is written, in the
/// order it happened, and keeps none.
/// </summary>
public sealed class SimulationLog(Action<LogRecord> onWritten)
{
    public void Write(LogRecord record) => onWritten(record);
}
