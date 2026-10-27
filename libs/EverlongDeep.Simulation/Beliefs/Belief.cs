using System.Diagnostics;
using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>A claim one person holds, as <c>docs/08 Social Model.md</c> → "Belief" defines.</summary>
public sealed class Belief
{
    private double _confidence;
    private double _relevance;

    internal Belief(PersonId holder, Claim claim, double confidence, double relevance)
    {
        Holder = holder;
        Claim = claim;
        Confidence = confidence;
        Relevance = relevance;
    }

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
}
