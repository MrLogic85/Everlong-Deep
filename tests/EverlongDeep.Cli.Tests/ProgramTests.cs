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
    public void Run_Trace_FollowsEachTellingWithItsBlock_AndOtherwisePrintsTheSame()
    {
        string[] plain = Run("--seed", "42", "--people", "6", "--hours", "2").Output.Split('\n');
        (int exitCode, string output, _) = Run("--trace", "--seed", "42", "--people", "6", "--hours", "2");

        exitCode.ShouldBe(0);
        Run("--seed", "42", "--trace", "--people", "6", "--hours", "2").Output.ShouldBe(output);
        string[] lines = output.Split('\n');
        WithoutBlocks(lines).ShouldBe(plain);

        int tellings = plain.Count(line => line.Contains(" tells "));
        tellings.ShouldBeGreaterThan(0);
        lines.Count(IsClosingRule).ShouldBe(tellings);
        lines.Count(IsHeading).ShouldBe(tellings);
        string[] titles = [TraceTables.IdsTitle, TraceTables.BeforeTitle, TraceTables.BeliefsTitle, TraceTables.TalkTitle, TraceTables.ChoicesTitle, TraceTables.StoryTitle, TraceTables.ChangesTitle];
        foreach (string title in titles)
        {
            lines.Count(line => line.Trim() == title).ShouldBe(tellings, title);
        }

        // Each telling picks one listener and one topic.
        lines.Count(line => line.Contains("│ picked │")).ShouldBe(2 * tellings);
        lines.ShouldContain(line => line.StartsWith("│ Talk now "));
    }

    [Fact]
    public void Run_Trace_HeadingOutcome_MatchesTheBelieveRow_AndEveryUnrolledChoiceSaysWhy()
    {
        string[] lines = Run("--trace", "--seed", "42", "--people", "6", "--hours", "6").Output.Split('\n');
        var outcomes = new HashSet<string>();

        for (int start = Array.FindIndex(lines, IsHeading); start >= 0; start = Array.FindIndex(lines, start + 1, IsHeading))
        {
            int end = Array.FindIndex(lines, start, IsClosingRule);
            string[] block = lines[start..end];
            string believe = block.Single(line => line.StartsWith("│ Believe the teller "));
            string outcome = block[0] switch
            {
                var heading when heading.Contains(" already knew ") => "│ not rolled, ",
                var heading when heading.Contains(" doesn't believe it ") => "✗ no",
                var heading when heading.Contains(" believes it ") => "✓ yes",
                var heading => throw new ShouldAssertException($"No outcome in: {heading}"),
            };
            believe.ShouldContain(outcome);
            outcomes.Add(outcome);

            // An unrolled choice shows a dash for its result, and its why says it wasn't rolled.
            block.Where(line => line.Contains("│ —      │")).ShouldAllBe(line => line.Contains("│ not rolled, "));
        }

        outcomes.Count.ShouldBe(3);
    }

    [Fact]
    public void Run_Trace_ColorsOnlyWhenAsked()
    {
        string[] run = ["--trace", "--seed", "42", "--people", "6", "--hours", "2"];

        Run(run).Output.ShouldNotContain('\u001b');

        var output = new StringWriter();
        Program.Run(run, output, new StringWriter(), color: true).ShouldBe(0);
        output.ToString().ShouldContain('\u001b');
    }

    [Fact]
    public void Run_Trace_EveryIdAnEntryNames_IsInTheIdsTable()
    {
        string[] lines = Run("--trace", "--seed", "42", "--people", "6", "--hours", "2").Output.Split('\n');
        int entries = 0;

        for (int start = Array.FindIndex(lines, IsHeading); start >= 0; start = Array.FindIndex(lines, start + 1, IsHeading))
        {
            string[] block = lines[start..Array.FindIndex(lines, start, IsClosingRule)];
            string[] listed = [.. block.Select(line => Regex.Match(line, @"^│ (p\d+) ")).Where(match => match.Success).Select(match => match.Groups[1].Value)];
            string[] named = [.. block.Select(line => Regex.Match(line, @"│ (?:from|to) (p\d+) ")).Where(match => match.Success).Select(match => match.Groups[1].Value)];
            named.ShouldBeSubsetOf(listed);
            entries += named.Length;
        }

        entries.ShouldBeGreaterThan(0);
    }

    private static bool IsHeading(string line) => line.StartsWith("── ") && line.Contains(" tells ");

    // A bare rule closes each trace.
    private static bool IsClosingRule(string line) => line.Length > 0 && line.All(character => character == '─');

    // The output without the traces, each of which runs from the line after a telling to its closing rule.
    private static IEnumerable<string> WithoutBlocks(string[] lines)
    {
        bool inBlock = false;
        foreach (string line in lines)
        {
            if (!inBlock)
            {
                yield return line;
            }

            inBlock = inBlock ? !IsClosingRule(line) : IsTimed(line) && line.Contains(" tells ");
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
