using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Events;

namespace EverlongDeep.Cli;

/// <summary>
/// The events the CLI injects into a run, recorded in the order it injects them, for a replay
/// (<c>docs/CODE_DESIGN_PRINCIPLES.md</c> §4). Each event happens at the tick it is injected at, so the event is the
/// whole record.
/// </summary>
public sealed class Injections
{
    private readonly List<Event> _recorded = [];

    public IReadOnlyList<Event> Recorded => _recorded;

    /// <summary>Records <paramref name="injected"/> as happening now, then schedules it in <paramref name="world"/>.</summary>
    public void Inject(World world, Event injected, SimulationLog log)
    {
        injected = injected with { Tick = world.Tick };
        _recorded.Add(injected);
        world.Schedule(injected, log);
    }
}
