using ValueRanker.Core.Domain;

namespace ValueRanker.Core.Tests.Domain;

public class EloCalculatorTests
{
    [Test]
    public async Task Equal_ratings_winner_gains_half_k_factor()
    {
        var (newWinner, newLoser) = EloCalculator.Apply(1000, 1000, scoreA: 1.0, kFactor: 32);

        await Assert.That(newWinner).IsEqualTo(1016.0);
        await Assert.That(newLoser).IsEqualTo(984.0);
    }

    [Test]
    public async Task Equal_ratings_draw_leaves_ratings_unchanged()
    {
        var (newA, newB) = EloCalculator.Apply(1000, 1000, scoreA: 0.5, kFactor: 32);

        await Assert.That(newA).IsEqualTo(1000.0);
        await Assert.That(newB).IsEqualTo(1000.0);
    }

    [Test]
    public async Task Higher_rated_winner_gains_less_than_lower_rated_winner()
    {
        var (favoriteWins, _) = EloCalculator.Apply(1200, 1000, scoreA: 1.0, kFactor: 32);
        var (underdogWins, _) = EloCalculator.Apply(1000, 1200, scoreA: 1.0, kFactor: 32);

        await Assert.That(favoriteWins - 1200).IsLessThan(underdogWins - 1000);
    }

    [Test]
    public async Task Total_rating_points_are_conserved()
    {
        var (newA, newB) = EloCalculator.Apply(1050, 950, scoreA: 0.5, kFactor: 32);

        await Assert.That(newA + newB).IsEqualTo(2000.0);
    }
}
