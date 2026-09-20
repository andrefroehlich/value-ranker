using ValueRanker.Core.Domain;

namespace ValueRanker.Core.Tests.Simulation;

/// <summary>
/// Simulates a user who knows a true best-to-worst order but perceives it with noise.
/// More noise means more mistakes, and closely-ranked values are naturally more error-prone
/// than far-apart ones since the noise is a fixed-size perturbation of rank position.
/// </summary>
public sealed class VirtualUser
{
    private readonly Dictionary<string, int> _trueRank;
    private readonly double _noise;
    private readonly Random _random;

    public VirtualUser(IReadOnlyList<string> trueOrderBestToWorst, double noise, Random random)
    {
        _trueRank = trueOrderBestToWorst
            .Select((id, index) => (id, index))
            .ToDictionary(x => x.id, x => x.index);
        _noise = noise;
        _random = random;
    }

    public GroupBestWorstEvent AnswerBestWorst(NextGroupStep step)
    {
        var ordered = OrderByPerceivedRank(step.ValueIds);
        return new GroupBestWorstEvent(step.ValueIds, ordered[0], ordered[^1]);
    }

    public GroupFullOrderEvent AnswerFullOrder(NextGroupStep step)
        => new(OrderByPerceivedRank(step.ValueIds));

    public DuelEvent AnswerDuel(NextDuelStep step)
    {
        var ordered = OrderByPerceivedRank([step.LeftId, step.RightId]);
        var result = ordered[0] == step.LeftId ? DuelResult.Left : DuelResult.Right;
        return new DuelEvent(step.LeftId, step.RightId, result);
    }

    private List<string> OrderByPerceivedRank(IReadOnlyList<string> ids)
        => ids
            .Select(id => (id, perceived: _trueRank[id] + NextNoise()))
            .OrderBy(x => x.perceived)
            .Select(x => x.id)
            .ToList();

    private double NextNoise()
    {
        if (_noise <= 0)
        {
            return 0;
        }

        var u1 = 1.0 - _random.NextDouble();
        var u2 = _random.NextDouble();
        var gaussian = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
        return gaussian * _noise;
    }
}
