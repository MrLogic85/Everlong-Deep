using System.Diagnostics;
using EverlongDeep.Simulation;
using EverlongDeep.Simulation.Beliefs;

namespace EverlongDeep.Storyteller;

/// <summary>
/// Listens to a run's log and tells each record as it is written: worded, and with its conversation's trace when the
/// run is traced. The records the world writes while it is built wait for <see cref="Begin"/>, which gives the world
/// that names them.
/// </summary>
public sealed class Narrator
{
    private readonly Queue<LogRecord> _waiting = new();
    private World? _world;
    private Action<Told>? _onTold;

    // The trace of the conversation whose record comes next, handed out just before it.
    private ConversationTrace? _trace;

    public Narrator() => Log = new SimulationLog(Hear);

    /// <summary>The log the world is built and stepped with.</summary>
    public SimulationLog Log { get; }

    /// <summary>
    /// Tells <paramref name="onTold"/> the records written so far, named by <paramref name="world"/>, then each one as
    /// it is written; a <paramref name="traced"/> run's conversations come with their traces.
    /// </summary>
    public void Begin(World world, Action<Told> onTold, bool traced)
    {
        Debug.Assert(_world is null, "A narrator begins once");
        if (_world is not null)
        {
            return;
        }

        _world = world;
        _onTold = onTold;
        if (traced)
        {
            world.TraceConversations(trace =>
            {
                Debug.Assert(_trace is null, "A trace's record comes before the next trace");
                _trace = trace;
            });
        }

        while (_waiting.TryDequeue(out LogRecord? record))
        {
            Tell(record);
        }
    }

    private void Hear(LogRecord record)
    {
        if (_world is null)
        {
            _waiting.Enqueue(record);
            return;
        }

        Tell(record);
    }

    private void Tell(LogRecord record)
    {
        Debug.Assert(_trace is null || ReferenceEquals(_trace.Ended, record), "A trace comes just before its record");
        ConversationTrace? trace = null;
        if (_trace is not null && ReferenceEquals(_trace.Ended, record))
        {
            trace = _trace;
            _trace = null;
        }

        _onTold!(new Told([.. LogText.Lines(_world!, record)], trace));
    }
}

/// <summary>A record as the Storyteller tells it: its log lines, and its conversation's trace when there is one.</summary>
public sealed record Told(IReadOnlyList<string> Lines, ConversationTrace? Trace);
