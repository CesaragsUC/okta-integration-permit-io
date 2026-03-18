using Newtonsoft.Json;
using OktaInlineHookPermitIOIntegration.Models;
using System.Net.Http;

namespace OktaInlineHookPermitIOIntegration.Services;

public interface IPermitApiClient
{
    /// <summary>
    /// Create or update a user with attributes and roles in Permit based on the provided request. This is typically called after receiving a token inline hook from Okta to ensure the user exists in Permit with the correct roles before performing authorization checks.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task CreateUserAsync(PermitSyncUserRequest request, CancellationToken ct = default);
    Task AssignRoleAsync(string userKey, PermitAssignRoleRequest request, CancellationToken ct = default);
    Task<PermitUserResponse> GetUserAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Basic user creation in Permit with just email, first name, and last name. This can be used for initial user provisioning without roles or additional attributes. Roles can be assigned later using the AssignRoleAsync method.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="firstName"></param>
    /// <param name="lastName"></param>
    /// <returns></returns>
    Task CreateUserAsync(string email, string firstName, string lastName);
    Task UpdateUserAsync(string email, string firstName, string lastName);
    Task EnsureTenantAsync(string tenantKey, string tenantName);
    Task EnsureRoleAsync(string roleKey, string roleName, CancellationToken ct = default);
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
    public async Task CreateUserAsync(PermitSyncUserRequest request, CancellationToken ct = default)
    {
        try
        {
            var url = $"v2/facts/{_projectId}/{_environmentId}/users";
            var response = await _http.PostAsJsonAsync(url, request, ct);

            // 409 = user already exists, that's fine
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                _logger.LogInformation("User already exists in Permit.io. Key={Key}", request.Key);
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Permit SyncUser failed. Status={Status}, Body={Body}",
                    response.StatusCode, body);
                response.EnsureSuccessStatusCode();
            }

            _logger.LogInformation("Permit SyncUser OK. UserKey={Key}", request.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing user in Permit API. UserKey={UserKey}", request.Key);
            throw;
        }

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

    public async Task<PermitUserResponse> GetUserAsync(string email, CancellationToken ct = default)
    {

        try
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing user in Permit API. Email={Email}", email);
            throw;
        }

    }

    // POST /v2/facts/{proj_id}/{env_id}/users/{user_key}/roles
    public async Task AssignRoleAsync(string userKey, PermitAssignRoleRequest request, CancellationToken ct = default)
    {
        try
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
                userKey, request.Role, request.Tenant);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role in Permit API. UserKey={UserKey}, Role={Role}, Tenant={Tenant}",
                userKey, request.Role, request.Tenant);
            throw;
        }

    }

    public async Task UpdateUserAsync(string email, string firstName, string lastName)
    {
        var body = new
        {
            key = email,
            email,
            first_name = firstName,
            last_name = lastName
        };

        await _http.PutAsJsonAsync($"v2/facts/{_projectId}/{_environmentId}/users/{Uri.EscapeDataString(email)}",body);
    }

    public async Task EnsureTenantAsync(string tenantKey, string tenantName)
    {
        try
        {
            var response = await _http.PostAsJsonAsync($"v2/facts/{_projectId}/{_environmentId}/tenants",
                                                    new { key = tenantKey, name = tenantName });

            // 409 Conflict = já existe, tudo ok
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                return;

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring tenant in Permit API. TenantKey={TenantKey}, TenantName={TenantName}",
                tenantKey, tenantName);
            throw;
        }

    }

    public async Task EnsureRoleAsync(string roleKey, string roleName, CancellationToken ct = default)
    {
        var url = $"v2/schema/{_projectId}/{_environmentId}/roles";
        var body = new { key = roleKey, name = roleName };
        var response = await _http.PostAsJsonAsync(url, body, ct);

        // 409 = already exists, fine
        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            return;

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Permit EnsureRole failed. Role={Role}, Status={Status}, Body={Body}",
                roleKey, response.StatusCode, responseBody);
        }
    }
}
