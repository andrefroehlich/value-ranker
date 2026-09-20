using ValueRanker.Core.Domain;

namespace ValueRanker.Core.Tests.Domain;

public class GroupEventToPairwiseComparisonsTests
{
    [Test]
    public async Task BestWorst_of_four_produces_five_comparisons()
    {
        var comparisons = GroupEventToPairwiseComparisons.FromBestWorst(["a", "b", "c", "d"], bestId: "a", worstId: "d");

        await Assert.That(comparisons.Count).IsEqualTo(5);
        await Assert.That(comparisons).Contains(c => c.IdA == "a" && c.IdB == "b" && c.ScoreA == 1.0);
        await Assert.That(comparisons).Contains(c => c.IdA == "a" && c.IdB == "c" && c.ScoreA == 1.0);
        await Assert.That(comparisons).Contains(c => c.IdA == "a" && c.IdB == "d" && c.ScoreA == 1.0);
        await Assert.That(comparisons).Contains(c => c.IdA == "b" && c.IdB == "d" && c.ScoreA == 1.0);
        await Assert.That(comparisons).Contains(c => c.IdA == "c" && c.IdB == "d" && c.ScoreA == 1.0);
    }

    [Test]
    public async Task BestWorst_never_compares_the_two_middle_values_against_each_other()
    {
        var comparisons = GroupEventToPairwiseComparisons.FromBestWorst(["a", "b", "c", "d"], bestId: "a", worstId: "d");

        var involvesBothMiddleValues = comparisons.Any(c =>
            (c.IdA == "b" && c.IdB == "c") || (c.IdA == "c" && c.IdB == "b"));

        await Assert.That(involvesBothMiddleValues).IsFalse();
    }

    [Test]
    public async Task FullOrder_of_four_produces_all_six_comparisons()
    {
        var comparisons = GroupEventToPairwiseComparisons.FromFullOrder(["a", "b", "c", "d"]);

        await Assert.That(comparisons.Count).IsEqualTo(6);
        await Assert.That(comparisons.All(c => c.ScoreA == 1.0)).IsTrue();
    }

    [Test]
    [Arguments(DuelResult.Left, 1.0)]
    [Arguments(DuelResult.Right, 0.0)]
    [Arguments(DuelResult.Equal, 0.5)]
    public async Task Duel_maps_result_to_score(DuelResult result, double expectedScoreLeft)
    {
        var comparison = GroupEventToPairwiseComparisons.FromDuel("left", "right", result);

        await Assert.That(comparison.ScoreA).IsEqualTo(expectedScoreLeft);
    }
}
