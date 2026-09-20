namespace ValueRanker.Core.Domain;

/// <summary>A single comparison between two values. ScoreA is 1 if A won, 0 if B won, 0.5 if tied.</summary>
public sealed record PairwiseComparison(string IdA, string IdB, double ScoreA);
