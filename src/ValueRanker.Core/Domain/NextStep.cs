namespace ValueRanker.Core.Domain;

public enum GroupTaskType
{
    BestWorst,
    FullOrder,
}

public abstract record NextStep;

public sealed record NextGroupStep(IReadOnlyList<string> ValueIds, GroupTaskType TaskType) : NextStep;

public sealed record NextDuelStep(string LeftId, string RightId) : NextStep;

public sealed record RankingFinishedStep : NextStep;
