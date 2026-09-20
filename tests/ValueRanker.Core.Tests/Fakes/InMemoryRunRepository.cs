using ValueRanker.Core.Domain;
using ValueRanker.Core.Ports;

namespace ValueRanker.Core.Tests.Fakes;

public sealed class InMemoryRunRepository : IRunRepository
{
    private readonly Dictionary<Guid, RankingRun> _runs = [];

    public Task<RankingRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default)
        => Task.FromResult(_runs.GetValueOrDefault(runId));

    public Task<IReadOnlyList<RankingRun>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RankingRun>>(_runs.Values.ToList());

    public Task SaveAsync(RankingRun run, CancellationToken cancellationToken = default)
    {
        _runs[run.Id] = run;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        _runs.Remove(runId);
        return Task.CompletedTask;
    }
}
