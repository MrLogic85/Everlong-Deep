using System.Diagnostics;
using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>A claim one person holds, as <c>docs/08 Social Model.md</c> → "Belief" defines.</summary>
public sealed class Belief
{
    /// <summary>Relevance a holder adds to a story they are the actor, target or witness of.</summary>
    public const double OwnStoryRelevance = 0.3;

    private readonly List<HeardEntry> _heardFrom = [];
    private readonly List<ToldEntry> _toldTo = [];
    private double _confidence;
    private double _relevance;

    internal Belief(PersonId holder, Claim claim, double confidence, double relevance)
    {
        Holder = holder;
        Claim = claim;
        Confidence = confidence;
        Relevance = relevance;
    }

    /// <summary>The relevance a new belief starts at: the claim's severity, plus more when it is the holder's own story.</summary>
    internal static double StartingRelevance(PersonId holder, Claim claim) =>
        Math.Min(1, claim.Severity + (claim.Involves(holder) ? OwnStoryRelevance : 0));

    public PersonId Holder { get; }
    public Claim Claim { get; }

    public bool IsFirstHand => Claim.Witness == Holder;

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

    /// <summary>Whether the holder last told <paramref name="listener"/> this belief as they hold it now, so they have nothing new for them.</summary>
    public bool WasToldUnchanged(PersonId listener) => _toldTo.Find(entry => entry.To == listener)?.Held == Claim;

    /// <summary>How much the holder wants to talk about it, 0 to 1.</summary>
    public double Relevance
    {
        get => _relevance;
        internal set
        {
            Debug.Assert(value is >= 0 and <= 1, "Relevance is 0 to 1");
            _relevance = Math.Clamp(value, 0, 1);
        }
    }

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
