namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// Whose opinion colors a dial as a teller tells a story, or why none does (<c>docs/08 Social Model.md</c> → "Telling"
/// step 4).
/// </summary>
public enum OpinionSource
{
    /// <summary>A person the teller knows, by the teller's opinion of them.</summary>
    Known,

    /// <summary>A thing, which nobody likes or dislikes.</summary>
    Thing,

    /// <summary>The teller themselves.</summary>
    Self,

    /// <summary>A person the teller never met.</summary>
    NeverMet,

    /// <summary>An actor nobody knows, so nobody can be blamed.</summary>
    Unknown,

    /// <summary>An act that does no harm, so nobody can be blamed.</summary>
    NoHarm,
}
