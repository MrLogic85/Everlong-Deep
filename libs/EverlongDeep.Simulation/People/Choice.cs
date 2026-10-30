namespace EverlongDeep.Simulation.People;

/// <summary>
/// One option at a decision point, a row of the effect table in <c>docs/08 Social Model.md</c> → "Traits".
/// A yes-or-no decision has one row, scored against not doing it; a choice between options has a row for each.
/// </summary>
public enum Choice
{
    /// <summary>Talk now.</summary>
    Talk,
    /// <summary>Approach a stranger.</summary>
    ApproachStranger,
    /// <summary>Pick a topic about others' business.</summary>
    TellOthersBusiness,
    /// <summary>Pick a scandal over good news.</summary>
    TellScandal,
    /// <summary>Pick a story the teller has nothing new to say about to this listener.</summary>
    TellAgain,
    /// <summary>How far to color the dials.</summary>
    ColorDials,
    /// <summary>Color toward harsher blame.</summary>
    ColorBlameHarsher,
    /// <summary>Claim "I saw it myself" of a story only heard.</summary>
    ClaimWitnessed,
    /// <summary>Keep the witness when passing it on.</summary>
    KeepWitness,
    /// <summary>Asked about a belief: tell it as believed.</summary>
    TellTruth,
    /// <summary>Asked about a belief: tell a colored version.</summary>
    TellColored,
    /// <summary>Asked about a belief: keep it to oneself.</summary>
    HoldBack,
    /// <summary>Asked about a belief: tell something else.</summary>
    Lie,
    /// <summary>Believe the teller.</summary>
    Believe,
}
