namespace ValueRanker.Core.Domain;

using static RankingChunking;
using static RankingPhaseCalculator;

public sealed class RankingStrategy : IRankingStrategy
{
    public RankingState Apply(RankingState state, RankingEvent evt)
    {
        var comparisons = evt switch
        {
            GroupBestWorstEvent e => GroupEventToPairwiseComparisons.FromBestWorst(e.ValueIds, e.BestId, e.WorstId),
            GroupFullOrderEvent e => GroupEventToPairwiseComparisons.FromFullOrder(e.OrderedValueIds),
            DuelEvent e => [GroupEventToPairwiseComparisons.FromDuel(e.LeftId, e.RightId, e.Result)],
            _ => throw new ArgumentOutOfRangeException(nameof(evt), evt, "Unknown ranking event type."),
        };

        var ratings = new Dictionary<string, double>(state.Ratings);
        var comparisonCounts = new Dictionary<string, int>(state.ComparisonCounts);
        var duelCounts = new Dictionary<string, int>(state.DuelCounts);

        foreach (var comparison in comparisons)
        {
            var kFactor = (KFactorFor(comparisonCounts[comparison.IdA]) + KFactorFor(comparisonCounts[comparison.IdB])) / 2.0;
            var (newA, newB) = EloCalculator.Apply(ratings[comparison.IdA], ratings[comparison.IdB], comparison.ScoreA, kFactor);
            ratings[comparison.IdA] = newA;
            ratings[comparison.IdB] = newB;
            comparisonCounts[comparison.IdA]++;
            comparisonCounts[comparison.IdB]++;

            if (evt is DuelEvent)
            {
                duelCounts[comparison.IdA]++;
                duelCounts[comparison.IdB]++;
            }
        }

        var buildGroupsCompleted = state.BuildGroupsCompleted;
        var focusGroupsCompleted = state.FocusGroupsCompleted;
        var duelsCompleted = state.DuelsCompleted;
        var round2UsedIds = state.Round2UsedIds;
        var focusUsedIds = state.FocusUsedIds;
        var duelledPairs = state.DuelledPairs;
        var recentTop3 = state.RecentTop3Snapshots;

        switch (evt)
        {
            case GroupBestWorstEvent groupBestWorst:
                var round1Total = Round1Chunks(state).Count;
                if (buildGroupsCompleted >= round1Total)
                {
                    var updatedRound2Used = new HashSet<string>(round2UsedIds);
                    updatedRound2Used.UnionWith(groupBestWorst.ValueIds);
                    round2UsedIds = updatedRound2Used;
                }

                buildGroupsCompleted++;
                break;

            case GroupFullOrderEvent groupFullOrder:
                focusGroupsCompleted++;
                var groupsPerPass = FocusGroupsPerPass(state);

                if (focusGroupsCompleted % groupsPerPass == 0)
                {
                    focusUsedIds = new HashSet<string>();
                }
                else
                {
                    var updatedFocusUsed = new HashSet<string>(focusUsedIds);
                    updatedFocusUsed.UnionWith(groupFullOrder.OrderedValueIds);
                    focusUsedIds = updatedFocusUsed;
                }

                break;

            case DuelEvent duel:
                duelsCompleted++;
                duelledPairs = new HashSet<string>(duelledPairs) { CanonicalPairKey(duel.LeftId, duel.RightId) };

                var top3 = ratings
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key)
                    .Take(3)
                    .Select(kv => kv.Key)
                    .ToList();

                var snapshots = recentTop3.ToList();
                snapshots.Add(top3);
                if (snapshots.Count > RankingOptions.StabilityWindow)
                {
                    snapshots.RemoveAt(0);
                }

                recentTop3 = snapshots;
                break;
        }

