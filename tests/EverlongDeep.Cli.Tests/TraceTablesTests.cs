using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;
using Spectre.Console;

namespace EverlongDeep.Cli.Tests;

public class TraceTablesTests
{
    private static readonly Thing _tavern = new(new ThingId(0), "The Drowned Lantern", ThingKind.Place, 0.5);
    private static readonly Thing _heron = new(new ThingId(1), "Grey Heron", ThingKind.Ship, 0.5);
    private static readonly Thing _logbook = new(new ThingId(2), "logbook", ThingKind.Item, 0.3);
    private static readonly PersonId _maraId = new(0);
    private static readonly PersonId _pellId = new(1);
    private static readonly PersonId _bramId = new(2);
    private static readonly PersonId _fenId = new(3);
    private static readonly PersonId _ilseId = new(4);

    private readonly World _world = new(
        seed: 0,
        new Random(0),
        [_tavern, _heron, _logbook],
        [Someone(_maraId, "Mara"), Someone(_pellId, "Pell"), Someone(_bramId, "Bram"), Someone(_fenId, "Fen"), Someone(_ilseId, "Ilse")]);

    // Bram sank the Grey Heron at the tavern, and Fen saw it.
    private static readonly Claim _sank = new(Of(_bramId), Act.Sank, Of(_heron.Id), _tavern.Id, hour: -9, severity: 0.6, blame: 0.4, _fenId);

    [Fact]
    public void Changes_ListOneLinePerChange_ButNoTelling()
    {
        var seen = new Claim(Of(_bramId), Act.WasSeenAt, Of(_tavern.Id), _tavern.Id, hour: -5, severity: 0, blame: 0, _bramId);
        var helped = new Claim(Of(_maraId), Act.Helped, Of(_bramId), _tavern.Id, hour: -9, severity: 0.1, blame: 0, witness: null);
        BeliefSnapshot held = Snapshot(helped, heard: [new HeardEntry(_maraId, 10, helped)]);
        PersonSnapshot before = Pell([Relation(_maraId, 0.2, 0.6, (TraitAxis.Kind, 0.1))], [held]);
        PersonSnapshot after = Pell(
            [Relation(_maraId, 0.3, 0.5, (TraitAxis.Kind, 0.2)), Relation(_bramId, 0, 0.3)],
            [
                held with { ToldTo = [new ToldEntry(_bramId, 64, helped, helped)] },
                Snapshot(seen, heard: [new HeardEntry(_bramId, 70, seen)], confidence: 0.54),
            ]);

        TraceTables.Changes(_world, before, after).Count().ShouldBe(5);
    }

    [Fact]
    public void Changes_AHeldBeliefsNewVersion_AndEachMovedNumber_GetALineEach()
    {
        var byBram = new Claim(Of(_bramId), Act.Attacked, Of(_maraId), _tavern.Id, hour: -9, severity: 0.5, blame: 0, witness: null);
        Claim byPell = new(Of(_pellId), Act.Attacked, Of(_maraId), _tavern.Id, hour: -9, severity: 0.5, blame: 0, witness: null);
        BeliefSnapshot held = Snapshot(byBram);
        PersonSnapshot before = Pell([], [held]);

        TraceTables.Changes(_world, before, Pell([], [held with { Confidence = 0.5, Relevance = 0.2 }])).Count().ShouldBe(2);
        TraceTables.Changes(_world, before, Pell([], [held with { Claim = byPell, Importance = 0.8 }])).Count().ShouldBe(2);
    }

    [Fact]
    public void Changes_NothingMoved_ListsNothing()
    {
        PersonSnapshot pell = Pell([Relation(_maraId, 0.2, 0.6, (TraitAxis.Kind, 0.1))], []);

        TraceTables.Changes(_world, pell, pell with { }).ShouldBeEmpty();
    }

    [Fact]
    public void BeliefRows_ListBeliefsNamingAWitnessOrASpeakerInHeardFromOrToldTo_ButNotAPlaceAlone()
    {
        // The Grey Heron is in the told story, but only as this attack's place.
        BeliefSnapshot aboard = Snapshot(new Claim(PersonOrThing.Unknown, Act.Attacked, Of(_ilseId), _heron.Id, hour: -3, severity: 0.5, blame: 0, witness: null));
        BeliefSnapshot witnessed = Snapshot(new Claim(Of(_ilseId), Act.Robbed, Of(_logbook.Id), _tavern.Id, hour: -4, severity: 0.2, blame: 0.3, _fenId));
        var seen = new Claim(Of(_ilseId), Act.WasSeenAt, Of(_tavern.Id), _tavern.Id, hour: -2, severity: 0, blame: 0, witness: null);
        BeliefSnapshot fromTeller = Snapshot(seen, heard: [new HeardEntry(_pellId, 30, seen)]);
        var helped = new Claim(Of(_ilseId), Act.Helped, Of(_ilseId), _tavern.Id, hour: -2, severity: 0.1, blame: 0, witness: null);
        BeliefSnapshot toTeller = Snapshot(helped) with { ToldTo = [new ToldEntry(_pellId, 31, helped, helped)] };
        // Fen saw the sinking, but only told Pell of a theft that names nobody in the conversation.
        var stolen = new Claim(Of(_ilseId), Act.Robbed, Of(_logbook.Id), _tavern.Id, hour: -6, severity: 0.2, blame: 0.3, witness: null);
        BeliefSnapshot fromFen = Snapshot(stolen, heard: [new HeardEntry(_fenId, 20, stolen)]);
        PersonSnapshot pell = Pell([], [aboard, witnessed, fromFen]);

        Rows(Mara([], [aboard, fromTeller, toTeller])).ShouldBe(
        [
            ["p1 Pell", "p4 Ilse", "t2 logbook"],
            ["p0 Mara", "p4 Ilse", "t0 The Drowned Lantern"],
            ["p0 Mara", "p4 Ilse", "p4 Ilse"],
        ]);
        Rows(Mara([], [aboard])).ShouldBe([["p1 Pell", "p4 Ilse", "t2 logbook"]]);

        string[][] Rows(PersonSnapshot mara) =>
            [.. TraceTables.BeliefRows(_world, pell, mara, _sank, minute: 72).Select(row => new[] { row.Holder, row.Actor, row.Target })];
    }

