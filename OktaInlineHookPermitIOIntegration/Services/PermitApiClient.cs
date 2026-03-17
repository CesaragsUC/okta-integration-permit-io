using Newtonsoft.Json;
using OktaInlineHookPermitIOIntegration.Models;
using System.Net.Http;

namespace OktaInlineHookPermitIOIntegration.Services;

public interface IPermitApiClient
{
    Task SyncUserAsync(PermitSyncUserRequest request, CancellationToken ct = default);
    Task AssignRoleAsync(string userKey, PermitAssignRoleRequest request, CancellationToken ct = default);
    Task<PermitUserResponse> SyncUserAsync(string email, CancellationToken ct = default);
    Task CreateUserAsync(string email, string firstName, string lastName);
}

public class PermitApiClient : IPermitApiClient
{
    private readonly HttpClient _http;
    private readonly string _projectId;
    private readonly string _environmentId;
    private readonly ILogger<PermitApiClient> _logger;

    // Base URL: https://api.permit.io/v2/facts/{proj_id}/{env_id}
    public PermitApiClient(HttpClient http, IConfiguration config, ILogger<PermitApiClient> logger)
    {
        _http = http;
        _projectId = config["Permit:ProjectId"]!;
        _environmentId = config["Permit:EnvironmentId"]!;
        _logger = logger;
    }

    // POST /v2/facts/{proj_id}/{env_id}/users
    public async Task SyncUserAsync(PermitSyncUserRequest request, CancellationToken ct = default)
    {
        var url = $"v2/facts/{_projectId}/{_environmentId}/users";
        var response = await _http.PostAsJsonAsync(url, request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Permit SyncUser failed. Status={Status}, Body={Body}",
                response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("Permit SyncUser OK. UserKey={Key}", request.Key);
    }

    public async Task<PermitUserResponse> SyncUserAsync(string email, CancellationToken ct = default)
    {
        var url = $"v2/facts/{_projectId}/{_environmentId}/users/{email}";
        var response = await _http.GetAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogInformation($"Permit API error: {response.StatusCode}");
            return null;
        }

        var permitUser = JsonConvert.DeserializeObject<PermitUserResponse>(await response.Content.ReadAsStringAsync());

        return permitUser;
    }

    public async Task CreateUserAsync(string email, string firstName, string lastName)
    {
        try
        {
            var body = new
            {
                key = email,
                email,
                first_name = firstName,
                last_name = lastName
            };

            await _http.PostAsJsonAsync($"v2/facts/{_projectId}/{_environmentId}/users", body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user in Permit API. Email={Email}", email);
            throw;
        }

    }

    // POST /v2/facts/{proj_id}/{env_id}/users/{user_key}/roles
    public async Task AssignRoleAsync(string userKey, PermitAssignRoleRequest request, CancellationToken ct = default)
    {
        var url = $"v2/facts/{_projectId}/{_environmentId}/users/{Uri.EscapeDataString(userKey)}/roles";
        var response = await _http.PostAsJsonAsync(url, request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Permit AssignRole failed. User={User}, Status={Status}, Body={Body}",
                userKey, response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        _logger.LogInformation("Permit AssignRole OK. UserKey={Key}, Role={Role}, Tenant={Tenant}",
            userKey, request.Roles, request.Tenant);
    }

    public async Task EnsureTenantAsync(string tenantKey, string tenantName)
    {
        var response = await _http.PostAsJsonAsync($"v2/facts/{_projectId}/{_environmentId}/tenants",
            new { key = tenantKey, name = tenantName });

        // 409 Conflict = já existe, tudo ok
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            return;

        response.EnsureSuccessStatusCode();
    }
}
