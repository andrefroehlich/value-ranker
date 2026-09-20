namespace ValueRanker.Core.Tests;

public class PlaceholderTests
{
    [Test]
    public async Task Sanity_check_passes()
    {
        var values = new[] { "a", "b", "c" };

        await Assert.That(values.Length).IsEqualTo(3);
    }
}
