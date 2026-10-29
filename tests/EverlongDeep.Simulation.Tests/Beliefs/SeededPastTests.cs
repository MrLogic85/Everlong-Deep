using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public class SeededPastTests
{
    private const double _tolerance = 1e-9;

    // Enough seeds and sizes to draw every act, both actor kinds and both kinds of hearsay.
    private static World[] Worlds() =>
    [
        .. from seed in Enumerable.Range(0, 30)
           from people in new[] { 3, 6, 16 }
           select WorldBuilder.Tavern(seed, people, new SimulationLog()),
    ];

    [Fact]
    public void Tavern_EveryClaim_FitsTheActTable_AndNamesTheWorld()
    {
        World[] worlds = Worlds();

        foreach (World world in worlds)
        {
            ThingId tavern = world.Things.Single(thing => thing.Kind == ThingKind.Place).Id;
            foreach (Claim claim in Beliefs(world).Select(belief => belief.Claim))
            {
                claim.Hour.ShouldBeInRange(-SeededPast.Days * 24, -1);
                claim.Actor.ShouldNotBe(claim.Target);
                claim.Actor.Thing.ShouldBeNull();
                if (claim.Actor.Person is { } actor)
                {
                    world.Person(actor);
                }

                if (claim.Witness is { } witness)
                {
                    world.Person(witness);
                    ((PersonId?)witness).ShouldNotBe(claim.Actor.Person);
                }

                switch (claim.Act)
                {
                    case Act.Sank:
                        Thing ship = world.Thing(claim.Target.Thing!.Value);
                        ship.Kind.ShouldBe(ThingKind.Ship);
                        claim.Place.ShouldBe(ship.Id);
                        break;
                    case Act.Robbed:
                        world.Thing(claim.Target.Thing!.Value).Kind.ShouldBe(ThingKind.Item);
                        claim.Place.ShouldBe(tavern);
                        break;
                    case Act.Attacked or Act.Betrayed or Act.Helped:
                        claim.Actor.IsUnknown.ShouldBeFalse();
                        world.Person(claim.Target.Person!.Value);
                        claim.Place.ShouldBe(tavern);
                        claim.Severity.ShouldBe(Claim.BaseSeverity(claim.Act));
                        break;
                    case Act.WasSeenAt:
                        claim.Actor.IsUnknown.ShouldBeFalse();
                        Thing seenAt = world.Thing(claim.Target.Thing!.Value);
                        (seenAt.Kind == ThingKind.Ship || seenAt.Id == tavern).ShouldBeTrue();
                        claim.Place.ShouldBe(seenAt.Id);
                        claim.Severity.ShouldBe(0);
                        break;
                    default:
                        throw new ShouldAssertException($"{claim.Act} is not drawn yet");
                }
            }
        }

        Claim[] claims = [.. worlds.SelectMany(Beliefs).Select(belief => belief.Claim)];
        claims.Select(claim => claim.Act).Distinct().Count().ShouldBe(6);
        claims.ShouldContain(claim => claim.Actor.IsUnknown);
    }

    [Fact]
    public void Tavern_DialsAndConfidence_StayInTheirRanges_AndRelevanceStartsAtImportance()
    {
        World[] worlds = Worlds();

        foreach (World world in worlds)
        {
            foreach (Belief belief in Beliefs(world))
            {
                Claim claim = belief.Claim;
                claim.Severity.ShouldBeInRange(0, 1);
                bool blamed = claim.Act is not (Act.Helped or Act.WasSeenAt) && !claim.Actor.IsUnknown;
                if (blamed)
                {
                    claim.Blame.ShouldBeInRange(Claim.HarmfulBlame.Min, Claim.HarmfulBlame.Max);
                }
                else
                {
                    claim.Blame.ShouldBe(0);
                }

                (double Min, double Max) confidence = belief.IsFirstHand
                    ? SeededPast.FirstHandConfidence
                    : SeededPast.HearsayConfidence;
                belief.Confidence.ShouldBeInRange(confidence.Min, confidence.Max);

                // The past is told before any coloring, so its severity is the table's; ties and opinions only add.
                double ownStake = claim.Involves(belief.Holder) ? Importance.OwnStoryStake : 0;
                belief.Importance.ShouldBeInRange(Math.Min(1, claim.Severity + ownStake) - _tolerance, 1);
                belief.Relevance.ShouldBe(belief.Importance, _tolerance);
            }
        }
    }

    [Fact]
    public void Tavern_Hearsay_IsTheSameClaim_HeldLessSureThanFirstHand()
    {
        World[] worlds = Worlds();

        foreach (World world in worlds)
        {
            foreach (IGrouping<Claim, Belief> story in Events(world))
            {
                Belief[] firstHand = [.. story.Where(belief => belief.IsFirstHand)];
                Belief[] hearsay = [.. story.Where(belief => !belief.IsFirstHand)];
                firstHand.ShouldNotBeEmpty();
                PersonId[] witnesses = [.. firstHand.Select(belief => belief.Holder)];
                hearsay.ShouldAllBe(belief => belief.Claim.Witness == null || witnesses.Contains(belief.Claim.Witness.Value));
                if (hearsay.Length > 0)
                {
                    hearsay.Max(belief => belief.Confidence).ShouldBeLessThan(firstHand.Min(belief => belief.Confidence));
                }
            }
        }

        Belief[] allHearsay = [.. worlds.SelectMany(Beliefs).Where(belief => !belief.IsFirstHand)];
        allHearsay.ShouldContain(belief => belief.Claim.Witness == null);
        allHearsay.ShouldContain(belief => belief.Claim.Witness != null);
    }

    [Fact]
    public void Tavern_SankAndRobbed_ScaleSeverityWithTheTargetsImportance()
    {
        World[] worlds = Worlds();

        var acts = new HashSet<Act>();
        foreach (World world in worlds)
        {
            foreach (Claim claim in Beliefs(world).Select(belief => belief.Claim).Where(claim => claim.Act is Act.Sank or Act.Robbed))
            {
                double importance = world.Thing(claim.Target.Thing!.Value).Importance;
                claim.Severity.ShouldBe(Claim.BaseSeverity(claim.Act) * (0.5 + (importance / 2)), _tolerance);
                acts.Add(claim.Act);
            }
        }

        acts.ShouldBe([Act.Sank, Act.Robbed], ignoreOrder: true);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(16)]
    public void Tavern_ThreeOrMorePeople_NeverHaveEveryoneAsAWitness(int peopleCount)
    {
        foreach (int seed in Enumerable.Range(0, 30))
        {
            World world = WorldBuilder.Tavern(seed, peopleCount, new SimulationLog());
            IGrouping<Claim, Belief>[] events = Events(world);

            events.Length.ShouldBe(Math.Min(SeededPast.MaxEvents, (int)Math.Ceiling(peopleCount / 3.0)));
            foreach (IGrouping<Claim, Belief> story in events)
            {
                int witnesses = story.Count(belief => belief.IsFirstHand);
                witnesses.ShouldBeInRange(1, Math.Max(1, peopleCount / 3));
                witnesses.ShouldBeLessThan(peopleCount);
                story.Count(belief => !belief.IsFirstHand).ShouldBeInRange(0, peopleCount / 3);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Tavern_FewerThanTwoPeople_HaveNoPast(int peopleCount)
    {
        var log = new SimulationLog();

        World world = WorldBuilder.Tavern(seed: 42, peopleCount, log);

        Beliefs(world).ShouldBeEmpty();
        log.Lines.ShouldNotContain(line => line.StartsWith("Past"));
    }

    [Fact]
    public void Tavern_TwoPeople_EachEventIsSeenByThePersonWhoIsNotTheActor()
    {
        foreach (int seed in Enumerable.Range(0, 30))
        {
            World world = WorldBuilder.Tavern(seed, peopleCount: 2, new SimulationLog());

            IGrouping<Claim, Belief> story = Events(world).ShouldHaveSingleItem();
            Belief witness = story.Where(belief => belief.IsFirstHand).ShouldHaveSingleItem();
            ((PersonId?)witness.Holder).ShouldNotBe(story.Key.Actor.Person);
        }
    }

    [Fact]
    public void Tavern_LogsEachEvent_WithItsWitnesses()
    {
        var log = new SimulationLog();

        World world = WorldBuilder.Tavern(seed: 42, peopleCount: 6, log);

        string[] pastLines = [.. log.Lines.Where(line => line.StartsWith("Past"))];
        IGrouping<Claim, Belief>[] events = Events(world);
        pastLines.Length.ShouldBe(events.Length);
        foreach (IGrouping<Claim, Belief> story in events)
        {
            string witnesses = string.Join(", ", story.Where(belief => belief.IsFirstHand).Select(belief => world.Person(belief.Holder).Name));
            pastLines.ShouldContain(line =>
                line.StartsWith($"Past, hour {story.Key.Hour}: {ClaimText.Describe(world, story.Key)} (")
                && line.EndsWith($"seen by {witnesses}"));
        }
    }

    [Fact]
    public void Tavern_SameSeed_DrawsTheSamePast()
    {
        var firstLog = new SimulationLog();
        var secondLog = new SimulationLog();

        World first = WorldBuilder.Tavern(seed: 7, peopleCount: 12, firstLog);
        World second = WorldBuilder.Tavern(seed: 7, peopleCount: 12, secondLog);

        Beliefs(first).ShouldNotBeEmpty();
        Describe(second).ShouldBe(Describe(first));
        secondLog.Lines.ShouldBe(firstLog.Lines);
        Describe(WorldBuilder.Tavern(seed: 8, peopleCount: 12, new SimulationLog())).ShouldNotBe(Describe(first));
    }

    private static IEnumerable<Belief> Beliefs(World world) => world.People.SelectMany(person => person.Beliefs);

    // An event's claims differ only in their witness.
    private static IGrouping<Claim, Belief>[] Events(World world) =>
        [.. Beliefs(world).GroupBy(belief => belief.Claim.WithWitness(null))];

    private static string[] Describe(World world) =>
        [
            .. Beliefs(world).Select(belief =>
                $"{belief.Holder.Value} {belief.Claim} {belief.Confidence} {belief.Relevance}"),
        ];
}
