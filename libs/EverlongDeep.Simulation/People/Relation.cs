using System.Diagnostics;

namespace EverlongDeep.Simulation.People;

/// <summary>How the holder sees one other person, as <c>docs/08 Social Model.md</c> → "Relation" defines.</summary>
public sealed class Relation
{
    private double _opinion;
    private double _trust;

    internal Relation(PersonId other, double opinion, double trust)
    {
        Other = other;
        Opinion = opinion;
        Trust = trust;
    }

    public PersonId Other { get; }

    public Ties Ties { get; internal set; }

    /// <summary>How the holder feels about <see cref="Other"/>, −1 (hate) to 1 (love).</summary>
    public double Opinion
    {
        get => _opinion;
        internal set
        {
            Debug.Assert(value is >= -1 and <= 1, "Opinion is −1 to 1");
            _opinion = Math.Clamp(value, -1, 1);
        }
    }

    /// <summary>How much the holder believes what <see cref="Other"/> says, 0 to 1.</summary>
    public double Trust
    {
        get => _trust;
        internal set
        {
            Debug.Assert(value is >= 0 and <= 1, "Trust is 0 to 1");
            _trust = Math.Clamp(value, 0, 1);
        }
    }

    /// <summary>Hours the feelings have stood past the mark that adds or removes the friend tie.</summary>
    internal int FriendChangeHours { get; set; }

    /// <summary>Hours the feelings have stood past the mark that adds or removes the rival tie.</summary>
    internal int RivalChangeHours { get; set; }
}
