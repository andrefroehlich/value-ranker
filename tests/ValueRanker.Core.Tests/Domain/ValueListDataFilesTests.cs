using ValueRanker.Core.Domain;

namespace ValueRanker.Core.Tests.Domain;

public class ValueListDataFilesTests
{
    [Test]
    public async Task German_value_list_loads_and_validates()
    {
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "TestData", "values.de.json"));

        var list = ValueListJsonLoader.Parse(json);

        await Assert.That(list.Language).IsEqualTo("de");
        await Assert.That(list.Values.Count).IsGreaterThanOrEqualTo(200);
        await Assert.That(list.Values.Select(v => v.Id).Distinct().Count()).IsEqualTo(list.Values.Count);
    }

    [Test]
    public async Task English_value_list_loads_and_validates()
    {
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "TestData", "values.en.json"));

        var list = ValueListJsonLoader.Parse(json);

        await Assert.That(list.Language).IsEqualTo("en");
        await Assert.That(list.Values.Count).IsGreaterThanOrEqualTo(200);
        await Assert.That(list.Values.Select(v => v.Id).Distinct().Count()).IsEqualTo(list.Values.Count);
    }
}
