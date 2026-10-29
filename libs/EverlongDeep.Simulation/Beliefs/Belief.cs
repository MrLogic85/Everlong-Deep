using System.Diagnostics;
using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>A claim one person holds, as <c>docs/08 Social Model.md</c> → "Belief" defines.</summary>
public sealed class Belief
{
    private readonly List<HeardEntry> _heardFrom = [];
    private readonly List<ToldEntry> _toldTo = [];
    private double _importance;
    private double _confidence;
    private double _relevance;

    internal Belief(PersonId holder, Claim claim, double confidence, double importance)
    {
        Holder = holder;
        Claim = claim;
        Importance = importance;
        Confidence = confidence;
        Relevance = importance;
    }

    public PersonId Holder { get; }
    /// <summary>What the holder believes happened; replaced only when they take a teller's competing version of the same story.</summary>
    public Claim Claim { get; internal set; }

    public bool IsFirstHand => Claim.Witness == Holder;

    /// <summary>How much it matters to the holder, 0 to 1, as <see cref="Beliefs.Importance"/> judges it.</summary>
    public double Importance
    {
        get => _importance;
        internal set
        {
            Debug.Assert(value is >= 0 and <= 1, "Importance is 0 to 1");
            _importance = Math.Clamp(value, 0, 1);
        }
    }

    /// <summary>How sure the holder is, 0 to 1.</summary>
    public double Confidence
    {
        get => _confidence;
        internal set
        {
            Debug.Assert(value is >= 0 and <= 1, "Confidence is 0 to 1");
            _confidence = Math.Clamp(value, 0, 1);
        }
    }

    /// <summary>Who told the holder this story, the latest telling from each, oldest first.</summary>
    public IReadOnlyList<HeardEntry> HeardFrom => _heardFrom;

    /// <summary>Whom the holder told this story, the latest telling to each, oldest first.</summary>
    public IReadOnlyList<ToldEntry> ToldTo => _toldTo;

    /// <summary>Whether <paramref name="teller"/> told the holder this story.</summary>
    public bool WasHeardFrom(PersonId teller) => _heardFrom.Exists(entry => entry.From == teller);

    /// <summary>Whether the holder last told <paramref name="listener"/> this belief as they hold it now, so they have nothing new for them.</summary>
    public bool WasToldUnchanged(PersonId listener) => _toldTo.Find(entry => entry.To == listener)?.Held == Claim;

    /// <summary>How much the holder wants to talk about it, 0 to 1; at 0 it is a memory, still held but no longer told.</summary>
    public double Relevance
    {
        get => _relevance;
        internal set
        {
            Debug.Assert(value is >= 0 and <= 1, "Relevance is 0 to 1");
            _relevance = Math.Clamp(value, 0, 1);
        }
    }

    public bool IsMemory => Relevance <= 0;

    // One entry per person: a later telling replaces theirs, so the lists grow with the people met, not with time.
    internal void RememberHeard(HeardEntry entry)
    {
        _heardFrom.RemoveAll(heard => heard.From == entry.From);
        _heardFrom.Add(entry);
    }

    internal void RememberTold(ToldEntry entry)
    {
        _toldTo.RemoveAll(told => told.To == entry.To);
        _toldTo.Add(entry);
    }
}
