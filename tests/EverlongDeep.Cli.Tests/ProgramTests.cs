using System.Text.RegularExpressions;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Events;

namespace EverlongDeep.Cli.Tests;

public class ProgramTests
{
    [Fact]
    public void Run_SameSeed_PrintsTheSameTavern()
    {
        (int exitCode, string first, string error) = Run("--seed", "42", "--people", "6");

        exitCode.ShouldBe(0);
        error.ShouldBeEmpty();
        first.Split('\n')[0].Trim().ShouldBe("00:00:00 Seed: 42");

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
        firstLine.ShouldStartWith("00:00:00 Seed: ");
        string seed = firstLine["00:00:00 Seed: ".Length..];
        Run("--seed", seed, "--people", Program.DefaultPeople.ToString()).Output.ShouldBe(output);
    }

    [Fact]
    public void Run_Hours_StepsThatManyHours_AfterTheSetup()
    {
        string before = Run("--seed", "42", "--people", "12", "--hours", "0").Output;
        string after = Run("--seed", "42", "--people", "12", "--hours", "200").Output;

        // The setup's log lines tell of the start or the past; the run's, of later.
        string[] setupLog = [.. before.Split('\n').Where(line => IsTimed(line))];
        setupLog.ShouldNotBeEmpty();
        setupLog.ShouldAllBe(line => line.StartsWith('-') || line.StartsWith("00:00:00 "));
        after.ShouldStartWith(before);
        string[] runLines = after[before.Length..].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        runLines.ShouldNotBeEmpty();
        runLines.ShouldAllBe(line => IsTimed(line) && !line.StartsWith('-') && !line.StartsWith("00:00:00 "));
    }

    // A log line starts with its time, such as "27:05:12 ", or "-03:00:00 " for the past.
    private static bool IsTimed(string line) => Regex.IsMatch(line, @"^-?\d{2,}:\d{2}:\d{2} ");

    [Fact]
    public void Run_StrangerKills_ChangesTheRunFromItsHourOn()
    {
        string[] scenario = ["--scenario", Scenarios.StrangerKillsName, "--seed", "42"];
        long hour = Scenarios.StrangerKillsTick / Clock.TicksPerHour;
        string untilIt = Run("--seed", "42", "--hours", $"{hour}").Output;

        Run([.. scenario, "--hours", $"{hour}"]).Output.ShouldBe(untilIt);

        (int exitCode, string withIt, _) = Run([.. scenario, "--hours", $"{hour + 1}"]);
        exitCode.ShouldBe(0);
        withIt.ShouldStartWith(untilIt);
        withIt.ShouldNotBe(Run("--seed", "42", "--hours", $"{hour + 1}").Output);
    }

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
    public void Run_Trace_ReplacesEachTellingWithItsHeadingAndProse_AfterOneLegend_AndOtherwisePrintsTheSame()
    {
        string[] plain = Run("--seed", "42", "--people", "6", "--hours", "2").Output.Split('\n');
        (int exitCode, string output, _) = Run("--trace", "--seed", "42", "--people", "6", "--hours", "2");

        exitCode.ShouldBe(0);
        Run("--seed", "42", "--trace", "--people", "6", "--hours", "2").Output.ShouldBe(output);
        string[] lines = output.Split('\n');
        WithoutTrace(lines).ShouldBe(plain.Where(line => !IsTelling(line)));
        lines.ShouldNotContain(line => IsTelling(line));

        int tellings = plain.Count(IsTelling);
        tellings.ShouldBeGreaterThan(0);
        lines.Count(IsClosingRule).ShouldBe(tellings);
        int[] headings = [.. Enumerable.Range(0, lines.Length).Where(i => IsHeading(lines[i]))];
        headings.Length.ShouldBe(tellings);
        // One line of prose follows each heading, before its first table.
        headings.ShouldAllBe(i => lines[i + 1].Length > 0 && !"╭│├╰─ ".Contains(lines[i + 1][0]) && lines[i + 2].Trim() == TraceTables.WhoTitle);
        string[] titles = [TraceTables.WhoTitle, TraceTables.FeelingsTitle, TraceTables.BeliefsTitle, TraceTables.ChoicesTitle, TraceTables.StoryTitle, TraceTables.ChangesTitle];
        foreach (string title in titles)
        {
            lines.Count(line => line.Trim() == title).ShouldBe(tellings, title);
        }

        lines.Count(line => line.Trim() == TraceTables.LegendTitle).ShouldBe(1);
        Array.FindIndex(lines, line => line.Trim() == TraceTables.LegendTitle).ShouldBeLessThan(headings[0]);
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
        string[] traced = [.. Blocks(lines).SelectMany(block => block)];
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

    // The output without the legend and the traces.
    private static IEnumerable<string> WithoutTrace(string[] lines)
    {
        bool inLegend = false;
        bool inBlock = false;
        foreach (string line in lines)
        {
            inLegend = inLegend || line.Trim() == TraceTables.LegendTitle;
            inBlock = inBlock || IsHeading(line);
            if (!inLegend && !inBlock)
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
        ["--scenario"],
        ["--scenario", "stranger"],
        ["--scenario", Scenarios.StrangerKillsName, "--scenario", Scenarios.StrangerKillsName],
        ["--scenario", Scenarios.StrangerKillsName, "--people", (Scenarios.StrangerKillsPeople - 1).ToString()],
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