        return state with
        {
            Ratings = ratings,
            ComparisonCounts = comparisonCounts,
            DuelCounts = duelCounts,
            BuildGroupsCompleted = buildGroupsCompleted,
            FocusGroupsCompleted = focusGroupsCompleted,
            DuelsCompleted = duelsCompleted,
            Round2UsedIds = round2UsedIds,
            FocusUsedIds = focusUsedIds,
            DuelledPairs = duelledPairs,
            RecentTop3Snapshots = recentTop3,
        };
    }

    public NextStep GetNextStep(RankingState state)
    {
        var phase = Determine(state);

        return phase switch
        {
            RankingPhase.Build => NextBuildStep(state),
            RankingPhase.Focus => NextFocusStep(state),
            RankingPhase.Finale => NextDuelStep(state),
            _ => new RankingFinishedStep(),
        };
    }

    public bool IsFinished(RankingState state) => Determine(state) == RankingPhase.Finished;

    private static NextStep NextBuildStep(RankingState state)
    {
        var round1Chunks = Round1Chunks(state);

        if (state.BuildGroupsCompleted < round1Chunks.Count)
        {
            return new NextGroupStep(round1Chunks[state.BuildGroupsCompleted], GroupTaskType.BestWorst);
        }

        // Round 1 "middles" (comparisonCount below the round-1 best/worst count) only ever lost to
        // the local best and beat the local worst, so a globally strong value can easily be buried
        // there just from sharing a group with an even stronger one ("group of death"). Round 2
        // therefore alternates between rescuing middles and refining round-1 winners, rather than
        // ordering purely by current rating (which would starve middles, who all sit near the
        // initial rating) or purely by "most uncertain first" (which would starve winners entirely,
        // since middles outnumber the fixed round-2 slots).
        var remaining = state.AllValueIds.Where(id => !state.Round2UsedIds.Contains(id));
        var middleTrack = remaining.Where(id => state.ComparisonCounts[id] < RankingOptions.GroupSize - 1).ToList();
        var winnerTrack = remaining.Where(id => state.ComparisonCounts[id] >= RankingOptions.GroupSize - 1).ToList();

        var round2Index = state.BuildGroupsCompleted - round1Chunks.Count;
        var preferMiddleTrack = round2Index % 2 == 0;

        var track = preferMiddleTrack
            ? (middleTrack.Count >= 2 ? middleTrack : winnerTrack)
            : (winnerTrack.Count >= 2 ? winnerTrack : middleTrack);

        var pool = track
            .OrderByDescending(id => state.Ratings[id])
            .ThenBy(id => id)
            .ToList();

        var group = ChunkWithMinSize(pool, RankingOptions.GroupSize, 2)[0];

        return new NextGroupStep(group, GroupTaskType.BestWorst);
    }

    private static NextStep NextFocusStep(RankingState state)
    {
        var passIndex = state.FocusGroupsCompleted / FocusGroupsPerPass(state);

        // No live top-N cutoff: see the comment in NextBuildStep. FocusUsedIds plus the fixed
        // per-pass group count keep this to exactly the intended top-30-ish pool each pass.
        var basePool = state.AllValueIds
            .Where(id => !state.FocusUsedIds.Contains(id))
            .OrderByDescending(id => state.Ratings[id])
            .ThenBy(id => id);

        // Alternate the ordering per pass so later passes re-pair values differently.
        var pool = (passIndex % 2 == 0 ? basePool : basePool.Reverse()).ToList();
        var group = ChunkWithMinSize(pool, RankingOptions.GroupSize, 2)[0];

        return new NextGroupStep(group, GroupTaskType.FullOrder);
    }

    private static NextStep NextDuelStep(RankingState state)
    {
        var poolSize = Math.Min(RankingOptions.FinalePoolSize, state.AllValueIds.Count);

        var pool = state.AllValueIds
            .OrderByDescending(id => state.Ratings[id])
            .ThenBy(id => id)
            .Take(poolSize)
            .ToList();

        (string Left, string Right)? best = null;
        var bestPriority = int.MaxValue;
        var bestGap = double.MaxValue;

        for (var i = 0; i < pool.Count; i++)
        {
            for (var j = i + 1; j < pool.Count; j++)
            {
                var idA = pool[i];
                var idB = pool[j];

                if (state.DuelledPairs.Contains(CanonicalPairKey(idA, idB)))
                {
                    continue;
                }

                var rankGap = j - i;
                var bothTopFive = i < 5 && j < 5;

                // Fully resolve the top 5 via round robin; outside that, only compare near-adjacent ranks.
                if (!bothTopFive && rankGap > 2)
                {
                    continue;
                }

                var priority = bothTopFive ? 0 : 1;
                var ratingGap = Math.Abs(state.Ratings[idA] - state.Ratings[idB]);

                if (priority < bestPriority || (priority == bestPriority && ratingGap < bestGap))
                {
                    bestPriority = priority;
                    bestGap = ratingGap;
                    best = (idA, idB);
                }
            }
        }

        if (best is null)
        {
            // All near-adjacent pairs are exhausted; fall back to any undueled pair with the smallest gap.
            for (var i = 0; i < pool.Count; i++)
            {
                for (var j = i + 1; j < pool.Count; j++)
                {
                    var idA = pool[i];
                    var idB = pool[j];

                    if (state.DuelledPairs.Contains(CanonicalPairKey(idA, idB)))
                    {
                        continue;
                    }

                    var ratingGap = Math.Abs(state.Ratings[idA] - state.Ratings[idB]);
                    if (ratingGap < bestGap)
                    {
                        bestGap = ratingGap;
                        best = (idA, idB);
                    }
                }
            }
        }

        return best is { } pair ? new NextDuelStep(pair.Left, pair.Right) : new RankingFinishedStep();
    }

    private static double KFactorFor(int comparisonCount) => comparisonCount switch
    {
        < RankingOptions.ProvisionalComparisonThreshold => RankingOptions.KFactorProvisional,
        < RankingOptions.TransitionalComparisonThreshold => RankingOptions.KFactorTransitional,
        _ => RankingOptions.KFactorStable,
    };

    private static string CanonicalPairKey(string idA, string idB)
        => string.CompareOrdinal(idA, idB) <= 0 ? $"{idA}|{idB}" : $"{idB}|{idA}";
}
