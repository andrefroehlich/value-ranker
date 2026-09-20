using ValueRanker.Core.Domain;
using ValueRanker.Core.Ports;

namespace ValueRanker.Core.Application;

/// <summary>The single entry point for any UI or API. Callers never touch Domain types like Elo or the strategy directly.</summary>
public sealed class RankingService(IRunRepository runRepository, IValueListProvider valueListProvider, IClock clock)
{
    private readonly IRankingStrategy _strategy = new RankingStrategy();

    public async Task<Guid> CreateRunAsync(CreateRunRequest request, CancellationToken cancellationToken = default)
    {
        var valueList = await valueListProvider.GetAsync(request.ListId, cancellationToken);
        var now = clock.UtcNow;

        var run = new RankingRun(
            Guid.NewGuid(),
            request.Name,
            now,
            now,
            valueList.ListId,
            valueList.Version,
            Random.Shared.Next(),
            []);

        await runRepository.SaveAsync(run, cancellationToken);
        return run.Id;
    }

    public async Task<IReadOnlyList<RunSummary>> ListRunsAsync(CancellationToken cancellationToken = default)
    {
        var runs = await runRepository.ListAsync(cancellationToken);
        var summaries = new List<RunSummary>();

        foreach (var run in runs)
        {
            var state = await ReplayAsync(run, cancellationToken);
            summaries.Add(new RunSummary(run.Id, run.Name, run.CreatedAt, run.UpdatedAt, RankingPhaseCalculator.Determine(state)));
        }

        return summaries;
    }

    public Task DeleteRunAsync(Guid runId, CancellationToken cancellationToken = default)
        => runRepository.DeleteAsync(runId, cancellationToken);

    public async Task<NextStep> GetNextStepAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await LoadRunAsync(runId, cancellationToken);
        var state = await ReplayAsync(run, cancellationToken);
        return _strategy.GetNextStep(state);
    }

    public Task SubmitGroupBestWorstAsync(Guid runId, GroupBestWorstAnswer answer, CancellationToken cancellationToken = default)
        => AppendEventAsync(runId, new GroupBestWorstEvent(answer.ValueIds, answer.BestId, answer.WorstId), cancellationToken);

    public Task SubmitGroupFullOrderAsync(Guid runId, GroupFullOrderAnswer answer, CancellationToken cancellationToken = default)
        => AppendEventAsync(runId, new GroupFullOrderEvent(answer.OrderedValueIds), cancellationToken);

    public Task SubmitDuelAsync(Guid runId, DuelAnswer answer, CancellationToken cancellationToken = default)
        => AppendEventAsync(runId, new DuelEvent(answer.LeftId, answer.RightId, answer.Result), cancellationToken);

    public async Task UndoAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await LoadRunAsync(runId, cancellationToken);

        if (run.Events.Count == 0)
        {
            return;
        }

        var updated = run with
        {
            Events = run.Events.Take(run.Events.Count - 1).ToList(),
            UpdatedAt = clock.UtcNow,
        };

        await runRepository.SaveAsync(updated, cancellationToken);
    }

    public async Task<RunResult> GetResultAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await LoadRunAsync(runId, cancellationToken);
        var valueList = await valueListProvider.GetAsync(run.ListId, cancellationToken);
        var state = await ReplayAsync(run, cancellationToken);
        var namesById = valueList.Values.ToDictionary(v => v.Id, v => v.Name);

        var ranking = state.Ratings
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Select((kv, index) => new ResultEntry(kv.Key, namesById[kv.Key], kv.Value, state.ComparisonCounts[kv.Key], index + 1))
            .ToList();

        return new RunResult(ranking);
    }

    private async Task<RankingRun> LoadRunAsync(Guid runId, CancellationToken cancellationToken)
        => await runRepository.GetAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Run '{runId}' was not found.");

    private async Task AppendEventAsync(Guid runId, RankingEvent evt, CancellationToken cancellationToken)
    {
        var run = await LoadRunAsync(runId, cancellationToken);

        var updated = run with
        {
            Events = [.. run.Events, evt],
            UpdatedAt = clock.UtcNow,
        };

        await runRepository.SaveAsync(updated, cancellationToken);
    }

    private async Task<RankingState> ReplayAsync(RankingRun run, CancellationToken cancellationToken)
    {
        var valueList = await valueListProvider.GetAsync(run.ListId, cancellationToken);
        var valueIds = valueList.Values.Select(v => v.Id).ToList();
        var initial = RankingState.CreateInitial(run.Seed, valueIds);
        return run.Events.Aggregate(initial, _strategy.Apply);
    }
}
