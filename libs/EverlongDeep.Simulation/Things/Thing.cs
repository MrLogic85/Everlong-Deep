using System.Diagnostics;

namespace EverlongDeep.Simulation.Things;

/// <summary>Anything in the world that isn't a person, as <c>docs/01 Simulation Architecture.md</c> → "Things" defines.</summary>
public sealed class Thing
{
    public Thing(ThingId id, string name, ThingKind kind, double importance)
    {
        Debug.Assert(importance >= 0 && importance <= 1, "Importance is 0 to 1");

        Id = id;
        Name = name;
        Kind = kind;
        Importance = importance;
    }

    public ThingId Id { get; }
    public string Name { get; }
    public ThingKind Kind { get; }

    /// <summary>How much harm to this thing weighs, from 0 to 1.</summary>
    public double Importance { get; }

    public bool CanHoldPeople => Kind is ThingKind.Place or ThingKind.Ship;
}
