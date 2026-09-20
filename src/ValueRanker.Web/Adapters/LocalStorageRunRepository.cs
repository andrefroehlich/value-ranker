using System.Text.Json;
using Microsoft.JSInterop;
using ValueRanker.Core.Domain;
using ValueRanker.Core.Ports;

namespace ValueRanker.Web.Adapters;

/// <summary>
/// Stores runs in the browser's localStorage, one entry per run plus an index of run ids
/// (localStorage has no native "list keys with this prefix" operation from .NET).
/// </summary>
public sealed class LocalStorageRunRepository : IRunRepository, IAsyncDisposable
{
    private const string IndexKey = "valueranker.runs";
    private const string RunKeyPrefix = "valueranker.run.";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public LocalStorageRunRepository(IJSRuntime jsRuntime)
    {
        _moduleTask = new(() => jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/localStorageInterop.js").AsTask());
    }

    public async Task<RankingRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var module = await _moduleTask.Value;
        return await ReadRunAsync(module, runId, cancellationToken);
    }

    public async Task<IReadOnlyList<RankingRun>> ListAsync(CancellationToken cancellationToken = default)
    {
        var module = await _moduleTask.Value;
        var ids = await ReadIndexAsync(module, cancellationToken);

        var runs = new List<RankingRun>();
        var staleIds = new List<Guid>();

        foreach (var id in ids)
        {
            var run = await ReadRunAsync(module, id, cancellationToken);
            if (run is not null)
            {
                runs.Add(run);
            }
            else
            {
                // The entry was missing or corrupt (already removed by ReadRunAsync): drop it from
                // the index too, so it does not keep showing up as a stale reference.
                staleIds.Add(id);
            }
        }

        if (staleIds.Count > 0)
        {
            foreach (var id in staleIds)
            {
                ids.Remove(id);
            }

            await WriteIndexAsync(module, ids, cancellationToken);
        }

        return runs;
    }

    public async Task SaveAsync(RankingRun run, CancellationToken cancellationToken = default)
    {
        var module = await _moduleTask.Value;
        var json = JsonSerializer.Serialize(run, JsonOptions);

        try
        {
            await module.InvokeVoidAsync("setItem", cancellationToken, RunKeyPrefix + run.Id, json);
        }
        catch (JSException ex)
        {
            throw new RunStorageException("Could not save the run. The browser's storage might be full or blocked.", ex);
        }

        var ids = await ReadIndexAsync(module, cancellationToken);
        if (!ids.Contains(run.Id))
        {
            ids.Add(run.Id);
            await WriteIndexAsync(module, ids, cancellationToken);
        }
    }

    public async Task DeleteAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var module = await _moduleTask.Value;
        await module.InvokeVoidAsync("removeItem", cancellationToken, RunKeyPrefix + runId);

        var ids = await ReadIndexAsync(module, cancellationToken);
        if (ids.Remove(runId))
        {
            await WriteIndexAsync(module, ids, cancellationToken);
        }
    }

    private static async Task<RankingRun?> ReadRunAsync(IJSObjectReference module, Guid runId, CancellationToken cancellationToken)
    {
        var json = await module.InvokeAsync<string?>("getItem", cancellationToken, RunKeyPrefix + runId);

        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RankingRun>(json, JsonOptions);
        }
        catch (JsonException)
        {
            // Corrupt entry: drop it so it does not keep failing on every future load.
            await module.InvokeVoidAsync("removeItem", cancellationToken, RunKeyPrefix + runId);
            return null;
        }
    }

    private static async Task<List<Guid>> ReadIndexAsync(IJSObjectReference module, CancellationToken cancellationToken)
    {
        var json = await module.InvokeAsync<string?>("getItem", cancellationToken, IndexKey);

        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static async Task WriteIndexAsync(IJSObjectReference module, List<Guid> ids, CancellationToken cancellationToken)
    {
        try
        {
            await module.InvokeVoidAsync("setItem", cancellationToken, IndexKey, JsonSerializer.Serialize(ids, JsonOptions));
        }
        catch (JSException ex)
        {
            throw new RunStorageException("Could not update the run index. The browser's storage might be full or blocked.", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}
