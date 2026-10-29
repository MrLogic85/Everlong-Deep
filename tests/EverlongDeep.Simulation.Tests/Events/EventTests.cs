using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.Events;
using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Tests.Events;

public class EventTests
{
    private const int _hours = 48;

    // A seeded tavern run for two days with "stranger-kills" at its hour, or without it; each conversation traced.
    private static Run StrangerKills(int seed, bool happen = true)
    {
        var log = new SimulationLog();
        World world = WorldBuilder.Tavern(seed, 8, log);
        var traces = new List<ConversationTrace>();
        world.TraceConversations(traces.Add);
        Event? happened = null;
        int eventLine = -1;
        Held[] heldAtEvent = [];
        bool strangerAtEvent = false;
        for (int hour = 0; hour < _hours; hour++)
        {
            if (happen && hour == Scenarios.StrangerKillsHour)
            {
                happened = Scenarios.StrangerKills(world);
                happened.Happen(world, log);
                eventLine = log.Lines.Count - 1;
                PersonId newcomer = happened.Actor;
                strangerAtEvent = world.People.All(person => person.RelationTo(newcomer) is null) && world.Person(newcomer).Relations.Count == 0;
                Story story = new(Act.Killed, PersonOrThing.Of(happened.Target));
                heldAtEvent =
                [
                    .. world.People.SelectMany(person => person.Beliefs).Where(belief => belief.Claim.Story == story)
                        .Select(belief => new Held(belief.Holder, belief.Claim, belief.Confidence)),
                ];
            }

            world.StepHour(log);
        }

        return new Run(world, log, [.. traces], happened, eventLine, heldAtEvent, strangerAtEvent);
    }

    private sealed record Run(World World, SimulationLog Log, ConversationTrace[] Traces, Event? Event, int EventLine, Held[] HeldAtEvent, bool StrangerAtEvent);

    // A belief as it was when the event gave it, before any talk changed it.
    private sealed record Held(PersonId Holder, Claim Claim, double Confidence);

    [Fact]
    public void Happen_SameSeed_GivesTheSameRun_AndDrawsNothingBeforeItHappens()
    {
        foreach (int seed in Enumerable.Range(0, 20))
        {
            Run run = StrangerKills(seed);
            Run plain = StrangerKills(seed, happen: false);

            StrangerKills(seed).Log.Lines.ShouldBe(run.Log.Lines);
            run.Log.Lines.Take(run.EventLine).ShouldBe(plain.Log.Lines.Take(run.EventLine));
        }
    }

    [Fact]
    public void Happen_StrangerKills_TheNewcomerIsAStrangerWhoTalks_TheVictimDies_AndEachOneThereHoldsTheStoryByTheirRole()
    {
        Run[] runs = [.. Enumerable.Range(0, 20).Select(seed => StrangerKills(seed))];

        foreach (Run run in runs)
        {
            Event happened = run.Event.ShouldNotBeNull();
            Person newcomer = run.World.Person(happened.Actor);
            newcomer.ShouldBeSameAs(happened.Newcomer);
            run.World.Person(happened.Target).IsDead.ShouldBeTrue();

            run.StrangerAtEvent.ShouldBeTrue();
            run.HeldAtEvent.Select(belief => belief.Holder)
                .ShouldBe([happened.Actor, .. happened.KeyWitnesses, .. happened.Bystanders], ignoreOrder: true);

            // The actor did it; anyone else either saw it, knowing who did it or not, or only caught the commotion.
            Held actors = run.HeldAtEvent.Single(belief => belief.Holder == happened.Actor);
            (actors.Claim.Actor, actors.Claim.Witness).ShouldBe((PersonOrThing.Of(happened.Actor), happened.Actor));
            run.HeldAtEvent.ShouldAllBe(belief => belief.Claim.Witness == belief.Holder || (belief.Claim.Witness == null && belief.Claim.Actor.IsUnknown));
            run.HeldAtEvent.ShouldAllBe(belief => belief.Claim.Actor.IsUnknown || belief.Claim.Actor == PersonOrThing.Of(happened.Actor));
            run.HeldAtEvent.Select(belief => belief.Claim.Severity).Distinct().ShouldHaveSingleItem();
        }

        Held[] held = [.. runs.SelectMany(run => run.HeldAtEvent)];
        Held[] firstHand = [.. held.Where(belief => belief.Claim.Witness == belief.Holder)];
        Held[] hearsay = [.. held.Where(belief => belief.Claim.Witness == null)];
        hearsay.ShouldNotBeEmpty("no witness only caught the commotion, so the checks above passed on nothing");
        firstHand.ShouldContain(belief => belief.Claim.Actor.IsUnknown, "no witness saw it without seeing who did it");
        hearsay.Max(belief => belief.Confidence).ShouldBeLessThan(firstHand.Min(belief => belief.Confidence));

        // The newcomer talks like anyone else: they have moments of their own, and they are told.
        bool opensFirst = runs.Any(run => run.Traces
            .FirstOrDefault(trace => trace.TellerBefore.Id == run.Event!.Actor || trace.ListenerBefore.Id == run.Event!.Actor)?.TellerBefore.Id == run.Event!.Actor);
        opensFirst.ShouldBeTrue("the newcomer never opens a conversation before someone talks to them");
        runs.ShouldContain(run => run.Traces.Any(trace => trace.ListenerBefore.Id == run.Event!.Actor));
    }
}
