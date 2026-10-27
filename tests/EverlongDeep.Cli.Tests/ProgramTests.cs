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
