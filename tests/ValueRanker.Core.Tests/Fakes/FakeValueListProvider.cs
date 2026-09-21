using ValueRanker.Core.Domain;
using ValueRanker.Core.Ports;

namespace ValueRanker.Core.Tests.Fakes;

public sealed class FakeValueListProvider(ValueList list) : IValueListProvider
{
    public Task<ValueList> GetAsync(string listId, CancellationToken cancellationToken = default)
    {
        if (listId != list.ListId)
        {
            throw new InvalidOperationException($"No fake value list registered for '{listId}'.");
        }

        return Task.FromResult(list);
    }

    public static ValueList CreateList(string listId, int count, string? idPrefix = null)
    {
        var values = Enumerable.Range(1, count)
            .Select(i => new ValueItem($"{idPrefix ?? listId}-{i:000}", $"Value {i}", $"Description {i}"))
            .ToList();

        return new ValueList(listId, "de", 1, "Test list", values);
    }
}
