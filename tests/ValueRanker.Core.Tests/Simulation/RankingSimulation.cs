using ValueRanker.Core.Domain;

namespace ValueRanker.Core.Tests.Simulation;

public sealed record SimulationResult(int Taps, bool Top3SetHit, bool Top3OrderHit, int Top10Overlap);

public static class RankingSimulation
{
    public static SimulationResult Run(int valueCount, int seed, double noise)
    {
        var valueIds = Enumerable.Range(1, valueCount).Select(i => $"v{i:0000}").ToList();
        var trueOrder = Shuffle(valueIds, unchecked(seed * 7919 + 13));

        var strategy = new RankingStrategy();
        var state = RankingState.CreateInitial(seed, valueIds);
        var user = new VirtualUser(trueOrder, noise, new Random(unchecked(seed * 104729 + 7)));

        var taps = 0;
        var safetyLimit = valueCount * 20;
        var iterations = 0;

        while (!strategy.IsFinished(state))
        {
            if (++iterations > safetyLimit)
            {
                throw new InvalidOperationException("Simulation did not converge within the safety limit.");
            }

            var step = strategy.GetNextStep(state);

            RankingEvent evt;
            switch (step)
            {
                case NextGroupStep { TaskType: GroupTaskType.BestWorst } g:
                    evt = user.AnswerBestWorst(g);
                    taps += 2;
                    break;

                case NextGroupStep g:
                    evt = user.AnswerFullOrder(g);
                    taps += g.ValueIds.Count - 1;
                    break;

                case NextDuelStep d:
                    evt = user.AnswerDuel(d);
                    taps += 1;
                    break;

                default:
                    throw new InvalidOperationException("Unexpected step: finished but GetNextStep returned a real step.");
            }

            state = strategy.Apply(state, evt);
        }

        var predicted = state.Ratings
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select(kv => kv.Key)
            .ToList();

        var predictedTop3 = predicted.Take(3).ToList();
        var predictedTop10 = predicted.Take(10).ToList();
        var trueTop3 = trueOrder.Take(3).ToList();
        var trueTop10 = trueOrder.Take(10).ToHashSet();

        var top3SetHit = predictedTop3.ToHashSet().SetEquals(trueTop3);
        var top3OrderHit = predictedTop3.SequenceEqual(trueTop3);
        var top10Overlap = predictedTop10.Count(trueTop10.Contains);

        return new SimulationResult(taps, top3SetHit, top3OrderHit, top10Overlap);
    }

    private static List<string> Shuffle(IReadOnlyList<string> items, int seed)
    {
        var shuffled = items.ToList();
        var random = new Random(seed);

        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }
}
