namespace EverlongDeep.Simulation.People;

/// <summary>
/// A yes-or-no choice as drawn: the choice, the traits and context that scored it, the chance that gave, and the roll
/// that decided it.
/// </summary>
public readonly record struct Roll
{
    internal Roll(Choice choice, TraitProfile traits, double context, double value)
    {
        Choice = choice;
        Traits = traits;
        Context = context;
        Value = value;
        Chance = TraitEffects.Chance(Score);
    }

    public Choice Choice { get; }
    public TraitProfile Traits { get; }
    public double Context { get; }
    public double Chance { get; }
    public double Value { get; }

    public bool Yes => Value < Chance;

    /// <summary>The context plus each trait's pull.</summary>
    public double Score => Context + TraitEffects.Pull(Traits, Choice);

    /// <summary>Each trait's pull, worked out when asked, so a roll nobody traces costs nothing more.</summary>
    public IReadOnlyList<TraitPull> Pulls => [.. TraitEffects.Pulls(Traits, Choice)];
}
