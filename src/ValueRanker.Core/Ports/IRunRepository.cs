using ValueRanker.Core.Domain;

namespace ValueRanker.Core.Ports;

public interface IRunRepository
{
    Task<RankingRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RankingRun>> ListAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(RankingRun run, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid runId, CancellationToken cancellationToken = default);
}
