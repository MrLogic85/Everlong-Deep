namespace EverlongDeep.Simulation;

/// <summary>A tavern was built from <paramref name="Seed"/>, with <paramref name="People"/> of the <paramref name="PeopleAsked"/>.</summary>
public sealed record WorldBuilt(long Tick, int Seed, int PeopleAsked, int People) : LogRecord(Tick);

/// <summary>The world was asked to step back by <paramref name="Ticks"/>, below 0; its clock stayed.</summary>
public sealed record NegativeStep(long Tick, long Ticks) : Problem(Tick);

/// <summary>The clock was asked to go back to <paramref name="ToTick"/>; it stayed.</summary>
public sealed record TimeWentBack(long Tick, long ToTick) : Problem(Tick);
