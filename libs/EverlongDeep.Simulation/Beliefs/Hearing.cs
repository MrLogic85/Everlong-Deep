namespace EverlongDeep.Simulation.Beliefs;

/// <summary>What a listener made of one telling (<c>docs/08 Social Model.md</c> → "Same story or another").</summary>
public enum Hearing
{
    /// <summary>A story new to them, believed.</summary>
    Believed,

    /// <summary>A story new to them, not believed.</summary>
    NotBelieved,

    /// <summary>A retelling, with nothing new for them, which only updates the entries.</summary>
    Retold,

    /// <summary>A story they already heard from this teller, which only updates whom they heard it from.</summary>
    SameTeller,

    /// <summary>The same story from a new teller, which makes them surer.</summary>
    Surer,

    /// <summary>The same story told to someone who saw it, who stays as they were.</summary>
    KeptOwn,

    /// <summary>A competing version they believe over their own, and take.</summary>
    TookTellers,

    /// <summary>A competing version they reject, trusting the teller less as a liar.</summary>
    ThinksLying,
}
