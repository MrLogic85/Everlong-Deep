using EverlongDeep.Simulation.People;

namespace EverlongDeep.Simulation.Beliefs;

/// <summary>What a listener made of a telling, with the numbers its outcome turned on.</summary>
public abstract record HearingOutcome
{
    /// <summary>The outcome as a plain key, for a reader that needs which outcome it was and not its numbers.</summary>
    public abstract Hearing Hearing { get; }

    /// <summary>
    /// Took <paramref name="Confidence"/>, a memory at once when the story doesn't matter to them, with the
    /// <paramref name="Pulls"/> on believing it.
    /// </summary>
    public sealed record Believed(double Confidence, bool IsMemory, ValueList<TraitValue> Pulls) : HearingOutcome
    {
        public override Hearing Hearing => Hearing.Believed;
    }

    /// <summary>Didn't believe it, with the <paramref name="Pulls"/> on believing it.</summary>
    public sealed record NotBelieved(ValueList<TraitValue> Pulls) : HearingOutcome
    {
        public override Hearing Hearing => Hearing.NotBelieved;
    }

    /// <summary>
    /// A retelling: the teller told them this version before or, when <paramref name="ToSource"/>, heard it from them.
    /// For now it changes nothing but the entries.
    /// </summary>
    public sealed record Retold(bool ToSource) : HearingOutcome
    {
        public override Hearing Hearing => Hearing.Retold;
    }

    public sealed record SameTeller : HearingOutcome
    {
        public override Hearing Hearing => Hearing.SameTeller;
    }

    public sealed record KeptOwn : HearingOutcome
    {
        public override Hearing Hearing => Hearing.KeptOwn;
    }

    /// <summary>Grew surer, from confidence <paramref name="Before"/> to <paramref name="After"/>.</summary>
    public sealed record Surer(double Before, double After) : HearingOutcome
    {
        public override Hearing Hearing => Hearing.Surer;
    }

    /// <summary>Kept <paramref name="Confidence"/> and trusts the teller less, from <paramref name="TrustBefore"/> to <paramref name="TrustAfter"/>.</summary>
    public sealed record ThinksLying(double Confidence, double TrustBefore, double TrustAfter) : HearingOutcome
    {
        public override Hearing Hearing => Hearing.ThinksLying;
    }

    /// <summary>Took the teller's version with <paramref name="Confidence"/>.</summary>
    public sealed record TookTellers(double Confidence) : HearingOutcome
    {
        public override Hearing Hearing => Hearing.TookTellers;
    }
}
