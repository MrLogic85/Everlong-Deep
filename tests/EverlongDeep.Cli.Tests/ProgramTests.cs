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
}
