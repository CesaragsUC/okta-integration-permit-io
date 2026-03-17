namespace OktaInlineHookPermitIOIntegration.Models;

using Newtonsoft.Json;

public record PermitAssignRoleRequest(
    [property: JsonProperty("roles")] string[] Roles,
    [property: JsonProperty("tenant")] string Tenant
);