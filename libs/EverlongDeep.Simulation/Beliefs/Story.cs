namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// Which story a claim tells: its act and target. Two claims with the same story are versions of one another, however
/// their actor, witness or dials differ.
/// </summary>
public readonly record struct Story(Act Act, PersonOrThing Target);
