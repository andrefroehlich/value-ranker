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

/// <summary>A value with the display text the UI needs, resolved from the run's value list.</summary>
public sealed record ValueOption(string Id, string Name, string Description);

public abstract record NextStepView;

public sealed record NextGroupStepView(IReadOnlyList<ValueOption> Values, GroupTaskType TaskType) : NextStepView;

public sealed record NextDuelStepView(ValueOption Left, ValueOption Right) : NextStepView;

public sealed record RunFinishedStepView : NextStepView;
