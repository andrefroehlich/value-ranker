namespace ValueRanker.Core.Domain;

public sealed record ValueList(
    string ListId,
    string Language,
    int Version,
    string Title,
    IReadOnlyList<ValueItem> Values);
