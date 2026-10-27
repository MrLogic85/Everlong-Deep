using System.Globalization;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.People;

namespace EverlongDeep.Cli;

/// <summary>Runs a seeded tavern and prints its log, for listening to the world from the terminal.</summary>
public static class Program
{
    public const int DefaultPeople = 6;

    public static string Usage =>
        "Usage: EverlongDeep.Cli [--seed N] [--people N]\n" +
        "  --seed N    any integer; random when absent, and the log prints the seed used\n" +
        $"  --people N  0 to {WorldBuilder.MaxPeople}, default {DefaultPeople}";

    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>Prints the run to <paramref name="output"/>, or usage to <paramref name="error"/>; returns the exit code.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParse(args, out int? seed, out int people))
        {
            error.WriteLine(Usage);
            return 1;
        }

        var log = new SimulationLog();
        World world = WorldBuilder.Tavern(seed ?? Random.Shared.Next(), people, log);

        foreach (string line in log.Lines)
        {
            output.WriteLine(line);
        }

        foreach (Person person in world.People)
        {
            string place = world.Places.First(candidate => candidate.Id == person.Place).Name;
            output.WriteLine($"#{person.Id.Value} {person.Name} ({string.Join(", ", person.Traits)}) in {place}");
        }

        return 0;
    }

    private static bool TryParse(string[] args, out int? seed, out int people)
    {
        seed = null;
        int? peopleArg = null;
        people = DefaultPeople;

        for (int i = 0; i < args.Length; i += 2)
        {
            if (i + 1 >= args.Length || !int.TryParse(args[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return false;
            }

            switch (args[i])
            {
                case "--seed" when seed is null:
                    seed = value;
                    break;
                case "--people" when peopleArg is null && value >= 0 && value <= WorldBuilder.MaxPeople:
                    peopleArg = value;
                    break;
                default:
                    return false;
            }
        }

        people = peopleArg ?? DefaultPeople;
        return true;
    }
}
