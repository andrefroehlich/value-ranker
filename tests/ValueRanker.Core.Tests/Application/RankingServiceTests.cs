using ValueRanker.Core.Application;
using ValueRanker.Core.Domain;
using ValueRanker.Core.Tests.Fakes;

namespace ValueRanker.Core.Tests.Application;

public class RankingServiceTests
{
    private static RankingService CreateService(out InMemoryRunRepository repository, out FakeClock clock, int valueCount = 20)
    {
        repository = new InMemoryRunRepository();
        clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var provider = new FakeValueListProvider(FakeValueListProvider.CreateList("test-list", valueCount));
        return new RankingService(repository, provider, clock);
    }

    [Test]
    public async Task CreateRun_persists_a_new_run_with_no_events()
    {
        var service = CreateService(out var repository, out _);

        var runId = await service.CreateRunAsync(new CreateRunRequest("My run", "test-list"));
        var run = await repository.GetAsync(runId);

        await Assert.That(run).IsNotNull();
        await Assert.That(run!.Name).IsEqualTo("My run");
        await Assert.That(run.Events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ListRuns_reports_the_current_phase_derived_from_replay()
    {
        var service = CreateService(out _, out _);
        var runId = await service.CreateRunAsync(new CreateRunRequest("My run", "test-list"));

        var summaries = await service.ListRunsAsync();

        await Assert.That(summaries.Count).IsEqualTo(1);
        await Assert.That(summaries[0].Phase).IsEqualTo(RankingPhase.Build);
    }

    [Test]
    public async Task SubmitGroupBestWorst_appends_an_event_and_advances_the_next_step()
    {
        var service = CreateService(out _, out _);
        var runId = await service.CreateRunAsync(new CreateRunRequest("My run", "test-list"));

        var firstStep = (NextGroupStep)await service.GetNextStepAsync(runId);
        await service.SubmitGroupBestWorstAsync(runId, new GroupBestWorstAnswer(firstStep.ValueIds, firstStep.ValueIds[0], firstStep.ValueIds[^1]));

        var secondStep = (NextGroupStep)await service.GetNextStepAsync(runId);

        await Assert.That(secondStep.ValueIds.SequenceEqual(firstStep.ValueIds)).IsFalse();
    }

    [Test]
    public async Task Undo_removes_the_last_event()
    {
        var service = CreateService(out var repository, out _);
        var runId = await service.CreateRunAsync(new CreateRunRequest("My run", "test-list"));

        var step = (NextGroupStep)await service.GetNextStepAsync(runId);
        await service.SubmitGroupBestWorstAsync(runId, new GroupBestWorstAnswer(step.ValueIds, step.ValueIds[0], step.ValueIds[^1]));

        var runAfterSubmit = await repository.GetAsync(runId);
        await Assert.That(runAfterSubmit!.Events.Count).IsEqualTo(1);

        await service.UndoAsync(runId);

        var runAfterUndo = await repository.GetAsync(runId);
        await Assert.That(runAfterUndo!.Events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Undo_on_a_run_with_no_events_does_nothing()
    {
        var service = CreateService(out var repository, out _);
        var runId = await service.CreateRunAsync(new CreateRunRequest("My run", "test-list"));

        await service.UndoAsync(runId);

        var run = await repository.GetAsync(runId);
        await Assert.That(run!.Events.Count).IsEqualTo(0);
    }

    [Test]
    public async Task GetResult_ranks_values_by_rating_with_the_winner_first()
    {
        var service = CreateService(out _, out _);
        var runId = await service.CreateRunAsync(new CreateRunRequest("My run", "test-list"));

        var step = (NextGroupStep)await service.GetNextStepAsync(runId);
        await service.SubmitGroupBestWorstAsync(runId, new GroupBestWorstAnswer(step.ValueIds, step.ValueIds[0], step.ValueIds[^1]));

        var result = await service.GetResultAsync(runId);

        await Assert.That(result.Ranking[0].ValueId).IsEqualTo(step.ValueIds[0]);
        await Assert.That(result.Ranking[0].Rank).IsEqualTo(1);
        await Assert.That(result.Ranking.Count).IsEqualTo(20);
    }

    [Test]
    public async Task DeleteRun_removes_it_from_the_repository()
    {
        var service = CreateService(out var repository, out _);
        var runId = await service.CreateRunAsync(new CreateRunRequest("My run", "test-list"));

        await service.DeleteRunAsync(runId);

        var run = await repository.GetAsync(runId);
        await Assert.That(run).IsNull();
    }
}
