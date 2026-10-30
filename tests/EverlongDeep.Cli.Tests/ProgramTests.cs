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
