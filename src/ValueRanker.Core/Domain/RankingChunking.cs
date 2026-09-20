namespace ValueRanker.Core.Domain;

/// <summary>
/// Splits a list into chunks of at most chunkSize, merging a trailing remainder smaller than
/// minSize into the previous chunk so no chunk ever ends up too small to compare (e.g. size 1).
/// </summary>
internal static class RankingChunking
{
    public static IReadOnlyList<IReadOnlyList<T>> ChunkWithMinSize<T>(IReadOnlyList<T> items, int chunkSize, int minSize)
    {
        var result = new List<IReadOnlyList<T>>();
        var i = 0;

        while (i < items.Count)
        {
            var remaining = items.Count - i;
            int take;

            if (remaining > chunkSize && remaining - chunkSize < minSize)
            {
                take = remaining;
            }
            else
            {
                take = Math.Min(chunkSize, remaining);
            }

            result.Add(items.Skip(i).Take(take).ToList());
            i += take;
        }

        return result;
    }

    public static IReadOnlyList<string> Shuffle(int seed, IReadOnlyList<string> items)
    {
        var shuffled = items.ToList();
        var random = new Random(seed);

        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }
}
