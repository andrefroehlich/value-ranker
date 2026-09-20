using ValueRanker.Core.Domain;

namespace ValueRanker.Core.Tests.Domain;

public class ValueListJsonLoaderTests
{
    private const string ValidJson = """
        {
          "listId": "de-default",
          "language": "de",
          "version": 1,
          "title": "Persönliche Arbeitswerte",
          "values": [
            { "id": "de-001", "name": "Verlässlichkeit", "description": "Zusagen einhalten." },
            { "id": "de-002", "name": "Offenheit", "description": "Neuem gegenüber aufgeschlossen sein." }
          ]
        }
        """;

    [Test]
    public async Task Parses_a_valid_list()
    {
        var result = ValueListJsonLoader.Parse(ValidJson);

        await Assert.That(result.ListId).IsEqualTo("de-default");
        await Assert.That(result.Language).IsEqualTo("de");
        await Assert.That(result.Version).IsEqualTo(1);
        await Assert.That(result.Title).IsEqualTo("Persönliche Arbeitswerte");
        await Assert.That(result.Values.Count).IsEqualTo(2);
        await Assert.That(result.Values[0]).IsEqualTo(new ValueItem("de-001", "Verlässlichkeit", "Zusagen einhalten."));
    }

    [Test]
    public async Task Rejects_duplicate_ids()
    {
        const string json = """
            {
              "listId": "de-default",
              "language": "de",
              "version": 1,
              "title": "Persönliche Arbeitswerte",
              "values": [
                { "id": "de-001", "name": "Verlässlichkeit", "description": "Zusagen einhalten." },
                { "id": "de-001", "name": "Offenheit", "description": "Neuem gegenüber aufgeschlossen sein." }
              ]
            }
            """;

        var exception = await Assert.ThrowsAsync<ValueListValidationException>(() => Task.Run(() => ValueListJsonLoader.Parse(json)));

        await Assert.That(exception!.Errors).Contains(e => e.Contains("Duplicate value id 'de-001'"));
    }

    [Test]
    public async Task Rejects_empty_name()
    {
        const string json = """
            {
              "listId": "de-default",
              "language": "de",
              "version": 1,
              "title": "Persönliche Arbeitswerte",
              "values": [
                { "id": "de-001", "name": "  ", "description": "Zusagen einhalten." }
              ]
            }
            """;

        var exception = await Assert.ThrowsAsync<ValueListValidationException>(() => Task.Run(() => ValueListJsonLoader.Parse(json)));

        await Assert.That(exception!.Errors).Contains(e => e.Contains("empty name"));
    }

    [Test]
    public async Task Rejects_empty_description()
    {
        const string json = """
            {
              "listId": "de-default",
              "language": "de",
              "version": 1,
              "title": "Persönliche Arbeitswerte",
              "values": [
                { "id": "de-001", "name": "Verlässlichkeit", "description": "" }
              ]
            }
            """;

        var exception = await Assert.ThrowsAsync<ValueListValidationException>(() => Task.Run(() => ValueListJsonLoader.Parse(json)));

        await Assert.That(exception!.Errors).Contains(e => e.Contains("empty description"));
    }

    [Test]
    public async Task Rejects_missing_values()
    {
        const string json = """
            {
              "listId": "de-default",
              "language": "de",
              "version": 1,
              "title": "Persönliche Arbeitswerte",
              "values": []
            }
            """;

        var exception = await Assert.ThrowsAsync<ValueListValidationException>(() => Task.Run(() => ValueListJsonLoader.Parse(json)));

        await Assert.That(exception!.Errors).Contains(e => e.Contains("at least one entry"));
    }

    [Test]
    public async Task Rejects_missing_list_id()
    {
        const string json = """
            {
              "listId": "",
              "language": "de",
              "version": 1,
              "title": "Persönliche Arbeitswerte",
              "values": [
                { "id": "de-001", "name": "Verlässlichkeit", "description": "Zusagen einhalten." }
              ]
            }
            """;

        var exception = await Assert.ThrowsAsync<ValueListValidationException>(() => Task.Run(() => ValueListJsonLoader.Parse(json)));

        await Assert.That(exception!.Errors).Contains(e => e.Contains("listId must not be empty"));
    }
}
