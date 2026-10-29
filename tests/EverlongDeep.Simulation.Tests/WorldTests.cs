using EverlongDeep.Simulation.Beliefs;

namespace EverlongDeep.Simulation.Tests;

public class WorldTests
{
    // A broad seeded run reaches the code's asserts, and a failed Debug.Assert fails the test.
    [Fact]
    public void StepHour_ManySeedsForAMonth_KeepsEveryInvariant()
    {
        var traces = new List<ConversationTrace>();
        foreach (int seed in Enumerable.Range(0, 20))
        {
            var log = new SimulationLog();
            World world = WorldBuilder.Tavern(seed, WorldBuilder.MaxPeople, log);
            world.TraceConversations(traces.Add);
            for (int hour = 0; hour < 30 * 24; hour++)
            {
                world.StepHour(log);
            }
        }

        // A retelling: the teller heard the story they tell.
        traces.ShouldContain(trace => trace.TellerBefore.Beliefs
            .Single(belief => belief.Claim.Story == trace.Held.Story).HeardFrom.Count > 0);

        // The other outcomes need a competing version or a repeat teller, which a tavern with only its seeded past lacks.
        Hearing[] reachable = [Hearing.Believed, Hearing.NotBelieved, Hearing.Surer, Hearing.KeptOwn];
        reachable.ShouldBeSubsetOf(traces.Select(trace => trace.Hearing));
    }
}
