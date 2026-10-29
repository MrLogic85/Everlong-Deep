using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests;

public class WorldBuilderTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(16)]
    public void Tavern_PutsEveryPersonInTheTavern(int peopleCount)
    {
        WorldBuilder.MaxPeople.ShouldBeGreaterThanOrEqualTo(peopleCount);

        World world = WorldBuilder.Tavern(seed: 42, peopleCount, new SimulationLog());

        world.Seed.ShouldBe(42);
        Thing tavern = world.Things.Where(thing => thing.Kind == ThingKind.Place).ShouldHaveSingleItem();
        world.People.Count.ShouldBe(peopleCount);
        world.People.ShouldAllBe(person => person.Place == tavern.Id);
        world.People.Select(person => person.Id).ShouldBeUnique();
        world.People.Select(person => person.Name).ShouldBeUnique();
    }

    [Fact]
    public void Tavern_BuildsShipsAndItems_EachFoundByItsId()
    {
        foreach (World world in Enumerable.Range(0, 20).Select(seed => WorldBuilder.Tavern(seed, peopleCount: 6, new SimulationLog())))
        {
            world.Things.ShouldContain(thing => thing.Kind == ThingKind.Ship);
            world.Things.ShouldContain(thing => thing.Kind == ThingKind.Item);
            world.Things.Select(thing => thing.Id).ShouldBeUnique();
            world.Things.Select(thing => thing.Name).ShouldBeUnique();
            world.Things.ShouldAllBe(thing => world.Thing(thing.Id) == thing);
        }
    }

    [Fact]
    public void Tavern_StartsWithMatchingKin_AcquaintancesAndStrangers_WhoKnowWhatTheirTieShows()
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
        TraitAxis[] visible = [.. Enum.GetValues<TraitAxis>().Where(TraitTables.IsVisible)];
        relations.Where(relation => relation.Ties != Ties.None)
            .ShouldAllBe(relation => relation.Impressions.Count == TraitProfile.AxisCount);
        relations.Where(relation => relation.Ties == Ties.None)
            .ShouldAllBe(relation => relation.Impressions.Keys.OrderBy(axis => axis).SequenceEqual(visible));
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
        DescribeThings(second).ShouldBe(DescribeThings(first));
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
                $"{person.Id.Value} {person.Name} {string.Join(",", Enum.GetValues<TraitAxis>().Select(axis => person.Traits[axis]))} {person.Place.Value} " +
                string.Join(";", person.Relations.Select(r =>
                    $"{r.Other.Value}:{r.Opinion}:{r.Trust}:{r.Ties}:{string.Join(",", r.Impressions.OrderBy(i => i.Key))}")))
            .ToArray();

    private static string[] DescribeThings(World world) =>
        world.Things.Select(thing => $"{thing.Id.Value} {thing.Name} {thing.Kind} {thing.Importance}").ToArray();
}
