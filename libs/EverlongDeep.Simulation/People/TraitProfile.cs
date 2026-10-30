using System.Diagnostics;

namespace EverlongDeep.Simulation.People;

/// <summary>A person's value on every trait axis, −1 to 1, as <c>docs/08 Social Model.md</c> → "Traits" defines.</summary>
public sealed class TraitProfile
{
    /// <summary>A trait shows only past this mark either way.</summary>
    public const double ShownMark = 0.5;

    public static readonly int AxisCount = Enum.GetValues<TraitAxis>().Length;

    private readonly double[] _values;

    /// <summary>A profile with <paramref name="values"/>; an axis left out is 0.</summary>
    public TraitProfile(IReadOnlyDictionary<TraitAxis, double> values)
    {
        _values = new double[AxisCount];
        foreach ((TraitAxis axis, double value) in values)
        {
            Debug.Assert(value is >= -1 and <= 1, "A trait is −1 to 1");
            _values[(int)axis] = Math.Clamp(value, -1, 1);
        }
    }

    public double this[TraitAxis axis] => _values[(int)axis];

    /// <summary>The traits that show, in axis order.</summary>
    public IEnumerable<TraitValue> Shown =>
        Enum.GetValues<TraitAxis>()
            .Where(axis => Math.Abs(this[axis]) > ShownMark)
            .Select(axis => new TraitValue(axis, this[axis]));
}
