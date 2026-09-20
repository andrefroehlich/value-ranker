namespace ValueRanker.Core.Domain;

public static class GroupEventToPairwiseComparisons
{
    public static IReadOnlyList<PairwiseComparison> FromBestWorst(IReadOnlyList<string> valueIds, string bestId, string worstId)
    {
        var result = new List<PairwiseComparison>();

        foreach (var id in valueIds)
        {
            if (id != bestId)
            {
                result.Add(new PairwiseComparison(bestId, id, 1.0));
            }
        }

        foreach (var id in valueIds)
        {
            if (id != bestId && id != worstId)
            {
                result.Add(new PairwiseComparison(id, worstId, 1.0));
            }
        }

        return result;
    }

    /// <summary>orderedValueIds is best to worst.</summary>
    public static IReadOnlyList<PairwiseComparison> FromFullOrder(IReadOnlyList<string> orderedValueIds)
    {
        var result = new List<PairwiseComparison>();

        for (var i = 0; i < orderedValueIds.Count; i++)
        {
            for (var j = i + 1; j < orderedValueIds.Count; j++)
            {
                result.Add(new PairwiseComparison(orderedValueIds[i], orderedValueIds[j], 1.0));
            }
        }

        return result;
    }

    public static PairwiseComparison FromDuel(string leftId, string rightId, DuelResult result)
    {
        var scoreLeft = result switch
        {
            DuelResult.Left => 1.0,
            DuelResult.Right => 0.0,
            DuelResult.Equal => 0.5,
            _ => throw new ArgumentOutOfRangeException(nameof(result)),
        };

        return new PairwiseComparison(leftId, rightId, scoreLeft);
    }
}
