namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// Identifies a story, given where it starts (<c>docs/08 Social Model.md</c> → "Claim"). Two claims with the same id are
/// versions of one story; it names the story, not what happened.
/// </summary>
public readonly record struct StoryId(int Value);
