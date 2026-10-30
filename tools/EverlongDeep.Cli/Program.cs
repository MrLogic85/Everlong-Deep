using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

    // In ticks per second: a day a second, past which --speed max serves.
    public const int MaxSpeed = 86_400;

    // How often a play run wakes to step the world and print what happened.
    private static readonly TimeSpan _frame = TimeSpan.FromMilliseconds(50);

    public static string Usage =>
        $"Usage: EverlongDeep.Cli [--seed N] [--people N] [--hours N] [--scenario {Scenarios.StrangerKillsName}] [--trace]\n" +
        "                         [--play [--speed N|max] [--dump]]\n" +
        "  --seed N    any integer; random when absent, and the log prints the seed used\n" +
        $"  --people N  0 to {WorldBuilder.MaxPeople}, default {_defaultPeople}\n" +
        $"  --hours N   simulation hours to run, 0 to {MaxHours}, default {_defaultHours}\n" +
        $"  --scenario {Scenarios.StrangerKillsName}\n" +
        $"              at hour {Scenarios.StrangerKillsTick / Clock.TicksPerHour}, a stranger enters and kills p5, with p1 and p2 as key witnesses " +
        $"and p3 and p4 as bystanders; runs only past that hour, and needs " +
        $"{Scenarios.StrangerKillsPeople} people or more\n" +
        "  --trace     print a legend, then tables of who decided what and why in each conversation\n" +
        "  --play      run in real time, printing each log line as it is written, until --hours have passed or Ctrl+C;\n" +
        "              leaves out the start-up state\n" +
        $"  --speed N   with --play, ticks per second, above 0 to {MaxSpeed}, default 1; max runs as fast as it can\n" +
        "  --dump      with --play, print the start-up state too";

    public static int Main(string[] args) =>
        Run(args, Console.Out, Console.Error, terminal: !Console.IsOutputRedirected, catchStop: CatchCtrlC);

    // The first Ctrl+C ends the run cleanly after the step it is in; a second kills it as usual.
    private static CancellationToken CatchCtrlC()
    {
        // Not disposed: it holds no timer, and Ctrl+C can still cancel it after Run returns.
        var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = !stop.IsCancellationRequested;
            stop.Cancel();
        };

        return stop.Token;
    }

    /// <summary>
    /// Prints the run to <paramref name="output"/>, or usage to <paramref name="error"/>; returns the exit code. A person
    /// at a <paramref name="terminal"/> gets color and the terminal's width; a run redirected to a file reads as plain
    /// text, as wide as the trace's tables need. A play run takes a token from <paramref name="catchStop"/> and ends
    /// early, still succeeding, once it is cancelled; any other run goes to its end.
    /// </summary>
    public static int Run(
        string[] args, TextWriter output, TextWriter error, bool terminal = false, Func<CancellationToken>? catchStop = null)
    {
        if (!TryParse(args, out Options? options))
        {
            error.WriteLine(Usage);
            return 1;
        }

        CancellationToken stop = options.Play && catchStop is not null ? catchStop() : CancellationToken.None;

        var log = new SimulationLog();
        World world = WorldBuilder.Tavern(options.Seed ?? Random.Shared.Next(), options.People, log);

        foreach (string line in log.Records.SelectMany(record => LogText.Lines(world, record)))
        {
            output.WriteLine(line);
        }

        if (!options.Play || options.Dump)
        {
            WriteState(world, output);
        }

        // A traced conversation's trace, kept until its log record prints.
        var traces = new Dictionary<ConversationEnded, ConversationTrace>(ReferenceEqualityComparer.Instance);
        if (options.Trace)
        {
            world.TraceConversations(conversation => traces.Add(conversation.Ended, conversation));
        }

        int printed = log.Records.Count;
        if (options.StrangerKills)
        {
            world.Schedule(Scenarios.StrangerKills(world), log);
        }

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

        if (options.Trace)
        {
            console.Write(TraceTables.Legend());
        }

        void Write(LogRecord record)
        {
            if (record is not ConversationEnded ended || !traces.Remove(ended, out ConversationTrace? conversation))
            {
                foreach (string line in LogText.Lines(world, record))
                {
                    output.WriteLine(line);
                }

                return;
            }

            // In a trace, prose that explains the conversation replaces its log line, unwrapped, so no id is parted from
            // its name; a terminal wraps it.
            console.Write(TraceTables.Heading(world, conversation));
            output.WriteLine(TraceProse.Of(world, conversation));
            console.Write(TraceTables.Render(world, conversation));
        }

        Advance(world, log, printed, (long)options.Hours * Clock.TicksPerHour, options.TicksPerSecond, Write, stop);
        return 0;
    }

    private static void WriteState(World world, TextWriter output)
    {
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
    }

    // Steps the world as the wall clock moves, or as fast as it can when unpaced, and writes each log record from index
    // printed on as it appears.
    private static void Advance(
        World world, SimulationLog log, int printed, long ticks, double? ticksPerSecond, Action<LogRecord> write, CancellationToken stop)
    {
        long start = world.Tick;
        long end = start + ticks;
        var clock = Stopwatch.StartNew();
        while (world.Tick < end && !stop.IsCancellationRequested)
        {
            long due = end;
            if (ticksPerSecond is { } speed)
            {
                due = Math.Min(start + (long)(clock.Elapsed.TotalSeconds * speed), end);
            }

            // A period a step at most, so a long catch-up, such as after the computer slept, prints as it goes and sees
            // a stop between steps.
            world.Step(Math.Min(due, world.Tick + World.PeriodTicks) - world.Tick, log);
            for (; printed < log.Records.Count; printed++)
            {
                write(log.Records[printed]);
            }

            if (world.Tick == due && world.Tick < end)
            {
                stop.WaitHandle.WaitOne(_frame);
            }
        }
    }

    private static string Source(World world, Belief belief) => belief.Claim.Witness switch
    {
        null => "heard",
        { } witness when witness == belief.Holder => ClaimText.WitnessVerb(belief.Claim),
        { } witness => $"heard {world.Person(witness).Name} {ClaimText.WitnessVerb(belief.Claim)}",
    };

    private sealed record Options(
        int? Seed, int People, int Hours, bool StrangerKills, bool Trace, bool Play, bool Dump, double? TicksPerSecond);

    private static bool TryParse(string[] args, [NotNullWhen(true)] out Options? options)
    {
        options = null;
        int? seed = null;
        bool strangerKills = false;
        bool trace = false;
        bool play = false;
        bool dump = false;
        int? peopleArg = null;
        int? hoursArg = null;
        string? speedArg = null;

        // Flags stand alone; every other option takes the value after it.
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--trace" && !trace)
            {
                trace = true;
                continue;
            }

            if (args[i] == "--play" && !play)
            {
                play = true;
                continue;
            }

            if (args[i] == "--dump" && !dump)
            {
                dump = true;
                continue;
            }

            if (args[i] == "--scenario" && !strangerKills && i + 1 < args.Length && args[i + 1] == Scenarios.StrangerKillsName)
            {
                strangerKills = true;
                i++;
                continue;
            }

            if (args[i] == "--speed" && speedArg is null && i + 1 < args.Length)
            {
                speedArg = args[++i];
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

        // A null speed leaves the run unpaced: a run without --play, or --speed max.
        double? ticksPerSecond = play ? 1 : null;
        if (speedArg == "max")
        {
            ticksPerSecond = null;
        }
        else if (speedArg is not null)
        {
            // Written as a range test, so NaN, which fails every comparison, is out of it.
            if (!double.TryParse(speedArg, NumberStyles.Float, CultureInfo.InvariantCulture, out double speed) || !(speed > 0 && speed <= MaxSpeed))
            {
                return false;
            }

            ticksPerSecond = speed;
        }

        // --speed and --dump shape only a play run.
        int people = peopleArg ?? _defaultPeople;
        if ((strangerKills && people < Scenarios.StrangerKillsPeople) || (!play && (dump || speedArg is not null)))
        {
            return false;
        }

        options = new Options(seed, people, hoursArg ?? _defaultHours, strangerKills, trace, play, dump, ticksPerSecond);
        return true;
    }
}
