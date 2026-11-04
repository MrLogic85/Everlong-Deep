using EverlongDeep.Simulation.People;

namespace EverlongDeep.Storyteller;

/// <summary>The words a <see cref="Gender"/> is written with: "she", "her", "her", "herself".</summary>
public sealed record Pronouns(string Subject, string Object, string Possessive, string Reflexive)
{
    private static readonly Pronouns _she = new("she", "her", "her", "herself");
    private static readonly Pronouns _he = new("he", "him", "his", "himself");
    private static readonly Pronouns _they = new("they", "them", "their", "themself");

    public static Pronouns Of(Gender gender) => gender switch
    {
        Gender.She => _she,
        Gender.He => _he,
        Gender.They => _they,
        _ => throw new ArgumentOutOfRangeException(nameof(gender), gender, null),
    };

    /// <summary>Whether a verb after <see cref="Subject"/> is plural: "they trust", but "she trusts".</summary>
    public bool TakesPluralVerb => this == _they;

    /// <summary>As the tables and the JSON line show them: "she/her".</summary>
    public override string ToString() => $"{Subject}/{Object}";
}
