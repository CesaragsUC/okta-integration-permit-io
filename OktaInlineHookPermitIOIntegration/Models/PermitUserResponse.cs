namespace OktaInlineHookPermitIOIntegration.Models;

using Newtonsoft.Json;
using System.Text.Json.Serialization;

public sealed class PermitUserResponse
{
    [JsonProperty("key")]
    public string Key { get; set; } = string.Empty;

    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("organization_id")]
    public string OrganizationId { get; set; } = string.Empty;

    [JsonProperty("project_id")]
    public string ProjectId { get; set; } = string.Empty;

    [JsonProperty("environment_id")]
    public string EnvironmentId { get; set; } = string.Empty;

    [JsonProperty("associated_tenants")]
    public List<AssociatedTenant> AssociatedTenants { get; set; } = [];

    [JsonProperty("roles")]
    public List<RoleAssignment> Roles { get; set; } = [];

    [JsonProperty("email")]
    public string Email { get; set; } = string.Empty;

    [JsonProperty("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("last_name")]
    public string LastName { get; set; } = string.Empty;
}

public sealed class AssociatedTenant
{
    [JsonProperty("tenant")]
    public string Tenant { get; set; } = string.Empty;

    [JsonProperty("roles")]
    public List<string> Roles { get; set; } = [];

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;
}

public sealed class RoleAssignment
{
    [JsonProperty("role")]
    public string Role { get; set; } = string.Empty;

    [JsonProperty("tenant")]
    public string Tenant { get; set; } = string.Empty;
}
