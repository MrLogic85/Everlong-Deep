using System.Globalization;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;
using Spectre.Console;

namespace EverlongDeep.Cli;

/// <summary>Runs a seeded tavern and prints its log, for listening to the world from the terminal.</summary>
public static class Program
{
    public const int DefaultPeople = 6;
    public const int DefaultHours = 48;

    // A year of hours keeps a mistyped argument from running for ever.
    public const int MaxHours = 24 * 365;

    public static string Usage =>
        "Usage: EverlongDeep.Cli [--seed N] [--people N] [--hours N] [--trace]\n" +
        "  --seed N    any integer; random when absent, and the log prints the seed used\n" +
        $"  --people N  0 to {WorldBuilder.MaxPeople}, default {DefaultPeople}\n" +
        $"  --hours N   simulation hours to run, 0 to {MaxHours}, default {DefaultHours}\n" +
        "  --trace     after each conversation, print who decided what and why, and what changed";

    // Color only for a person at a terminal, so a run redirected to a file reads as plain text.
    public static int Main(string[] args) => Run(args, Console.Out, Console.Error, color: !Console.IsOutputRedirected);

    /// <summary>Prints the run to <paramref name="output"/>, or usage to <paramref name="error"/>; returns the exit code.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error, bool color = false)
    {
        if (!TryParse(args, out int? seed, out int people, out int hours, out bool trace))
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
            output.WriteLine($"#{person.Id.Value} {person.Name} ({string.Join(", ", person.Traits.Labels)}) in {place}");
            foreach (Relation relation in person.Relations)
            {
                string ties = relation.Ties == Ties.None ? "" : $", {relation.Ties}";
                output.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  -> {world.Person(relation.Other).Name}: opinion {relation.Opinion:0.00}, trust {relation.Trust:0.00}{ties}"));
            }

            foreach (Belief belief in person.Beliefs)
            {
                output.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"  {Source(world, belief)}: {ClaimText.Describe(world, belief.Claim)}, hour {belief.Claim.Hour} " +
                    $"(importance {belief.Importance:0.00}, confidence {belief.Confidence:0.00}, relevance {belief.Relevance:0.00}, " +
                    $"severity {belief.Claim.Severity:0.00}, blame {belief.Claim.Blame:0.00})"));
            }
        }

        var traces = new List<ConversationTrace>();
        if (trace)
        {
            world.TraceConversations(traces.Add);
        }

        int printed = log.Lines.Count;
        for (int hour = 0; hour < hours; hour++)
        {
            world.StepHour(log);
        }

        Dictionary<int, ConversationTrace> byLine = traces.ToDictionary(conversation => conversation.LogLine);
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = color ? AnsiSupport.Yes : AnsiSupport.No,
            ColorSystem = color ? ColorSystemSupport.Detect : ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(output),

            // Spectre's CI detection turns color on under GitHub Actions; color follows only the caller.
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
        });
        for (int i = printed; i < log.Lines.Count; i++)
        {
            output.WriteLine(log.Lines[i]);
            if (byLine.TryGetValue(i, out ConversationTrace? conversation))
            {
                console.Write(TraceTables.Render(world, conversation));
            }
        }

        return 0;
    }

    private static string Source(World world, Belief belief) => belief.Claim.Witness switch
    {
        null => "heard",
        { } witness when witness == belief.Holder => ClaimText.WitnessVerb(belief.Claim),
        { } witness => $"heard {world.Person(witness).Name} {ClaimText.WitnessVerb(belief.Claim)}",
    };

    private static bool TryParse(string[] args, out int? seed, out int people, out int hours, out bool trace)
    {
        seed = null;
        trace = false;
        int? peopleArg = null;
        int? hoursArg = null;
        people = DefaultPeople;
        hours = DefaultHours;

        // Flags stand alone; every other option takes the value after it.
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--trace" && !trace)
            {
                trace = true;
                continue;
            }

            if (i + 1 >= args.Length || !int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return false;
            }

            switch (args[i - 1])
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