    [Fact]
    public void WhoAndWhat_ListsThePeople_ThenTheStory_ThenWhomTheirBeliefsName_OnceEach()
    {
        // The teller did it, the target is a thing, and Fen saw it.
        Claim told = new(Of(_pellId), Act.Sank, Of(_heron.Id), _tavern.Id, hour: -9, severity: 0.6, blame: 0.4, _fenId);
        // Bram is named only as whom the robbery was heard from.
        var robbery = new Claim(Of(_ilseId), Act.Robbed, Of(_logbook.Id), _tavern.Id, hour: -4, severity: 0.2, blame: 0.3, _fenId);
        BeliefSnapshot robbed = Snapshot(robbery, heard: [new HeardEntry(_bramId, 15, robbery)]);
        PersonSnapshot pell = Pell([], [robbed]) with { Traits = new TraitProfile(new Dictionary<TraitAxis, double> { [TraitAxis.Brave] = 0.9 }) };

        Ids(TraceTables.WhoAndWhat(_world, pell, Mara([], []), told)).ShouldBe(["p1", "p0", "t1", "t0", "p3", "p2", "p4", "t2"]);
    }

    [Fact]
    public void WhoAndWhat_SeenAtAPlace_ListsThePlaceOnce()
    {
        Claim told = new(Of(_bramId), Act.WasSeenAt, Of(_tavern.Id), _tavern.Id, hour: -2, severity: 0, blame: 0, witness: null);

        Ids(TraceTables.WhoAndWhat(_world, Pell([], []), Mara([], []), told)).ShouldBe(["p1", "p0", "p2", "t0"]);
    }

    [Fact]
    public void WhoAndWhat_UnknownActor_IsNotListed()
    {
        Claim told = new(PersonOrThing.Unknown, Act.Sank, Of(_heron.Id), _tavern.Id, hour: -9, severity: 0.6, blame: 0, witness: null);

        Ids(TraceTables.WhoAndWhat(_world, Pell([], []), Mara([], []), told)).ShouldBe(["p1", "p0", "t1", "t0"]);
    }

    [Fact]
    public void Feelings_FromEachTowardTheOther_AndEachPersonTheStoryNames_ButNotThemselves()
    {
        // Mara is the target, so she has no line toward herself.
        Claim told = new(Of(_bramId), Act.Attacked, Of(_maraId), _tavern.Id, hour: -9, severity: 0.5, blame: 0.4, _fenId);
        PersonSnapshot pell = Pell([Relation(_maraId, 0.2, 0.6), Relation(_bramId, 0.1, 0.2)], []);

        TraceTables.Feelings(_world, pell, Mara([Relation(_pellId, -0.3, 0.5)], []), told).Select(feeling => feeling.Who).ShouldBe(
        [
            "p1 Pell → p0 Mara", "p1 Pell → p2 Bram", "p1 Pell → p3 Fen", "p0 Mara → p1 Pell", "p0 Mara → p2 Bram", "p0 Mara → p3 Fen",
        ]);
    }

    [Fact]
    public void Id_IsPOrTAndTheNumber_OrAQuestionMarkWhenUnknown()
    {
        TraceTables.Id(Of(_bramId)).ShouldBe("p2");
        TraceTables.Id(Of(_logbook.Id)).ShouldBe("t2");
        TraceTables.Id(PersonOrThing.Unknown).ShouldBe("?");
    }

