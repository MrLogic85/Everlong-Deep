using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>The latest telling the holder gave one listener, with the belief they held then beside the version told.</summary>
public sealed record ToldEntry(PersonId To, long Tick, Claim Held, Claim Told);
