using ValueRanker.Core.Ports;

namespace ValueRanker.Web.Adapters;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
