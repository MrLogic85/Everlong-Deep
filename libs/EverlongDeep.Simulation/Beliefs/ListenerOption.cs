using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>A known person a teller could have told, scored by opinion + trust.</summary>
public sealed record ListenerOption(PersonId Person, double Score, double Chance);
