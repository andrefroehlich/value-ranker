namespace ValueRanker.Core.Domain;

public static class EloCalculator
{
    public static (double RatingA, double RatingB) Apply(double ratingA, double ratingB, double scoreA, double kFactor)
    {
        var expectedA = 1.0 / (1.0 + Math.Pow(10, (ratingB - ratingA) / 400.0));
        var expectedB = 1.0 - expectedA;
        var scoreB = 1.0 - scoreA;

        var newRatingA = ratingA + kFactor * (scoreA - expectedA);
        var newRatingB = ratingB + kFactor * (scoreB - expectedB);

        return (newRatingA, newRatingB);
    }
}
