namespace ValueRanker.Core.Tests.Simulation;

// Counterpart to RankingSimulationTests, but for the compact value list size
// (see Milestone 9 in PLAN.md). GroupSize=4 was chosen specifically to serve
// this list size well.
public class RankingSimulationCompactTests
{
    private const int ValueCount = 55;

    private static readonly int[] Seeds = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
    private static readonly double[] NoiseLevels = [0.0, 0.5, 1.0, 2.0];

    [Test]
    public async Task Prints_simulation_table_for_55_values()
    {
        Console.WriteLine("noise | avg taps | top3 set hit | top3 order hit | avg top10 overlap");
        Console.WriteLine("----- | -------- | ------------ | --------------- | ------------------");

        foreach (var noise in NoiseLevels)
        {
            var results = Seeds.Select(seed => RankingSimulation.Run(ValueCount, seed, noise)).ToList();

            var avgTaps = results.Average(r => r.Taps);
            var top3SetRate = results.Count(r => r.Top3SetHit) / (double)results.Count;
            var top3OrderRate = results.Count(r => r.Top3OrderHit) / (double)results.Count;
            var avgTop10Overlap = results.Average(r => r.Top10Overlap);

            Console.WriteLine($"{noise,5:0.0} | {avgTaps,8:0} | {top3SetRate,12:P0} | {top3OrderRate,15:P0} | {avgTop10Overlap,18:0.0}");

            await Assert.That(avgTaps).IsLessThanOrEqualTo(150);
        }
    }

    [Test]
    [Arguments(0.0, 0.7)]
    [Arguments(0.5, 0.6)]
    public async Task Low_noise_reliably_finds_the_true_top_3_set_within_the_tap_budget(double noise, double minimumTop3SetRate)
    {
        var results = Seeds.Select(seed => RankingSimulation.Run(ValueCount, seed, noise)).ToList();

        var avgTaps = results.Average(r => r.Taps);
        var top3SetRate = results.Count(r => r.Top3SetHit) / (double)results.Count;

        await Assert.That(avgTaps).IsLessThanOrEqualTo(150);
        await Assert.That(top3SetRate).IsGreaterThanOrEqualTo(minimumTop3SetRate);
    }
}
