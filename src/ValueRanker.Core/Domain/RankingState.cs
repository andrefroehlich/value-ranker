namespace ValueRanker.Core.Domain;

/// <summary>
/// The full state needed to decide the next step and to keep applying further events.
/// Always produced by replaying a seed and an event log; never mutated in place.
/// </summary>
public sealed record RankingState
{
    public required int Seed { get; init; }

    public required IReadOnlyList<string> AllValueIds { get; init; }

    public required IReadOnlyDictionary<string, double> Ratings { get; init; }

    public required IReadOnlyDictionary<string, int> ComparisonCounts { get; init; }

    public required IReadOnlyDictionary<string, int> DuelCounts { get; init; }

    public required int BuildGroupsCompleted { get; init; }

    public required int FocusGroupsCompleted { get; init; }

    public required int DuelsCompleted { get; init; }

    /// <summary>Value ids already used in the current round-2 pass, so every pool member gets exactly one round-2 group.</summary>
    public required IReadOnlySet<string> Round2UsedIds { get; init; }

    /// <summary>Value ids already used in the current focus pass; reset when a pass completes.</summary>
    public required IReadOnlySet<string> FocusUsedIds { get; init; }

    public required IReadOnlySet<string> DuelledPairs { get; init; }

    /// <summary>Top-3 value ids (best to worst) after each duel, most recent last, capped to RankingOptions.StabilityWindow entries.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> RecentTop3Snapshots { get; init; }

    public static RankingState CreateInitial(int seed, IReadOnlyList<string> valueIds)
    {
        var ratings = valueIds.ToDictionary(id => id, _ => RankingOptions.InitialRating);
        var comparisonCounts = valueIds.ToDictionary(id => id, _ => 0);
        var duelCounts = valueIds.ToDictionary(id => id, _ => 0);

        return new RankingState
        {
            Seed = seed,
            AllValueIds = valueIds,
            Ratings = ratings,
            ComparisonCounts = comparisonCounts,
            DuelCounts = duelCounts,
            BuildGroupsCompleted = 0,
            FocusGroupsCompleted = 0,
            DuelsCompleted = 0,
            Round2UsedIds = new HashSet<string>(),
            FocusUsedIds = new HashSet<string>(),
            DuelledPairs = new HashSet<string>(),
            RecentTop3Snapshots = [],
        };
    }
}
