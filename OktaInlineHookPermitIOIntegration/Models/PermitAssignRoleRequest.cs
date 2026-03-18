namespace OktaInlineHookPermitIOIntegration.Models;

using System.Text.Json.Serialization;

public record PermitAssignRoleRequest(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("tenant")] string Tenant
);