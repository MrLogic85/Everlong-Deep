using System.Text.RegularExpressions;
using EverlongDeep.Simulation;

namespace EverlongDeep.Cli.Tests;

public class ProgramTests
{
    [Fact]
    public void Run_SameSeed_PrintsTheSameTavern()
    {
        (int exitCode, string first, string error) = Run("--seed", "42", "--people", "6");

        exitCode.ShouldBe(0);
        error.ShouldBeEmpty();
        first.Split('\n')[0].Trim().ShouldBe("Seed: 42");

        Run("--seed", "42", "--people", "6").Output.ShouldBe(first);

        // Every run prints its own seed, so only the people show whether another seed builds another tavern.
        People(Run("--seed", "43", "--people", "6").Output).ShouldNotBe(People(first));
    }

    [Fact]
    public void Run_WithoutSeed_PrintsASeedThatReproducesTheRun()
    {
        (int exitCode, string output, _) = Run();

        exitCode.ShouldBe(0);
        string firstLine = output.Split('\n')[0].Trim();
        firstLine.ShouldStartWith("Seed: ");
        string seed = firstLine["Seed: ".Length..];
        Run("--seed", seed, "--people", Program.DefaultPeople.ToString()).Output.ShouldBe(output);
    }

    [Fact]
    public void Run_Hours_StepsThatManyHours_AfterPrintingRelations()
    {
        string before = Run("--seed", "42", "--people", "12", "--hours", "0").Output;
        string after = Run("--seed", "42", "--people", "12", "--hours", "200").Output;

        before.Split('\n').ShouldContain(line => line.StartsWith("  -> ") && line.Contains(": opinion "));
        before.Split('\n').ShouldNotContain(line => IsTimed(line));
        after.ShouldStartWith(before);
        string[] hourLines = after[before.Length..].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        hourLines.ShouldNotBeEmpty();
        hourLines.ShouldAllBe(line => IsTimed(line));
    }

    [Fact]
    public void Run_PrintsTheThings_BeforeThePeople()
    {
        string[] lines = Run("--seed", "42", "--people", "6", "--hours", "0").Output.Split('\n');

        int firstThing = Array.FindIndex(lines, line => line.StartsWith("Thing "));
        int lastThing = Array.FindLastIndex(lines, line => line.StartsWith("Thing "));
        int firstPerson = Array.FindIndex(lines, line => line.StartsWith('#'));
        firstThing.ShouldBeGreaterThan(0);
        lastThing.ShouldBeLessThan(firstPerson);
        lines[firstThing].Trim().ShouldBe("Thing 0 The Drowned Lantern (Place, importance 0.50)");
        lines[firstThing..(lastThing + 1)].ShouldContain(line => line.Contains("(Ship, importance "));
        lines[firstThing..(lastThing + 1)].ShouldContain(line => line.Contains("(Item, importance "));
    }

