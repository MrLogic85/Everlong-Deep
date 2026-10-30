namespace EverlongDeep.Simulation;

/// <summary>One log line: <see cref="Text"/> holds no clock, and <see cref="Tick"/> says when it happened.</summary>
public sealed record LogLine(long Tick, string Text);
