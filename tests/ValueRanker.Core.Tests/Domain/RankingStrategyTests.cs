using ValueRanker.Core.Domain;

namespace ValueRanker.Core.Tests.Domain;

public class RankingStrategyTests
{
    private static readonly IReadOnlyList<string> ValueIds = Enumerable.Range(1, 20).Select(i => $"v{i:00}").ToList();

    [Test]
    public async Task Fresh_state_starts_in_build_phase()
    {
        var state = RankingState.CreateInitial(seed: 1, ValueIds);

        await Assert.That(RankingPhaseCalculator.Determine(state)).IsEqualTo(RankingPhase.Build);
    }

    [Test]
    public async Task GetNextStep_returns_a_build_worst_group_at_the_start()
    {
        var strategy = new RankingStrategy();
        var state = RankingState.CreateInitial(seed: 1, ValueIds);

        var step = strategy.GetNextStep(state);
        var group = step as NextGroupStep;

        await Assert.That(group).IsNotNull();
        await Assert.That(group!.TaskType).IsEqualTo(GroupTaskType.BestWorst);
        await Assert.That(group.ValueIds.Count).IsGreaterThanOrEqualTo(2);
    }

    [Test]
    public async Task Applying_a_group_event_updates_ratings_and_comparison_counts()
    {
        var strategy = new RankingStrategy();
        var state = RankingState.CreateInitial(seed: 1, ValueIds);
        var evt = new GroupBestWorstEvent(["v01", "v02", "v03", "v04"], BestId: "v01", WorstId: "v04");

        var next = strategy.Apply(state, evt);

        await Assert.That(next.Ratings["v01"]).IsGreaterThan(RankingOptions.InitialRating);
        await Assert.That(next.Ratings["v04"]).IsLessThan(RankingOptions.InitialRating);
        await Assert.That(next.ComparisonCounts["v01"]).IsEqualTo(3);
        await Assert.That(next.ComparisonCounts["v02"]).IsEqualTo(2);
        await Assert.That(next.BuildGroupsCompleted).IsEqualTo(1);
    }

    [Test]
    public async Task Replay_is_deterministic_for_the_same_seed_and_events()
    {
        var strategy = new RankingStrategy();
        var events = new RankingEvent[]
        {
            new GroupBestWorstEvent(["v01", "v02", "v03", "v04"], "v01", "v04"),
            new GroupBestWorstEvent(["v05", "v06", "v07", "v08"], "v07", "v05"),
        };

        var stateA = events.Aggregate(RankingState.CreateInitial(42, ValueIds), strategy.Apply);
        var stateB = events.Aggregate(RankingState.CreateInitial(42, ValueIds), strategy.Apply);

        var ratingsMatch = ValueIds.All(id => stateA.Ratings[id] == stateB.Ratings[id]);
        await Assert.That(ratingsMatch).IsTrue();

        var nextA = (NextGroupStep)strategy.GetNextStep(stateA);
        var nextB = (NextGroupStep)strategy.GetNextStep(stateB);

        await Assert.That(nextA.ValueIds.SequenceEqual(nextB.ValueIds)).IsTrue();
        await Assert.That(nextA.TaskType).IsEqualTo(nextB.TaskType);
    }

    [Test]
    public async Task Full_run_reaches_finished_within_a_safety_limit()
    {
        var strategy = new RankingStrategy();
        var state = RankingState.CreateInitial(seed: 7, ValueIds);
        var iterations = 0;

        while (!strategy.IsFinished(state))
        {
            if (++iterations > 500)
            {
                throw new InvalidOperationException("Did not finish within the safety limit.");
            }

            var step = strategy.GetNextStep(state);

            RankingEvent evt = step switch
            {
                NextGroupStep { TaskType: GroupTaskType.BestWorst } g => new GroupBestWorstEvent(g.ValueIds, g.ValueIds[0], g.ValueIds[^1]),
                NextGroupStep g => new GroupFullOrderEvent(g.ValueIds),
                NextDuelStep d => new DuelEvent(d.LeftId, d.RightId, DuelResult.Left),
                _ => throw new InvalidOperationException("Unexpected finished step."),
            };

            state = strategy.Apply(state, evt);
        }

        await Assert.That(strategy.IsFinished(state)).IsTrue();
    }
}
