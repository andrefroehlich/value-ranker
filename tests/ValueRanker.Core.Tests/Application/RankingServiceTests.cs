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

        var firstStep = (NextGroupStepView)await service.GetNextStepAsync(runId);
        var firstIds = firstStep.Values.Select(v => v.Id).ToList();
        await service.SubmitGroupBestWorstAsync(runId, new GroupBestWorstAnswer(firstIds, firstIds[0], firstIds[^1]));

        var secondStep = (NextGroupStepView)await service.GetNextStepAsync(runId);
        var secondIds = secondStep.Values.Select(v => v.Id).ToList();

        await Assert.That(secondIds.SequenceEqual(firstIds)).IsFalse();
    }

    [Test]
    public async Task Undo_removes_the_last_event()
    {
        var service = CreateService(out var repository, out _);
        var runId = await service.CreateRunAsync(new CreateRunRequest("My run", "test-list"));

        var step = (NextGroupStepView)await service.GetNextStepAsync(runId);
        var ids = step.Values.Select(v => v.Id).ToList();
        await service.SubmitGroupBestWorstAsync(runId, new GroupBestWorstAnswer(ids, ids[0], ids[^1]));

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

        var step = (NextGroupStepView)await service.GetNextStepAsync(runId);
        var ids = step.Values.Select(v => v.Id).ToList();
        await service.SubmitGroupBestWorstAsync(runId, new GroupBestWorstAnswer(ids, ids[0], ids[^1]));

        var result = await service.GetResultAsync(runId);

        await Assert.That(result.Ranking[0].ValueId).IsEqualTo(ids[0]);
        await Assert.That(result.Ranking[0].Rank).IsEqualTo(1);
        await Assert.That(result.Ranking.Count).IsEqualTo(20);
    }

    [Test]
    public async Task GetDebugSnapshot_reports_current_ratings_and_comparison_counts_for_all_values()
    {
        var service = CreateService(out _, out _);
        var runId = await service.CreateRunAsync(new CreateRunRequest("My run", "test-list"));

        var step = (NextGroupStepView)await service.GetNextStepAsync(runId);
        var ids = step.Values.Select(v => v.Id).ToList();
        await service.SubmitGroupBestWorstAsync(runId, new GroupBestWorstAnswer(ids, ids[0], ids[^1]));

        var snapshot = await service.GetDebugSnapshotAsync(runId);

        await Assert.That(snapshot.Values.Count).IsEqualTo(20);
        await Assert.That(snapshot.Values[0].ValueId).IsEqualTo(ids[0]);
        await Assert.That(snapshot.Values[0].ComparisonCount).IsEqualTo(3);
        await Assert.That(snapshot.Values[0].Rating).IsGreaterThan(RankingOptions.InitialRating);

        var lastPlaceEntry = snapshot.Values.Single(v => v.ValueId == ids[^1]);
        await Assert.That(lastPlaceEntry.Rating).IsLessThan(RankingOptions.InitialRating);

        var untouchedEntry = snapshot.Values.First(v => !ids.Contains(v.ValueId));
        await Assert.That(untouchedEntry.ComparisonCount).IsEqualTo(0);
        await Assert.That(untouchedEntry.Rating).IsEqualTo(RankingOptions.InitialRating);
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

    [Test]
    public async Task ListRuns_reports_a_run_as_incompatible_instead_of_throwing_when_its_value_list_changed()
    {
        var repository = new InMemoryRunRepository();
        var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var originalListProvider = new FakeValueListProvider(FakeValueListProvider.CreateList("test-list", 20));
        var serviceAtCreation = new RankingService(repository, originalListProvider, clock);
        var staleRunId = await serviceAtCreation.CreateRunAsync(new CreateRunRequest("Stale run", "test-list"));

        var firstStep = (NextGroupStepView)await serviceAtCreation.GetNextStepAsync(staleRunId);
        var firstIds = firstStep.Values.Select(v => v.Id).ToList();
        await serviceAtCreation.SubmitGroupBestWorstAsync(staleRunId, new GroupBestWorstAnswer(firstIds, firstIds[0], firstIds[^1]));

        // Simulate the value list's content changing under the same list id (e.g. a content edit
        // that renames/merges values), so the stale run's events reference ids that no longer exist.
        var changedListProvider = new FakeValueListProvider(FakeValueListProvider.CreateList("test-list", 20, idPrefix: "changed"));
        var serviceAfterChange = new RankingService(repository, changedListProvider, clock);
        var freshRunId = await serviceAfterChange.CreateRunAsync(new CreateRunRequest("Fresh run", "test-list"));

        var summaries = await serviceAfterChange.ListRunsAsync();

        await Assert.That(summaries.Count).IsEqualTo(2);

        var staleSummary = summaries.Single(s => s.Id == staleRunId);
        await Assert.That(staleSummary.IsCompatible).IsFalse();

        var freshSummary = summaries.Single(s => s.Id == freshRunId);
        await Assert.That(freshSummary.IsCompatible).IsTrue();
    }

    [Test]
    public async Task RenameRun_updates_the_name()
    {
        var service = CreateService(out var repository, out _);
        var runId = await service.CreateRunAsync(new CreateRunRequest("Old name", "test-list"));

        await service.RenameRunAsync(runId, "New name");

        var run = await repository.GetAsync(runId);
        await Assert.That(run!.Name).IsEqualTo("New name");
    }
}
