using System.Globalization;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;
using EverlongDeep.Simulation.Events;
using EverlongDeep.Simulation.People;
using EverlongDeep.Simulation.Things;
using EverlongDeep.Storyteller;
using Spectre.Console;

namespace EverlongDeep.Cli;

/// <summary>Runs a seeded tavern and prints its log, for listening to the world from the terminal.</summary>
public static class Program
{
    private const int _defaultPeople = 6;
    private const int _defaultHours = 48;

    // A year of hours keeps a mistyped argument from running for ever.
    public const int MaxHours = 24 * 365;

    public static string Usage =>
        $"Usage: EverlongDeep.Cli [--seed N] [--people N] [--hours N] [--scenario {Scenarios.StrangerKillsName}] [--trace]\n" +
        "  --seed N    any integer; random when absent, and the log prints the seed used\n" +
        $"  --people N  0 to {WorldBuilder.MaxPeople}, default {_defaultPeople}\n" +
        $"  --hours N   simulation hours to run, 0 to {MaxHours}, default {_defaultHours}\n" +
        $"  --scenario {Scenarios.StrangerKillsName}\n" +
        $"              at hour {Scenarios.StrangerKillsTick / Clock.TicksPerHour}, a stranger enters and kills p5, with p1 and p2 as key witnesses " +
        $"and p3 and p4 as bystanders; runs only past that hour, and needs " +
        $"{Scenarios.StrangerKillsPeople} people or more\n" +
        "  --trace     print a legend, then tables of who decided what and why in each conversation";

    public static int Main(string[] args) => Run(args, Console.Out, Console.Error, terminal: !Console.IsOutputRedirected);

    /// <summary>
    /// Prints the run to <paramref name="output"/>, or usage to <paramref name="error"/>; returns the exit code. A person
    /// at a <paramref name="terminal"/> gets color and the terminal's width; a run redirected to a file reads as plain
    /// text, as wide as the trace's tables need.
    /// </summary>
    public static int Run(string[] args, TextWriter output, TextWriter error, bool terminal = false)
    {
        if (!TryParse(args, out int? seed, out int people, out int hours, out bool strangerKills, out bool trace))
        {
            error.WriteLine(Usage);
            return 1;
        }

        var log = new SimulationLog();
        World world = WorldBuilder.Tavern(seed ?? Random.Shared.Next(), people, log);

        foreach (string line in log.Records.SelectMany(record => LogText.Lines(world, record)))
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
            output.WriteLine($"#{person.Id.Value} {person.Name} ({string.Join(", ", TraitWords.Shown(person.Traits))}) in {place}");
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
                    $"  {Source(world, belief)}: {ClaimText.Describe(world, belief.Claim)}, at {Clock.At(belief.Claim.Tick)} " +
                    $"(importance {belief.Importance:0.00}, confidence {belief.Confidence:0.00}, relevance {belief.Relevance:0.00}, " +
                    $"severity {belief.Claim.Severity:0.00}, blame {belief.Claim.Blame:0.00})"));
            }
        }

        var traces = new List<ConversationTrace>();
        if (trace)
        {
            world.TraceConversations(traces.Add);
        }

        int printed = log.Records.Count;
        if (strangerKills)
        {
            world.Schedule(Scenarios.StrangerKills(world), log);
        }

        world.Step((long)hours * Clock.TicksPerHour, log);

        Dictionary<ConversationEnded, ConversationTrace> byEnd = traces.ToDictionary<ConversationTrace, ConversationEnded>(
            conversation => conversation.Ended, ReferenceEqualityComparer.Instance);
        IAnsiConsole console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = terminal ? AnsiSupport.Yes : AnsiSupport.No,
            ColorSystem = terminal ? ColorSystemSupport.Detect : ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No,
            Out = new AnsiConsoleOutput(output),

            // Spectre's CI detection turns color on under GitHub Actions; color follows only the caller.
            Enrichment = new ProfileEnrichment { UseDefaultEnrichers = false },
        });
        if (!terminal)
        {
            console.Profile.Width = TraceTables.Width;
        }

        if (trace)
        {
            console.Write(TraceTables.Legend());
        }

        foreach (LogRecord record in log.Records.Skip(printed))
        {
            if (record is not ConversationEnded ended || !byEnd.TryGetValue(ended, out ConversationTrace? conversation))
            {
                foreach (string line in LogText.Lines(world, record))
                {
                    output.WriteLine(line);
                }

                continue;
            }

            // In a trace, prose that explains the conversation replaces its log line, unwrapped, so no id is parted from
            // its name; a terminal wraps it.
            console.Write(TraceTables.Heading(world, conversation));
            output.WriteLine(TraceProse.Of(world, conversation));
            console.Write(TraceTables.Render(world, conversation));
        }

        return 0;
    }

    private static string Source(World world, Belief belief) => belief.Claim.Witness switch
    {
        null => "heard",
        { } witness when witness == belief.Holder => ClaimText.WitnessVerb(belief.Claim),
        { } witness => $"heard {world.Person(witness).Name} {ClaimText.WitnessVerb(belief.Claim)}",
    };

    private static bool TryParse(string[] args, out int? seed, out int people, out int hours, out bool strangerKills, out bool trace)
    {
        seed = null;
        strangerKills = false;
        trace = false;
        int? peopleArg = null;
        int? hoursArg = null;
        people = _defaultPeople;
        hours = _defaultHours;

        // Flags stand alone; every other option takes the value after it.
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--trace" && !trace)
            {
                trace = true;
                continue;
            }

            if (args[i] == "--scenario" && !strangerKills && i + 1 < args.Length && args[i + 1] == Scenarios.StrangerKillsName)
            {
                strangerKills = true;
                i++;
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

        people = peopleArg ?? _defaultPeople;
        hours = hoursArg ?? _defaultHours;
        return !strangerKills || people >= Scenarios.StrangerKillsPeople;
    }
}
