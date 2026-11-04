using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.Events;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests;

public class WorldTests
{
    // On a period boundary, and inside a period.
    [Theory]
    [InlineData(World.PeriodTicks)]
    [InlineData(World.PeriodTicks + 1_234)]
    public void Step_SameSeed_AtAnyStepSize_GivesTheSameRun_WithEventsScheduled(long eventTick)
    {
        (string[] byPeriod, World world, RecordedLog log) = Run(eventTick, World.PeriodTicks);

        // Both events happen at their tick, one after the other in the order they were scheduled; a second firing
        // would fail the event's own assert, since its target is dead.
        EventHappened[] atTick = [.. log.Records.Where(record => record.Tick == eventTick).OfType<EventHappened>()];
        atTick.Select(happened => happened.Seen.Actor).ShouldBe([PersonOrThing.Of(world.People[^1].Id), PersonOrThing.Of(new PersonId(6))]);
        world.Person(new PersonId(7)).IsDead.ShouldBeTrue();

        // The same step again shows the run is deterministic; the others, that the step size changes nothing.
        foreach (long step in new long[] { World.PeriodTicks, 1, 1_000, 2 * World.PeriodTicks })
        {
            (string[] state, _, RecordedLog stepped) = Run(eventTick, step);
            stepped.Records.ShouldBe(log.Records);
            state.ShouldBe(byPeriod);
        }
    }

    [Fact]
    public void Step_AtAPeriodBoundary_RunsWhatIsDueBeforeIt_ThenThePeriodicSteps_ThenWhatIsDueAtIt()
    {
        var tavern = new Thing(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);
        Person[] people = [.. Enumerable.Range(0, 4).Select(id => new Person(new PersonId(id), $"Person {id}", Gender.They, new TraitProfile(new Dictionary<TraitAxis, double>()), tavern.Id))];

        // Every draw says no, so nobody talks, and only fading changes the events' beliefs.
        var world = new World(seed: 0, new FixedRandom(0.99), [tavern], people);
        var log = new RecordedLog();
        world.Schedule(new Event(Act.Killed, people[0].Id, people[1].Id, tavern.Id, World.PeriodTicks - 1, [], [], Newcomer: null), log);
        world.Schedule(new Event(Act.Killed, people[2].Id, people[3].Id, tavern.Id, World.PeriodTicks, [], [], Newcomer: null), log);

        world.Step(World.PeriodTicks + 1, log);

        Belief before = people[0].Beliefs.ShouldHaveSingleItem();
        Belief at = people[2].Beliefs.ShouldHaveSingleItem();
        before.Relevance.ShouldBeLessThan(before.Importance);
        at.Relevance.ShouldBe(at.Importance);
    }

    // Runs a seeded tavern with two killings at eventTick to two periods past it, in steps of the given size. State is
    // everything the run can change, and the next draw.
    private static (string[] State, World World, RecordedLog Log) Run(long eventTick, long step)
    {
        long end = eventTick + (2 * World.PeriodTicks);
        var log = new RecordedLog();
        World world = WorldBuilder.Tavern(seed: 0, 8, log);
        Event killing = Scenarios.StrangerKills(world) with { Tick = eventTick };
        world.Schedule(killing, log);
        world.Schedule(new Event(Act.Killed, new PersonId(6), new PersonId(7), killing.Place, eventTick, [], [], Newcomer: null), log);
        while (world.Tick < end)
        {
            world.Step(Math.Min(step, end - world.Tick), log);
        }

        string[] state =
        [
            .. world.People.SelectMany(person =>
                person.Beliefs.Select(belief => $"{person.Id} {belief.Claim} {belief.Confidence} {belief.Relevance}")
                    .Concat(person.Relations.Select(relation =>
                        $"{person.Id} {relation.Other} {relation.Opinion} {relation.Trust} {relation.Familiarity} {relation.Ties} {relation.FriendChangeTicks}"))),
            $"next {world.Random.NextDouble()}",
        ];
        return (state, world, log);
    }
}
