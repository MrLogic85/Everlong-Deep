using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Events;

/// <summary>
/// Hand-written events a run can schedule, until the player and the LLM cause them (<c>docs/08 Social Model.md</c> →
/// "Events").
/// </summary>
public static class Scenarios
{
    public const string StrangerKillsName = "stranger-kills";

    public const long StrangerKillsTick = 86_400;

    /// <summary>The fewest people the scenario needs: its roles go up to p5.</summary>
    public const int StrangerKillsPeople = 6;

    /// <summary>A stranger enters p5's place and kills p5.</summary>
    public static Event StrangerKills(World world)
    {
        Person victim = world.Person(new PersonId(5));
        var stranger = new Person(
            new PersonId(world.People.Count),
            "Rook",
            new TraitProfile(new Dictionary<TraitAxis, double> { [TraitAxis.Kind] = -0.8, [TraitAxis.Talkative] = -0.6, [TraitAxis.Confident] = 0.7 }),
            victim.Place);
        return new Event(
            Act.Killed,
            stranger.Id,
            victim.Id,
            victim.Place,
            StrangerKillsTick,
            KeyWitnesses: [new PersonId(1), new PersonId(2)],
            Bystanders: [new PersonId(3), new PersonId(4)],
            stranger);
    }
}
