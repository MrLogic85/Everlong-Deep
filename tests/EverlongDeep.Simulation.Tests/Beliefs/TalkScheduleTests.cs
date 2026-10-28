using System.Text.RegularExpressions;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Simulation.Tests.Beliefs;

public partial class TalkScheduleTests
{
    private const int _hours = 12;

    // A seeded tavern's talk over half a day, with each world kept to name its people.
    private static (World World, string[] Lines)[] Runs() =>
    [
        .. Enumerable.Range(0, 20).Select(seed =>
        {
            var log = new SimulationLog();
            World world = WorldBuilder.Tavern(seed, 8, log);
            for (int hour = 0; hour < _hours; hour++)
            {
                world.StepHour(log);
            }

            return (world, log.Lines.Where(line => TellingLine().IsMatch(line)).ToArray());
        }),
    ];

    [Fact]
    public void StepHour_AStoryHeard_CanBeRetoldWithinTheSameHour()
    {
        bool retoldWithinTheHour = Runs().Any(run =>
        {
            LoggedTelling[] tellings = [.. run.Lines.Select(Parse)];
            return tellings.Any(heard => heard.Believed && tellings.Any(retold =>
                retold.Teller == heard.Listener && retold.Story == heard.Story &&
                retold.StartMinute >= heard.EndMinute && retold.EndMinute / 60 == heard.EndMinute / 60));
        });

        retoldWithinTheHour.ShouldBeTrue();
    }

    [Fact]
    public void StepHour_NobodyIsInTwoConversationsAtOnce()
    {
        foreach ((_, string[] lines) in Runs())
        {
            LoggedTelling[] tellings = [.. lines.Select(Parse)];
            foreach (string name in tellings.SelectMany(telling => new[] { telling.Teller, telling.Listener }).Distinct())
            {
                LoggedTelling[] theirs = [.. tellings.Where(telling => telling.Teller == name || telling.Listener == name).OrderBy(telling => telling.StartMinute)];
                for (int i = 1; i < theirs.Length; i++)
                {
                    theirs[i].StartMinute.ShouldBeGreaterThanOrEqualTo(theirs[i - 1].EndMinute, $"{name}: {theirs[i - 1]} overlaps {theirs[i]}");
                }
            }
        }
    }

    [Fact]
    public void StepHour_AcrossSeeds_TheFirstToTalk_IsNotAlwaysTheLowestId()
    {
        int[] firstTellers =
        [
            .. Runs().Where(run => run.Lines.Length > 0)
                .Select(run => run.World.People.Single(person => person.Name == Parse(run.Lines[0]).Teller).Id.Value),
        ];

        firstTellers.Length.ShouldBeGreaterThan(10);
        firstTellers.Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void StepHour_SameSeed_GivesTheSameTalk()
    {
        (World World, string[] Lines)[] runs = Runs();

        runs.ShouldAllBe(run => run.Lines.Length > 0);
        runs.SelectMany(run => run.Lines).ShouldContain(line => line.Contains("a stranger met"));
        Runs().Select(run => run.Lines).ShouldBe(runs.Select(run => run.Lines));
    }

    [Fact]
    public void StepHour_Traced_ChangesNothing_AndTracesEachTelling()
    {
        foreach (int seed in Enumerable.Range(0, 20))
        {
            (World plain, SimulationLog plainLog) = Run(seed, traces: null);
            var traces = new List<ConversationTrace>();
            (World traced, SimulationLog tracedLog) = Run(seed, traces);

            tracedLog.Lines.ShouldBe(plainLog.Lines);
            State(traced).ShouldBe(State(plain));
            traces.Count.ShouldBe(tracedLog.Lines.Count(line => TellingLine().IsMatch(line)));
        }

        static (World, SimulationLog) Run(int seed, List<ConversationTrace>? traces)
        {
            var log = new SimulationLog();
            World world = WorldBuilder.Tavern(seed, 8, log);
            if (traces is not null)
            {
                world.TraceConversations(traces.Add);
            }

            for (int hour = 0; hour < _hours; hour++)
            {
                world.StepHour(log);
            }

            return (world, log);
        }

        // Everything a conversation can change, and the next draw, which shows no extra draw was made.
        static string[] State(World world) =>
        [
            .. world.People.SelectMany(person =>
                person.Beliefs.Select(belief => $"{person.Id} {belief.Claim} {belief.Confidence}")
                    .Concat(person.Relations.Select(relation => $"{person.Id} {relation.Other} {relation.Opinion} {relation.Trust} {relation.Ties}"))),
            $"next {world.Random.NextDouble()}",
        ];
    }

    [Fact]
    public void StepHour_SomeoneWhoKeepsQuiet_ConsidersAgainLater()
    {
        var tavern = new Thing(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);
        Person mara = Someone(0, 0.6);
        Person pell = Someone(1, 0);
        mara.Meet(pell.Id, opinion: 0.4, trust: 0.6);
        pell.Meet(mara.Id, opinion: 0.4, trust: 0.6);
        mara.Believe(
            new Claim(PersonOrThing.Unknown, Act.WasSeenAt, PersonOrThing.Of(tavern.Id), tavern.Id, hour: -5, severity: 0, blame: 0, witness: null),
            confidence: 0.9, relevance: 0.5);

        // Mara's moment is minute 0 and Pell's 30. Mara first keeps quiet, so her wait of 5 minutes (a draw of 0) brings
        // her back at minute 5, and every later draw says yes: she tells Pell for 4 minutes.
        var world = new World(seed: 0, new ScriptedThenFixedRandom(0, 0, 0, 0.5, 0.99), [tavern], [mara, pell]);
        var log = new SimulationLog();
        world.StepHour(log);

        log.Lines[0].ShouldStartWith("00:09 Person 0 tells Person 1 (4 min; ");
    }

    [Theory]
    [InlineData(0, 0, 3)]
    [InlineData(1, 1, 7)]
    [InlineData(-0.5, -0.5, 1)]
    [InlineData(-1, -1, 1)]
    public void Length_GrowsWithHowTalkativeThePairIs_AndLastsAtLeastAMinute(double teller, double listener, int expected)
    {
        TalkSchedule.Length(Someone(0, teller), Someone(1, listener)).ShouldBe(expected);
    }

    private static Person Someone(int id, double talkative) =>
        new(new PersonId(id), $"Person {id}", new TraitProfile(new Dictionary<TraitAxis, double> { [TraitAxis.Talkative] = talkative }), new ThingId(0));

    private static LoggedTelling Parse(string line)
    {
        Match match = TellingLine().Match(line);
        int end = (int.Parse(match.Groups["hour"].Value) * 60) + int.Parse(match.Groups["minute"].Value);
        return new LoggedTelling(
            match.Groups["teller"].Value,
            match.Groups["listener"].Value,
            end - int.Parse(match.Groups["length"].Value),
            end,
            match.Groups["story"].Value,
            line.Contains(" believes it ("));
    }

    // "03:07 Esker tells Hollis (4 min; …): Bram betrayed Jory at The Drowned Lantern, …"
    [GeneratedRegex(@"^(?<hour>\d{2,}):(?<minute>\d{2}) (?<teller>\w+) tells (?<listener>\w+) \((?<length>\d+) min;[^)]*\): (?<story>[^,]+),")]
    private static partial Regex TellingLine();

    private sealed record LoggedTelling(string Teller, string Listener, int StartMinute, int EndMinute, string Story, bool Believed);
}
