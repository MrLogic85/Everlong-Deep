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

    private static string[] Describe(World world) =>
        world.People
            .Select(person => $"{person.Id.Value} {person.Name} {string.Join(",", person.Traits)} {person.Place.Value}")
            .ToArray();
}
