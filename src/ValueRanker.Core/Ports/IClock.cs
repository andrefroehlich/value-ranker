namespace ValueRanker.Core.Ports;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
