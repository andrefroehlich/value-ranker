namespace ValueRanker.Core.Domain;

/// <summary>The persisted shape of a run. State (ratings, phase, next step) is always derived by replaying Seed + Events.</summary>
public sealed record RankingRun(
    Guid Id,
    string Name,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string ListId,
    int ListVersion,
    int Seed,
    IReadOnlyList<RankingEvent> Events,
    int SchemaVersion = 1);
