namespace OktaInlineHookPermitIOIntegration.Models;

using Newtonsoft.Json;

public record PermitSyncUserRequest(
    [property: JsonProperty("key")] string Key,
    [property: JsonProperty("email")] string Email,
    [property: JsonProperty("first_name")] string? FirstName,
    [property: JsonProperty("last_name")] string? LastName,
    [property: JsonProperty("attributes")] Dictionary<string, object>? Attributes
);
