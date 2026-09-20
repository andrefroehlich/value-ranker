namespace ValueRanker.Core.Domain;

public sealed class ValueListValidationException(IReadOnlyList<string> errors)
    : Exception($"Value list validation failed: {string.Join("; ", errors)}")
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
