namespace ValueRanker.Core.Tests.Simulation;

public class RankingSimulationTests
{
    private static readonly int[] Seeds = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12];
    private static readonly double[] NoiseLevels = [0.0, 0.5, 1.0, 2.0];

    [Test]
    public async Task Prints_simulation_table_for_250_values()
    {
        Console.WriteLine("noise | avg taps | top3 set hit | top3 order hit | avg top10 overlap");
        Console.WriteLine("----- | -------- | ------------ | --------------- | ------------------");

        foreach (var noise in NoiseLevels)
        {
            var results = Seeds.Select(seed => RankingSimulation.Run(250, seed, noise)).ToList();

            var avgTaps = results.Average(r => r.Taps);
            var top3SetRate = results.Count(r => r.Top3SetHit) / (double)results.Count;
            var top3OrderRate = results.Count(r => r.Top3OrderHit) / (double)results.Count;
            var avgTop10Overlap = results.Average(r => r.Top10Overlap);

            Console.WriteLine($"{noise,5:0.0} | {avgTaps,8:0} | {top3SetRate,12:P0} | {top3OrderRate,15:P0} | {avgTop10Overlap,18:0.0}");

            await Assert.That(avgTaps).IsLessThanOrEqualTo(280);
        }
    }

    // Thresholds reflect GroupSize=4 (see RankingOptions), chosen deliberately to
    // also serve the shorter compact list well, even though it costs some accuracy
    // here on the 250-value list compared to GroupSize=5.
    [Test]
    [Arguments(0.0, 0.5)]
    [Arguments(0.5, 0.5)]
    public async Task Low_noise_reliably_finds_the_true_top_3_set_within_the_tap_budget(double noise, double minimumTop3SetRate)
    {
        var results = Seeds.Select(seed => RankingSimulation.Run(250, seed, noise)).ToList();

        var avgTaps = results.Average(r => r.Taps);
        var top3SetRate = results.Count(r => r.Top3SetHit) / (double)results.Count;

        await Assert.That(avgTaps).IsLessThanOrEqualTo(280);
        await Assert.That(top3SetRate).IsGreaterThanOrEqualTo(minimumTop3SetRate);
    }
}