    [Fact]
    public void Run_PrintsThePast_AndEachPersonsBeliefs_AfterTheirRelations()
    {
        string[] lines = Run("--seed", "42", "--people", "16", "--hours", "0").Output.Split('\n');

        int firstThing = Array.FindIndex(lines, line => line.StartsWith("Thing "));
        lines[..firstThing].ShouldContain(line => line.StartsWith("Past, hour -"));
        string[] beliefLines = [.. lines.Where(IsBelief)];
        beliefLines.ShouldContain(line => line.StartsWith("  did: "));
        beliefLines.ShouldContain(line => line.StartsWith("  saw: "));
        beliefLines.ShouldContain(line => line.StartsWith("  heard: "));
        beliefLines.ShouldContain(line => line.StartsWith("  heard ") && line.Contains(" saw: "));
        beliefLines.ShouldAllBe(line => line.Contains(", hour -") && line.Contains("(importance "));

        // Within each person, relations come first and beliefs last.
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("  -> "))
            {
                IsBelief(lines[i - 1]).ShouldBeFalse();
            }
        }
    }

    // A line from a simulated hour starts with its time, such as "27:05 ".
    private static bool IsTimed(string line) => Regex.IsMatch(line, @"^\d{2,}:\d{2} ");

    private static bool IsBelief(string line) => line.StartsWith("  did: ") || line.StartsWith("  saw: ") || line.StartsWith("  heard");

    public static TheoryData<int> PeopleBounds => [0, WorldBuilder.MaxPeople];

    [Theory]
    [MemberData(nameof(PeopleBounds))]
    public void Run_PeopleAtTheBounds_PrintsThatManyPeople(int people)
    {
        (int exitCode, string output, _) = Run("--people", people.ToString());

        exitCode.ShouldBe(0);
        People(output).Length.ShouldBe(people);
    }

    [Fact]
    public void Run_Trace_OpensEachTellingWithItsHeading_AfterOneLegend_AndOtherwisePrintsTheSame()
    {
        string[] plain = Run("--seed", "42", "--people", "6", "--hours", "2").Output.Split('\n');
        (int exitCode, string output, _) = Run("--trace", "--seed", "42", "--people", "6", "--hours", "2");

        exitCode.ShouldBe(0);
        Run("--seed", "42", "--trace", "--people", "6", "--hours", "2").Output.ShouldBe(output);
        string[] lines = output.Split('\n');
        WithoutTrace(lines).ShouldBe(plain);

        int tellings = plain.Count(IsTelling);
        tellings.ShouldBeGreaterThan(0);
        lines.Count(IsClosingRule).ShouldBe(tellings);
        int[] headings = [.. Enumerable.Range(0, lines.Length).Where(i => IsHeading(lines[i]))];
        headings.Length.ShouldBe(tellings);
        headings.ShouldAllBe(i => IsTelling(lines[i + 1]));
        string[] titles = [TraceTables.WhoTitle, TraceTables.FeelingsTitle, TraceTables.BeliefsTitle, TraceTables.ChoicesTitle, TraceTables.StoryTitle, TraceTables.ChangesTitle];
        foreach (string title in titles)
        {
            lines.Count(line => line.Trim() == title).ShouldBe(tellings, title);
        }

        lines.Count(line => line.Trim() == TraceTables.LegendTitle).ShouldBe(1);
        Array.FindIndex(lines, line => line.Trim() == TraceTables.LegendTitle).ShouldBeLessThan(headings[0]);

        // Each telling picks one listener and one topic.
        lines.Count(line => line.Contains("│ picked │")).ShouldBe(2 * tellings);
        lines.ShouldContain(line => line.StartsWith("│ Talk now "));
    }

    [Fact]
    public void Run_Trace_HeadingOutcome_MatchesTheBelieveRow_AndEveryUnrolledChoiceSaysWhy()
    {
        string[] lines = Run("--trace", "--seed", "42", "--people", "6", "--hours", "6").Output.Split('\n');
        var outcomes = new HashSet<string>();

        foreach (string[] block in Blocks(lines))
        {
            string believe = block.Single(line => line.StartsWith("│ Believe the teller "));
            string outcome = block[0] switch
            {
                var heading when heading.Contains(" doesn't believe it ") => "✗ no",
                var heading when heading.Contains(" believes it ") => "✓ yes",
                var heading when heading.Contains(" heard it from them before ") || heading.Contains(" is surer ")
                    || heading.Contains(" keeps their own, ") || heading.Contains("'s version ") || heading.Contains(" is lying ") =>
                    "│ not rolled, ",
                var heading => throw new ShouldAssertException($"No outcome in: {heading}"),
            };
            believe.ShouldContain(outcome);
            outcomes.Add(outcome);

            // An unrolled choice shows a dash for its result, and its why says it wasn't rolled.
            Table(block, TraceTables.ChoicesTitle).Where(line => Regex.IsMatch(line, "│ — +│$")).ShouldAllBe(line => line.Contains("│ not rolled, "));
        }

        outcomes.Count.ShouldBe(3);
    }

    [Fact]
    public void Run_Trace_ColorsOnlyAtATerminal()
    {
        string[] run = ["--trace", "--seed", "42", "--people", "6", "--hours", "2"];

        Run(run).Output.ShouldNotContain('\u001b');

        var output = new StringWriter();
        Program.Run(run, output, new StringWriter(), terminal: true).ShouldBe(0);
        output.ToString().ShouldContain('\u001b');
    }

    [Fact]
    public void Run_Trace_WhoAndWhat_ListsExactlyWhomFeelingsBeliefsAndTheStoryName()
    {
        string[] lines = Run("--trace", "--seed", "42", "--people", "6", "--hours", "6").Output.Split('\n');
        int blocks = 0;

        foreach (string[] block in Blocks(lines))
        {
            string[] listed = [.. Table(block, TraceTables.WhoTitle).Select(line => Regex.Match(line, @"^│ ([pt]\d+) ")).Where(match => match.Success).Select(match => match.Groups[1].Value)];
            string[] named = [.. new[] { TraceTables.FeelingsTitle, TraceTables.BeliefsTitle, TraceTables.StoryTitle }
                .SelectMany(title => Table(block, title))
                .SelectMany(line => Regex.Matches(line, @"(?<!\w)([pt]\d+) ").Select(match => match.Groups[1].Value))];
            listed.ShouldBeUnique();
            listed.ShouldBe(named.Distinct(), ignoreOrder: true);
            blocks++;
        }

        blocks.ShouldBeGreaterThan(0);
    }

    // The run the owner reads, and the most people, whose tables are the widest.
    public static TheoryData<string[]> TracedRuns =>
    [
        ["--seed", "42"],
        ["--seed", "1", "--people", WorldBuilder.MaxPeople.ToString()],
        ["--seed", "7", "--people", WorldBuilder.MaxPeople.ToString()],
    ];

    [Theory]
    [MemberData(nameof(TracedRuns))]
    public void Run_Trace_NamesEveryoneByIdAndName_WithoutBeliefIdsOrWrappedCells(string[] args)
    {
        string output = Run(["--trace", .. args]).Output;
        string[] lines = output.Split('\n');
        (string Id, string Name)[] names =
        [
            .. lines.Select(line => Regex.Match(line, @"^#(\d+) (\S+) \(")).Where(match => match.Success).Select(match => ($"p{match.Groups[1].Value}", match.Groups[2].Value)),
            .. lines.Select(line => Regex.Match(line, @"^Thing (\d+) (.+?) \(")).Where(match => match.Success).Select(match => ($"t{match.Groups[1].Value}", match.Groups[2].Value)),
        ];
        string[] traced = [.. Blocks(lines).SelectMany(block => block).Where(line => !IsTelling(line))];
        names.ShouldNotBeEmpty();
        traced.ShouldNotBeEmpty();

        // An id comes just before its name, or in the cell before it in Who and what.
        foreach ((string id, string name) in names)
        {
            traced.ShouldNotContain(line => Regex.IsMatch(line, $@"(?<!\b{id} (?: *│ )?)\b{Regex.Escape(name)}\b"), $"{name} without {id}");
        }

        output.ShouldNotMatch(@"[pt]\d+/\w+/[pt]\d+");

        // A table that fits the width is as wide as its content; one that didn't would fill the width and wrap its cells.
        // Some are wider than Spectre's default 80 columns, so the run is not left at that width.
        string[] tables = [.. lines.Where(line => line.Length > 0 && "╭│├╰".Contains(line[0]))];
        tables.ShouldAllBe(line => line.Length < TraceTables.Width);
        tables.Max(line => line.Length).ShouldBeGreaterThan(80);
    }

    private static bool IsHeading(string line) => line.StartsWith("── ") && line.Contains(" tells ");

    private static bool IsTelling(string line) => IsTimed(line) && line.Contains(" tells ");

    // A bare rule closes each trace.
    private static bool IsClosingRule(string line) => line.Length > 0 && line.All(character => character == '─');

    // Each trace, from its heading to its closing rule.
    private static IEnumerable<string[]> Blocks(string[] lines)
    {
        for (int start = Array.FindIndex(lines, IsHeading); start >= 0; start = Array.FindIndex(lines, start + 1, IsHeading))
        {
            yield return lines[start..Array.FindIndex(lines, start, IsClosingRule)];
        }
    }

    // A table's lines, from its title to its bottom border.
    private static IEnumerable<string> Table(string[] block, string title) =>
        block.SkipWhile(line => line.Trim() != title).Skip(1).TakeWhile(line => !line.StartsWith('╰'));

    // The output without the legend and the traces, but with each telling's log line.
    private static IEnumerable<string> WithoutTrace(string[] lines)
    {
        bool inLegend = false;
        bool inBlock = false;
        foreach (string line in lines)
        {
            inLegend = inLegend || line.Trim() == TraceTables.LegendTitle;
            inBlock = inBlock || IsHeading(line);
            if (!inLegend && !(inBlock && !IsTelling(line)))
            {
                yield return line;
            }

            inLegend = inLegend && !line.StartsWith('╰');
            inBlock = inBlock && !IsClosingRule(line);
        }
    }

    public static TheoryData<string[]> MalformedArguments =>
    [
        ["--seed"],
        ["--seed", "x"],
        ["--people", "-1"],
        ["--people", (WorldBuilder.MaxPeople + 1).ToString()],
        ["--hours", "-1"],
        ["--hours", (Program.MaxHours + 1).ToString()],
        ["--seed", "1", "--seed", "2"],
        ["--speed", "1"],
        ["42"],
        ["--trace", "5"],
        ["--trace", "--trace"],
        ["--trace-json"],
        ["--trace-json", "trace.json"],
    ];

    [Theory]
    [MemberData(nameof(MalformedArguments))]
    public void Run_MalformedArguments_PrintsUsageAndFails(string[] args)
    {
        (int exitCode, string output, string error) = Run(args);

        exitCode.ShouldNotBe(0);
        output.ShouldBeEmpty();
        error.ShouldStartWith("Usage:");
    }

    private static string[] People(string output) =>
        output.Split('\n').Where(line => line.StartsWith('#')).ToArray();

    private static (int ExitCode, string Output, string Error) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        int exitCode = Program.Run(args, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }
}
