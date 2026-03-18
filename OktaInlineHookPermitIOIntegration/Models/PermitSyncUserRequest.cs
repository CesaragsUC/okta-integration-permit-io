namespace OktaInlineHookPermitIOIntegration.Models;

using System.Text.Json.Serialization;

public record PermitSyncUserRequest(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("attributes")] Dictionary<string, object>? Attributes
);