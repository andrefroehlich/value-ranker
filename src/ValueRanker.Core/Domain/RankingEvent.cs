using System.Text.Json.Serialization;

namespace ValueRanker.Core.Domain;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(GroupBestWorstEvent), "groupBestWorst")]
[JsonDerivedType(typeof(GroupFullOrderEvent), "groupFullOrder")]
[JsonDerivedType(typeof(DuelEvent), "duel")]
public abstract record RankingEvent;

public sealed record GroupBestWorstEvent(IReadOnlyList<string> ValueIds, string BestId, string WorstId) : RankingEvent;

/// <summary>Values ordered from best to worst.</summary>
public sealed record GroupFullOrderEvent(IReadOnlyList<string> OrderedValueIds) : RankingEvent;

public enum DuelResult
{
    Left,
    Right,
    Equal,
}

public sealed record DuelEvent(string LeftId, string RightId, DuelResult Result) : RankingEvent;
