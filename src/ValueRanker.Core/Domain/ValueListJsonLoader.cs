using System.Text.Json;

namespace ValueRanker.Core.Domain;

public static class ValueListJsonLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static ValueList Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<ValueListDto>(json, SerializerOptions)
            ?? throw new ValueListValidationException(["JSON content is empty or 'null'."]);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.ListId))
        {
            errors.Add("listId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(dto.Language))
        {
            errors.Add("language must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            errors.Add("title must not be empty.");
        }

        if (dto.Values is null || dto.Values.Count == 0)
        {
            errors.Add("values must contain at least one entry.");
        }
        else
        {
            var seenIds = new HashSet<string>();

            foreach (var value in dto.Values)
            {
                if (string.IsNullOrWhiteSpace(value.Id))
                {
                    errors.Add("A value has an empty id.");
                }
                else if (!seenIds.Add(value.Id))
                {
                    errors.Add($"Duplicate value id '{value.Id}'.");
                }

                if (string.IsNullOrWhiteSpace(value.Name))
                {
                    errors.Add($"Value '{value.Id}' has an empty name.");
                }

                if (string.IsNullOrWhiteSpace(value.Description))
                {
                    errors.Add($"Value '{value.Id}' has an empty description.");
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ValueListValidationException(errors);
        }

        var values = dto.Values!
            .Select(v => new ValueItem(v.Id!, v.Name!, v.Description!))
            .ToList();

        return new ValueList(dto.ListId!, dto.Language!, dto.Version, dto.Title!, values);
    }

    private sealed class ValueListDto
    {
        public string? ListId { get; set; }

        public string? Language { get; set; }

        public int Version { get; set; }

        public string? Title { get; set; }

        public List<ValueItemDto>? Values { get; set; }
    }

    private sealed class ValueItemDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public string? Description { get; set; }
    }
}
