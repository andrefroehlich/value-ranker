namespace ValueRanker.Core.Domain;

public static class RankingOptions
{
    public const double InitialRating = 1000;

    /// <summary>K-factor for a value with few comparisons so far, so early mistakes correct quickly.</summary>
    public const double KFactorProvisional = 48;

    /// <summary>K-factor once a value has accumulated a moderate number of comparisons.</summary>
    public const double KFactorTransitional = 24;

    /// <summary>K-factor once a value is well established, keeping the finale stable enough to converge.</summary>
    public const double KFactorStable = 12;

    public const int ProvisionalComparisonThreshold = 5;

    public const int TransitionalComparisonThreshold = 15;

    public const int GroupSize = 5;

    public const int Round2PoolSize = 120;

    public const int FocusPoolSize = 30;

    public const int FocusPasses = 2;

    public const int FinalePoolSize = 12;

    public const int ResultTopN = 10;

    public const int StabilityWindow = 10;

    public const int MinDuelsPerTop10 = 3;

    public const int MaxDuels = 35;
}
