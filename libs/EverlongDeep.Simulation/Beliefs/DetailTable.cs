namespace EverlongDeep.Simulation.Beliefs;

/// <summary>
/// The details an event may start its story with, per act (<c>docs/08 Social Model.md</c> → "Claim"). Starting values
/// are tuned by listening.
/// </summary>
internal static class DetailTable
{
    /// <summary>The most details an event starts its story with.</summary>
    public const int MaxDrawn = 2;

    /// <summary>The chance of each of the <see cref="MaxDrawn"/> details, so an event has 0 to 2.</summary>
    public const double DrawnChance = 0.5;

    /// <summary>The details <paramref name="act"/> draws from.</summary>
    public static IReadOnlyList<Detail> Of(Act act) => act switch
    {
        Act.Sank => [Detail.InAStorm, Detail.AtNight, Detail.WithAllHands, Detail.OffTheReef],
        Act.Killed => [Detail.WithAKnife, Detail.InTheBack, Detail.WithoutAWord, Detail.OverCards],
        Act.Robbed => [Detail.AtNight, Detail.InPlainSight, Detail.DuringABrawl],
        Act.Attacked => [Detail.WithABottle, Detail.FromBehind, Detail.OverCards],
        Act.Betrayed => [Detail.ForCoin, Detail.OverADebt, Detail.AfterYears],
        Act.Helped => [Detail.WithCoin, Detail.InAFight, Detail.WithoutAsking],
        Act.WasSeenAt => [Detail.DrinkingAlone, Detail.AtNight, Detail.WithAStranger],
        _ => throw new ArgumentOutOfRangeException(nameof(act), act, null),
    };

    /// <summary>0 to <see cref="MaxDrawn"/> different details for <paramref name="act"/>, drawn from <paramref name="random"/>.</summary>
    public static ValueList<Detail> Draw(Act act, Random random)
    {
        int count = Enumerable.Range(0, MaxDrawn).Count(_ => random.NextDouble() < DrawnChance);
        if (count == 0)
        {
            return [];
        }

        Detail[] details = [.. Of(act)];
        random.Shuffle(details);
        return [.. details[..count]];
    }
}
