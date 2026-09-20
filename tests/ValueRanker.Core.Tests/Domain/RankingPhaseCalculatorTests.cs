using ValueRanker.Core.Domain;

namespace ValueRanker.Core.Tests.Domain;

public class RankingPhaseCalculatorTests
{
    private static readonly IReadOnlyList<string> ValueIds = Enumerable.Range(1, 20).Select(i => $"v{i:00}").ToList();

    [Test]
    public async Task Fresh_state_is_at_the_start_of_phase_one_of_three()
    {
        var state = RankingState.CreateInitial(seed: 1, ValueIds);

        var progress = RankingPhaseCalculator.ComputeProgress(state);

        await Assert.That(progress.Phase).IsEqualTo(RankingPhase.Build);
        await Assert.That(progress.PhaseNumber).IsEqualTo(1);
        await Assert.That(progress.TotalPhases).IsEqualTo(3);
        await Assert.That(progress.StepsCompletedInPhase).IsEqualTo(0);
        await Assert.That(progress.EstimatedStepsInPhase).IsGreaterThan(0);
    }

    [Test]
    public async Task Progress_within_build_advances_as_groups_are_answered()
    {
        var strategy = new RankingStrategy();
        var state = RankingState.CreateInitial(seed: 1, ValueIds);
        var evt = new GroupBestWorstEvent(["v01", "v02", "v03", "v04"], "v01", "v04");

        state = strategy.Apply(state, evt);
        var progress = RankingPhaseCalculator.ComputeProgress(state);

        await Assert.That(progress.Phase).IsEqualTo(RankingPhase.Build);
        await Assert.That(progress.StepsCompletedInPhase).IsEqualTo(1);
    }

    [Test]
    public async Task Progress_resets_within_phase_when_focus_starts()
    {
        var strategy = new RankingStrategy();
        var state = RankingState.CreateInitial(seed: 3, ValueIds);
        var iterations = 0;

        while (RankingPhaseCalculator.Determine(state) == RankingPhase.Build)
        {
            if (++iterations > 200)
            {
                throw new InvalidOperationException("Did not leave Build within the safety limit.");
            }

            var step = (NextGroupStep)strategy.GetNextStep(state);
            state = strategy.Apply(state, new GroupBestWorstEvent(step.ValueIds, step.ValueIds[0], step.ValueIds[^1]));
        }

        var progress = RankingPhaseCalculator.ComputeProgress(state);

        await Assert.That(progress.Phase).IsEqualTo(RankingPhase.Focus);
        await Assert.That(progress.PhaseNumber).IsEqualTo(2);
        await Assert.That(progress.StepsCompletedInPhase).IsEqualTo(0);
        await Assert.That(progress.EstimatedStepsInPhase).IsGreaterThan(0);
    }

    [Test]
    public async Task Finale_progress_is_capped_at_the_max_duels_estimate()
    {
        var strategy = new RankingStrategy();
        var state = RankingState.CreateInitial(seed: 7, ValueIds);
        var iterations = 0;

        while (RankingPhaseCalculator.Determine(state) != RankingPhase.Finale)
        {
            if (++iterations > 200)
            {
                throw new InvalidOperationException("Did not reach Finale within the safety limit.");
            }

            var step = strategy.GetNextStep(state);

            RankingEvent evt = step switch
            {
                NextGroupStep { TaskType: GroupTaskType.BestWorst } g => new GroupBestWorstEvent(g.ValueIds, g.ValueIds[0], g.ValueIds[^1]),
                NextGroupStep g => new GroupFullOrderEvent(g.ValueIds),
                _ => throw new InvalidOperationException("Unexpected step before Finale."),
            };

            state = strategy.Apply(state, evt);
        }

        var progress = RankingPhaseCalculator.ComputeProgress(state);

        await Assert.That(progress.Phase).IsEqualTo(RankingPhase.Finale);
        await Assert.That(progress.PhaseNumber).IsEqualTo(3);
        await Assert.That(progress.EstimatedStepsInPhase).IsEqualTo(RankingOptions.MaxDuels);
    }
}
