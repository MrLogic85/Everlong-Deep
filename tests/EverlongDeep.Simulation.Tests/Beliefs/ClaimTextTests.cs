using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public class ClaimTextTests
{
    [Fact]
    public void Describe_NamesEachPartOfTheClaim_ReadingTheThingsArticles()
    {
        var mara = new Person(new PersonId(0), "Mara", [Trait.Honest], new ThingId(0));
        var pell = new Person(new PersonId(1), "Pell", [Trait.Gossip], new ThingId(0));
        var tavern = new Thing(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);
        var ship = new Thing(new ThingId(1), "Saltwren", ThingKind.Ship, 0.5);
        var watch = new Thing(new ThingId(2), "brass watch", ThingKind.Item, 0.5);
        var world = new World(seed: 0, new Random(0), [tavern, ship, watch], [mara, pell]);

        ClaimText.Describe(world, Claim(PersonOrThing.Unknown, Act.Sank, PersonOrThing.Of(ship.Id), ship.Id))
            .ShouldBe("Someone sank the Saltwren");
        ClaimText.Describe(world, Claim(PersonOrThing.Of(mara.Id), Act.Robbed, PersonOrThing.Of(watch.Id), tavern.Id))
            .ShouldBe("Mara stole the brass watch at The Drowned Lantern");
        ClaimText.Describe(world, Claim(PersonOrThing.Of(pell.Id), Act.Attacked, PersonOrThing.Of(mara.Id), tavern.Id))
            .ShouldBe("Pell attacked Mara at The Drowned Lantern");
        ClaimText.Describe(world, Claim(PersonOrThing.Of(pell.Id), Act.Betrayed, PersonOrThing.Of(mara.Id), tavern.Id))
            .ShouldBe("Pell betrayed Mara at The Drowned Lantern");
        ClaimText.Describe(world, Claim(PersonOrThing.Of(mara.Id), Act.Helped, PersonOrThing.Of(pell.Id), tavern.Id))
            .ShouldBe("Mara helped Pell at The Drowned Lantern");
        ClaimText.Describe(world, Claim(PersonOrThing.Unknown, Act.Killed, PersonOrThing.Of(pell.Id), tavern.Id))
            .ShouldBe("Someone killed Pell at The Drowned Lantern");
        ClaimText.Describe(world, Claim(PersonOrThing.Of(mara.Id), Act.WasSeenAt, PersonOrThing.Of(tavern.Id), tavern.Id))
            .ShouldBe("Mara was seen at The Drowned Lantern");
    }

    private static Claim Claim(PersonOrThing actor, Act act, PersonOrThing target, ThingId place) =>
        new(actor, act, target, place, hour: -1, severity: 0.5, blame: 0, witness: null);
}
