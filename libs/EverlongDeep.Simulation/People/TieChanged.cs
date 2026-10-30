namespace EverlongDeep.Simulation.People;

/// <summary>
/// <paramref name="Holder"/> now counts <paramref name="Other"/> as <paramref name="Tie"/>, or, when not
/// <paramref name="Formed"/>, no longer does; with the opinion and trust that held long enough.
/// </summary>
public sealed record TieChanged(long Tick, PersonId Holder, PersonId Other, Ties Tie, bool Formed, double Opinion, double Trust)
    : LogRecord(Tick);
