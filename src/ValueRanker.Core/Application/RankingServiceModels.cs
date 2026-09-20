using ValueRanker.Core.Domain;

namespace ValueRanker.Core.Application;

public sealed record CreateRunRequest(string Name, string ListId);

public sealed record RunSummary(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, RankingPhase Phase);

public sealed record GroupBestWorstAnswer(IReadOnlyList<string> ValueIds, string BestId, string WorstId);

/// <summary>OrderedValueIds is best to worst.</summary>
public sealed record GroupFullOrderAnswer(IReadOnlyList<string> OrderedValueIds);

public sealed record DuelAnswer(string LeftId, string RightId, DuelResult Result);

public sealed record ResultEntry(string ValueId, string Name, double Rating, int ComparisonCount, int Rank);

public sealed record RunResult(IReadOnlyList<ResultEntry> Ranking);
