using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>The latest telling the holder heard from one teller.</summary>
public sealed record HeardEntry(PersonId From, int Minute, Claim Heard);
