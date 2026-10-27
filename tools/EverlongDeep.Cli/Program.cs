using System.Globalization;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;

namespace EverlongDeep.Cli;

/// <summary>Runs a seeded tavern and prints its log, for listening to the world from the terminal.</summary>
public static class Program
{
    public const int DefaultPeople = 6;
    public const int DefaultHours = 48;

    // A year of hours keeps a mistyped argument from running for ever.
    public const int MaxHours = 24 * 365;

    public static string Usage =>
        "Usage: EverlongDeep.Cli [--seed N] [--people N] [--hours N]\n" +
        "  --seed N    any integer; random when absent, and the log prints the seed used\n" +
        $"  --people N  0 to {WorldBuilder.MaxPeople}, default {DefaultPeople}\n" +
        $"  --hours N   simulation hours to run, 0 to {MaxHours}, default {DefaultHours}";

    public static int Main(string[] args) => Run(args, Console.Out, Console.Error);

    /// <summary>Prints the run to <paramref name="output"/>, or usage to <paramref name="error"/>; returns the exit code.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (!TryParse(args, out int? seed, out int people, out int hours))
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

        foreach (Thing thing in world.Things)
        {
            output.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Thing {thing.Id.Value} {thing.Name} ({thing.Kind}, importance {thing.Importance:0.00})"));
        }

        foreach (Person person in world.People)
        {
            string place = world.Thing(person.Place).Name;
            output.WriteLine($"#{person.Id.Value} {person.Name} ({string.Join(", ", person.Traits)}) in {place}");
            foreach (Relation relation in person.Relations)
            {
                string ties = relation.Ties == Ties.None ? "" : $", {relation.Ties}";
                output.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  -> {world.Person(relation.Other).Name}: opinion {relation.Opinion:0.00}, trust {relation.Trust:0.00}{ties}"));
            }
        }

        int printed = log.Lines.Count;
        for (int hour = 0; hour < hours; hour++)
        {
            world.StepHour(log);
        }

        foreach (string line in log.Lines.Skip(printed))
        {
            output.WriteLine(line);
        }

        return 0;
    }

    private static bool TryParse(string[] args, out int? seed, out int people, out int hours)
    {
        seed = null;
        int? peopleArg = null;
        int? hoursArg = null;
        people = DefaultPeople;
        hours = DefaultHours;

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
                case "--hours" when hoursArg is null && value >= 0 && value <= MaxHours:
                    hoursArg = value;
                    break;
                default:
                    return false;
            }
        }

        people = peopleArg ?? DefaultPeople;
        hours = hoursArg ?? DefaultHours;
        return true;
    }
}
