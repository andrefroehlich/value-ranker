using ValueRanker.Core.Domain;

namespace ValueRanker.Core.Application;

public sealed record CreateRunRequest(string Name, string ListId);

/// <summary>
/// IsCompatible is false when the run's stored events reference value ids that no longer exist
/// in its value list (e.g. the list's content changed after the run was created). Phase is
/// meaningless in that case and should not be read.
/// </summary>
public sealed record RunSummary(Guid Id, string Name, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, RankingPhase Phase, bool IsCompatible = true);

public sealed record GroupBestWorstAnswer(IReadOnlyList<string> ValueIds, string BestId, string WorstId);

/// <summary>OrderedValueIds is best to worst.</summary>
public sealed record GroupFullOrderAnswer(IReadOnlyList<string> OrderedValueIds);

public sealed record DuelAnswer(string LeftId, string RightId, DuelResult Result);

public sealed record ResultEntry(string ValueId, string Name, double Rating, int ComparisonCount, int Rank);

public sealed record RunResult(IReadOnlyList<ResultEntry> Ranking);

/// <summary>A value's current scoring state mid-run, for transparency into how comparisons are affecting ratings.</summary>
public sealed record ValueDebugInfo(string ValueId, string Name, double Rating, int ComparisonCount, int DuelCount);

public sealed record RunDebugSnapshot(IReadOnlyList<ValueDebugInfo> Values);

/// <summary>A value with the display text the UI needs, resolved from the run's value list.</summary>
public sealed record ValueOption(string Id, string Name, string Description);

public abstract record NextStepView;

public sealed record NextGroupStepView(IReadOnlyList<ValueOption> Values, GroupTaskType TaskType, RunProgress Progress) : NextStepView;

public sealed record NextDuelStepView(ValueOption Left, ValueOption Right, RunProgress Progress) : NextStepView;

public sealed record RunFinishedStepView : NextStepView;
