namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// The confidence a telling gives its listener: the teller's own times the listener's trust in them
/// (<c>docs/08 Social Model.md</c> → "Telling" step 6).
/// </summary>
public readonly record struct ListenerConfidence(double Teller, double Trust)
{
    public double Value => Teller * Trust;
}
