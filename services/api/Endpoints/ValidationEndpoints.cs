using System.Text.Json;
using Central.Engine.Services;

namespace Central.Api.Endpoints;

public static class ValidationEndpoints
{
    public static RouteGroupBuilder MapValidationEndpoints(this RouteGroupBuilder group)
    {
        // Validate a record against registered rules
        group.MapPost("/validate/{entityType}", (string entityType, JsonElement body) =>
        {
            // Build a dynamic object from the JSON for validation
            var dict = new Dictionary<string, object?>();
            foreach (var prop in body.EnumerateObject())
                dict[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : prop.Value.GetRawText();

            // Create a dynamic wrapper that the validator can reflect on
            var result = DataValidationService.Instance.Validate(entityType, new DynamicEntity(dict));
            return Results.Ok(new { result.IsValid, result.Errors, result.ErrorSummary });
        });

        return group;
    }

    private class DynamicEntity
    {
        private readonly Dictionary<string, object?> _values;
        public DynamicEntity(Dictionary<string, object?> values) => _values = values;

        // Reflection-based property access for the validator
        public string? SwitchName => _values.GetValueOrDefault("SwitchName")?.ToString();
        public string? Building => _values.GetValueOrDefault("Building")?.ToString();
        public string? Username => _values.GetValueOrDefault("Username")?.ToString();
        public string? Subject => _values.GetValueOrDefault("Subject")?.ToString();
        public string? Name => _values.GetValueOrDefault("Name")?.ToString();
        public string? Code => _values.GetValueOrDefault("Code")?.ToString();
        public string? EntityType => _values.GetValueOrDefault("EntityType")?.ToString();
        public string? Prefix => _values.GetValueOrDefault("Prefix")?.ToString();
    }
}
