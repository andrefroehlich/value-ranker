using ValueRanker.Core.Domain;
using ValueRanker.Core.Ports;

namespace ValueRanker.Web.Adapters;

public sealed class HttpValueListProvider(HttpClient httpClient) : IValueListProvider
{
    private static readonly Dictionary<string, string> ListFilesByListId = new()
    {
        ["de-default"] = "data/values.de.json",
        ["en-default"] = "data/values.en.json",
        ["de-compact"] = "data/values.de.compact.json",
        ["en-compact"] = "data/values.en.compact.json",
    };

    private readonly Dictionary<string, ValueList> _cache = [];

    public async Task<ValueList> GetAsync(string listId, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(listId, out var cached))
        {
            return cached;
        }

        if (!ListFilesByListId.TryGetValue(listId, out var path))
        {
            throw new InvalidOperationException($"Unknown value list '{listId}'.");
        }

        string json;
        try
        {
            json = await httpClient.GetStringAsync(path, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"Could not load value list '{listId}' from '{path}'.", ex);
        }

        var list = ValueListJsonLoader.Parse(json);
        _cache[listId] = list;
        return list;
    }
}
