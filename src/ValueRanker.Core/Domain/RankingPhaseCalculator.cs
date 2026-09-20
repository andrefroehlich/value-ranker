namespace ValueRanker.Core.Domain;

using static RankingChunking;

public static class RankingPhaseCalculator
{
    public static RankingPhase Determine(RankingState state)
    {
        var round1Total = Round1Chunks(state).Count;
        var round2Total = Round2ChunkCount(state);

        if (state.BuildGroupsCompleted < round1Total + round2Total)
        {
            return RankingPhase.Build;
        }

        var focusTotal = RankingOptions.FocusPasses * FocusGroupsPerPass(state);

        if (state.FocusGroupsCompleted < focusTotal)
        {
            return RankingPhase.Focus;
        }

        return IsFinaleDone(state) ? RankingPhase.Finished : RankingPhase.Finale;
    }

    public static RunProgress ComputeProgress(RankingState state)
    {
        const int totalPhases = 3;
        var phase = Determine(state);

        return phase switch
        {
            RankingPhase.Build => new RunProgress(phase, 1, totalPhases, state.BuildGroupsCompleted, Round1Chunks(state).Count + Round2ChunkCount(state)),
            RankingPhase.Focus => new RunProgress(phase, 2, totalPhases, state.FocusGroupsCompleted, RankingOptions.FocusPasses * FocusGroupsPerPass(state)),
            RankingPhase.Finale => new RunProgress(phase, 3, totalPhases, state.DuelsCompleted, RankingOptions.MaxDuels),
            _ => new RunProgress(phase, totalPhases, totalPhases, state.DuelsCompleted, state.DuelsCompleted),
        };
    }

    internal static IReadOnlyList<IReadOnlyList<string>> Round1Chunks(RankingState state)
        => ChunkWithMinSize(Shuffle(state.Seed, state.AllValueIds), RankingOptions.GroupSize, 2);

    internal static int Round2ChunkCount(RankingState state)
    {
        var poolSize = Math.Min(RankingOptions.Round2PoolSize, state.AllValueIds.Count);
        return ChunkWithMinSize(Enumerable.Range(0, poolSize).ToList(), RankingOptions.GroupSize, 2).Count;
    }

    internal static int FocusGroupsPerPass(RankingState state)
    {
        var poolSize = Math.Min(RankingOptions.FocusPoolSize, state.AllValueIds.Count);
        return ChunkWithMinSize(Enumerable.Range(0, poolSize).ToList(), RankingOptions.GroupSize, 2).Count;
    }

    internal static bool IsFinaleDone(RankingState state)
    {
        if (state.RecentTop3Snapshots.Count < RankingOptions.StabilityWindow)
        {
            return state.DuelsCompleted >= RankingOptions.MaxDuels;
        }

        var first = state.RecentTop3Snapshots[0];
        var stable = state.RecentTop3Snapshots.All(snapshot => snapshot.SequenceEqual(first));

        if (!stable)
        {
            return state.DuelsCompleted >= RankingOptions.MaxDuels;
        }

        var top10 = state.AllValueIds
            .OrderByDescending(id => state.Ratings[id])
            .ThenBy(id => id)
            .Take(Math.Min(RankingOptions.ResultTopN, state.AllValueIds.Count));

        var everyTop10HasEnoughDuels = top10.All(id => state.DuelCounts[id] >= RankingOptions.MinDuelsPerTop10);

        return everyTop10HasEnoughDuels || state.DuelsCompleted >= RankingOptions.MaxDuels;
    }
}
