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
        beliefLines.ShouldContain(line => line.StartsWith("  saw: "));
        beliefLines.ShouldContain(line => line.StartsWith("  heard: "));
        beliefLines.ShouldContain(line => line.StartsWith("  heard ") && line.Contains(" saw: "));
        beliefLines.ShouldAllBe(line => line.Contains(", hour -") && line.Contains("(confidence "));

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

    private static bool IsBelief(string line) => line.StartsWith("  saw: ") || line.StartsWith("  heard");

    public static TheoryData<int> PeopleBounds => [0, WorldBuilder.MaxPeople];

    [Theory]
    [MemberData(nameof(PeopleBounds))]
    public void Run_PeopleAtTheBounds_PrintsThatManyPeople(int people)
    {
        (int exitCode, string output, _) = Run("--people", people.ToString());

        exitCode.ShouldBe(0);
        People(output).Length.ShouldBe(people);
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
