using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Tests;

public class WorldBuilderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(16)]
    public void Tavern_PutsEveryPersonInTheTavern_AndLogsTheSeed(int peopleCount)
    {
        WorldBuilder.MaxPeople.ShouldBeGreaterThanOrEqualTo(peopleCount);
        var log = new SimulationLog();

        World world = WorldBuilder.Tavern(seed: 42, peopleCount, log);

        world.Seed.ShouldBe(42);
        world.Places.Count.ShouldBe(1);
        world.People.Count.ShouldBe(peopleCount);
        world.People.ShouldAllBe(person => person.Place == world.Places[0].Id);
        world.People.Select(person => person.Id).ShouldBeUnique();
        world.People.Select(person => person.Name).ShouldBeUnique();
        world.People.ShouldAllBe(person => person.Traits.Count > 0);
        log.Lines.ShouldContain("Seed: 42");
    }

    [Fact]
    public void Tavern_StartsWithMatchingKin_AcquaintancesAndStrangers()
    {
        World[] worlds = [.. Enumerable.Range(0, 5).Select(seed => WorldBuilder.Tavern(seed, peopleCount: 12, new SimulationLog()))];

        foreach (World world in worlds)
        {
            foreach (Person person in world.People)
            {
                foreach (Relation relation in person.Relations)
                {
                    Relation back = world.Person(relation.Other).RelationTo(person.Id).ShouldNotBeNull();
                    back.Ties.ShouldBe(Reciprocal(relation.Ties));
                }
            }
        }

        Relation[] relations = [.. worlds.SelectMany(world => world.People).SelectMany(person => person.Relations)];
        relations.ShouldContain(relation => relation.Ties == Ties.Sibling);
        relations.ShouldContain(relation => relation.Ties == Ties.Parent);
        relations.ShouldContain(relation => relation.Ties == Ties.None);
        worlds.ShouldAllBe(world => world.People.Any(person => person.Relations.Count < world.People.Count - 1));
    }

    [Fact]
    public void Tavern_SameSeed_BuildsTheSamePeople()
    {
        World first = WorldBuilder.Tavern(seed: 7, peopleCount: 8, new SimulationLog());
        World second = WorldBuilder.Tavern(seed: 7, peopleCount: 8, new SimulationLog());

        Describe(second).ShouldBe(Describe(first));
        second.Random.Next().ShouldBe(first.Random.Next());
    }

    [Fact]
    public void Tavern_DifferentSeed_BuildsDifferentPeople()
    {
        World first = WorldBuilder.Tavern(seed: 7, peopleCount: 8, new SimulationLog());
        World second = WorldBuilder.Tavern(seed: 8, peopleCount: 8, new SimulationLog());

        Describe(second).ShouldNotBe(Describe(first));
    }

    private static Ties Reciprocal(Ties ties) => ties switch
    {
        Ties.Parent => Ties.Child,
        Ties.Child => Ties.Parent,
        _ => ties,
    };

    private static string[] Describe(World world) =>
        world.People
            .Select(person =>
                $"{person.Id.Value} {person.Name} {string.Join(",", person.Traits)} {person.Place.Value} " +
                string.Join(";", person.Relations.Select(r => $"{r.Other.Value}:{r.Opinion}:{r.Trust}:{r.Ties}")))
            .ToArray();
}
