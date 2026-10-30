namespace EverlongDeep.Simulation;

/// <summary>
/// One thing the log tells of, at <see cref="Tick"/>, with the values as they were then. A record holds data only: a
/// reader after the run words it, and never looks its values up in a world that has moved on.
/// </summary>
public abstract record LogRecord(long Tick);

/// <summary>Something the simulation was asked to do and refused; a Debug build fails its assert first.</summary>
public abstract record Problem(long Tick) : LogRecord(Tick);
