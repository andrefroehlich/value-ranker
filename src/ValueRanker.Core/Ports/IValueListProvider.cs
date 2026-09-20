using ValueRanker.Core.Domain;

namespace ValueRanker.Core.Ports;

public interface IValueListProvider
{
    Task<ValueList> GetAsync(string listId, CancellationToken cancellationToken = default);
}