    // Some outcomes are too rare to find in a seeded run, so each is rendered from a trace built for it.
    [Theory]
    [InlineData(Hearing.Believed)]
    [InlineData(Hearing.NotBelieved)]
    [InlineData(Hearing.SameTeller)]
    [InlineData(Hearing.KeptOwn)]
    [InlineData(Hearing.Surer)]
    [InlineData(Hearing.TookTellers)]
    [InlineData(Hearing.ThinksLying)]
    public void Render_EveryOutcome_WithOneLineOfProseNamingBothSpeakers(Hearing hearing)
    {
        // Pell heard from Fen that Bram sank the Grey Heron, and tells Mara a harsher version as hearsay.
        Claim told = _sank.WithDials(0.6, 0.6).WithWitness(null);
        var byFen = new Claim(Of(_fenId), Act.Sank, Of(_heron.Id), _tavern.Id, hour: -9, severity: 0.6, blame: 0.4, witness: null);
        (BeliefSnapshot[] Before, BeliefSnapshot[] After, double? Roll) listener = hearing switch
        {
            Hearing.Believed => ([], [Snapshot(told)], 0.2),
            Hearing.NotBelieved => ([], [], 0.9),
            Hearing.SameTeller or Hearing.Surer => ([Snapshot(told)], [Snapshot(told, confidence: 0.5)], null),
            Hearing.KeptOwn => ([Snapshot(_sank.WithWitness(_maraId))], [Snapshot(_sank.WithWitness(_maraId))], null),
            Hearing.TookTellers => ([Snapshot(byFen)], [Snapshot(told)], null),
            Hearing.ThinksLying => ([Snapshot(byFen, confidence: 0.7)], [Snapshot(byFen, confidence: 0.7)], null),
            _ => throw new ArgumentOutOfRangeException(nameof(hearing)),
        };
        double trustAfter = hearing == Hearing.ThinksLying ? 0.7 : 0.8;
        PersonSnapshot pell = Pell([Relation(_maraId, 0.1, 0.7), Relation(_bramId, -1, 0.5)], [Snapshot(_sank, heard: [new HeardEntry(_fenId, 10, _sank)])]);
        TraitProfile traits = pell.Traits;
        var trace = new ConversationTrace(startMinute: 60, pell, new Roll(Choice.Talk, traits, context: 0, value: 0))
        {
            EndMinute = 64,
            ListenerBefore = Mara([Relation(_pellId, 0.2, 0.8)], listener.Before),
            ListenerAfter = Mara([Relation(_pellId, 0.2, trustAfter)], listener.After),
            TellerAfter = pell,
            Held = _sank,
            Told = told,
            Coloring = new Coloring(0.1, OpinionSource.Thing, 0, OpinionSource.Known, -1, Harsher: true),
            HarsherBlame = new Roll(Choice.ColorBlameHarsher, traits, context: 0, value: 0),
            ClaimWitnessed = new Roll(Choice.ClaimWitnessed, traits, context: -2, value: 0.99),
            KeepWitness = new Roll(Choice.KeepWitness, traits, context: 1, value: 0.99),
            Believe = listener.Roll is { } value ? new Roll(Choice.Believe, traits, context: 0.6, value) : null,
            Offered = new ListenerConfidence(0.5, 0.8),
            Hearing = hearing,
        };

        string prose = TraceTables.Prose(_world, trace);
        prose.ShouldNotContain('\n');
        prose.ShouldContain("p1 Pell");
        prose.ShouldContain("p0 Mara");

        // A table that fits is as wide as its content; one that didn't would fill the width and wrap its cells.
        var output = new StringWriter();
        // As Program sets it up, so Spectre's CI detection doesn't change the borders under GitHub Actions.
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            Out = new AnsiConsoleOutput(output),
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
        });
        console.Profile.Width = TraceTables.Width;
        console.Write(TraceTables.Render(_world, trace));
        string[] tables = [.. output.ToString().Split('\n').Where(line => line.Length > 0 && "╭│├╰".Contains(line[0]))];
        tables.ShouldNotBeEmpty();
        tables.ShouldAllBe(line => line.TrimEnd().Length < TraceTables.Width);
    }

    private static string[] Ids(IEnumerable<(string Id, string Name, string Kind, string Role)> listed) => [.. listed.Select(entry => entry.Id)];

    private static PersonOrThing Of(PersonId person) => PersonOrThing.Of(person);

    private static PersonOrThing Of(ThingId thing) => PersonOrThing.Of(thing);

    private static Person Someone(PersonId id, string name) =>
        new(id, name, new TraitProfile(new Dictionary<TraitAxis, double>()), _tavern.Id);

    private static BeliefSnapshot Snapshot(Claim claim, HeardEntry[]? heard = null, double confidence = 0.4) =>
        new(claim, Importance: 0.1, confidence, Relevance: 0.1, heard ?? [], []);

    private static PersonSnapshot Pell(RelationSnapshot[] relations, BeliefSnapshot[] beliefs) => Snapshot(_pellId, "Pell", relations, beliefs);

    private static PersonSnapshot Mara(RelationSnapshot[] relations, BeliefSnapshot[] beliefs) => Snapshot(_maraId, "Mara", relations, beliefs);

    private static PersonSnapshot Snapshot(PersonId id, string name, RelationSnapshot[] relations, BeliefSnapshot[] beliefs) =>
        new(id, name, _tavern.Id, new TraitProfile(new Dictionary<TraitAxis, double>()), relations, beliefs);

    private static RelationSnapshot Relation(PersonId other, double opinion, double trust, params (TraitAxis Axis, double Value)[] impressions) =>
        new(other, opinion, trust, Ties.None, impressions.ToDictionary(impression => impression.Axis, impression => impression.Value));
}
