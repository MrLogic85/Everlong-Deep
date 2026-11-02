using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Events;

namespace EverlongDeep.Cli.Tests;

public class ProgramTests
{
    public static TheoryData<string[]> MalformedArguments =>
    [
        ["--seed"],
        ["--seed", "x"],
        ["--people", "-1"],
        ["--people", (WorldBuilder.MaxPeople + 1).ToString()],
        ["--past-events", "-1"],
        ["--past-events", (WorldBuilder.MaxPastEvents + 1).ToString()],
        ["--past-events", "1", "--past-events", "2"],
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
        ["--play", "--play"],
        ["--dump"],
        ["--play", "--dump", "--dump"],
        ["--play", "--speed"],
        ["--play", "--speed", "0"],
        ["--play", "--speed", (Program.MaxSpeed + 1).ToString()],
        ["--play", "--speed", "NaN"],
        ["--play", "--speed", "fast"],
        ["--play", "--speed", "1", "--speed", "2"],
    ];

    // An empty world for no hours, so each run ends at once.
    public static TheoryData<string[]> PlayArguments =>
    [
        ["--play", "--people", "0", "--hours", "0"],
        ["--play", "--speed", Program.MaxSpeed.ToString(), "--dump", "--people", "0", "--hours", "0"],
        ["--play", "--speed", "max", "--trace", "--people", "0", "--hours", "0"],
    ];

    [Theory]
    [MemberData(nameof(MalformedArguments))]
    public void Run_MalformedArguments_PrintsUsageAndFails(string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();

        int exitCode = Program.Run(args, output, error);

        exitCode.ShouldNotBe(0);
        output.ToString().ShouldBeEmpty();
        error.ToString().ShouldStartWith("Usage:");
    }

    // The past's lines are the ones before tick 0, and 2 is below the default of one per person.
    [Fact]
    public void Run_PastEvents_DrawsThatManyIntoThePast()
    {
        var output = new StringWriter();

        int exitCode = Program.Run(["--seed", "42", "--people", "8", "--hours", "0", "--past-events", "2"], output, new StringWriter());

        exitCode.ShouldBe(0);
        output.ToString().Split('\n').Count(line => line.StartsWith('-')).ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(PlayArguments))]
    public void Run_PlayArguments_Succeeds(string[] args)
    {
        var error = new StringWriter();

        int exitCode = Program.Run(args, new StringWriter(), error);

        exitCode.ShouldBe(0);
        error.ToString().ShouldBeEmpty();
    }
}
