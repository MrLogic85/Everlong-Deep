using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Events;

namespace EverlongDeep.Cli.Tests;

public class InjectionsTests
{
    // The replay world is built fresh from the seed and given only the recorded events, each at its tick, after the first
    // run's newcomer has entered and come to believe what they did.
    [Fact]
    public void Recorded_ReplayedFromTheSeed_GiveTheSameRecords()
    {
        var first = new List<LogRecord>();
        var firstLog = new SimulationLog(first.Add);
        World world = WorldBuilder.Tavern(seed: 42, Scenarios.StrangerKillsPeople, firstLog);
        var injections = new Injections();
        world.Step(World.PeriodTicks, firstLog);
        injections.Inject(world, Scenarios.StrangerKills(world), firstLog);
        world.Step(World.PeriodTicks, firstLog);

        var replayed = new List<LogRecord>();
        var replayLog = new SimulationLog(replayed.Add);
        World replay = WorldBuilder.Tavern(seed: 42, Scenarios.StrangerKillsPeople, replayLog);
        foreach (Event injected in injections.Recorded)
        {
            replay.Step(injected.Tick - replay.Tick, replayLog);
            replay.Schedule(injected, replayLog);
        }

        replay.Step(world.Tick - replay.Tick, replayLog);

        replayed.OfType<EventHappened>().ShouldHaveSingleItem();
        replayed.ShouldBe(first);
    }
}
