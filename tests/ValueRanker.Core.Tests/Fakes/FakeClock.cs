using ValueRanker.Core.Ports;

namespace ValueRanker.Core.Tests.Fakes;

public sealed class FakeClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = now;
}
