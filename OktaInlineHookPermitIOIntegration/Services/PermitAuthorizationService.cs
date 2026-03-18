using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using OktaInlineHookPermitIOIntegration.Helpers;
using OktaInlineHookPermitIOIntegration.Models;
using PermitSDK;
using PermitSDK.Models;
using PermitSDK.OpenAPI.Models;
using System.Security.Claims;

namespace OktaInlineHookPermitIOIntegration.Services;

public interface IPermitAuthorizationService
{
    Task<bool> IsAllowedAsync(UserKey user,
        string action,
        string resource,
        string? resourceId = null,
        Dictionary<string, dynamic>? resourceAttributes = null);

    Task<UserKey> GetUserKeyFromClaims(ClaimsPrincipal user);

    Task<PermitUserResponse> GetUserAsync(string email, CancellationToken ct = default);

    /// <summary>
    /// Create or update a user with attributes and roles in Permit based on the provided request. This is typically called after receiving a token inline hook from Okta to ensure the user exists in Permit with the correct roles before performing authorization checks.
    /// </summary>
    /// <param name="request"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task CreateUserAsync(PermitSyncUserRequest request, CancellationToken ct = default);

    /// <summary>
    /// Basic user creation in Permit with just email, first name, and last name. This can be used for initial user provisioning without roles or additional attributes. Roles can be assigned later using the AssignRoleAsync method.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="firstName"></param>
    /// <param name="lastName"></param>
    /// <returns></returns>
    Task CreateUserAsync(string email, string firstName, string lastName);

    Task AssignRoleAsync(string userKey, PermitAssignRoleRequest request, CancellationToken ct = default);
    Task AssignRoleAsync(string email, string role, string tenant);
    Task EnsureTenantAsync(string tenantKey, string tenantName);
    Task EnsureRoleAsync(string roleKey, string roleName, CancellationToken ct = default);
    Task<RoleRead> CreateRoleAsync(string roleKey, string roleName, string resourceName = "", string action = "", CancellationToken ct = default);
    Task<RoleRead> GetOrCreateRoleAsync(string roleKey,string roleName = "");
    Task<JObject?> GetOktaUserProfile(string userId);
    Task CreateUserAsync(UserCreate user);
}



public class PermitAuthorizationService : IPermitAuthorizationService
{
    private readonly Permit _permit;
    private readonly IPermitApiClient _permitApi;
    private readonly ILogger<PermitAuthorizationService> _logger;
    private readonly IHttpContextAccessor _httpContext;
    private readonly IConfiguration _configuration;

    public PermitAuthorizationService(IConfiguration configuration,
        ILogger<PermitAuthorizationService> logger,
        IHttpContextAccessor httpContext,
        IPermitApiClient permitApi)
    {
        _configuration = configuration;
        var permitToken = _configuration["Permit:ApiKey"];
        var permitPdpUrl = _configuration["Permit:PdpUrl"];
        var enviromentId = _configuration["Permit:EnvironmentId"];
        var projectId = _configuration["Permit:ProjectId"];

        _permit = new Permit(token:permitToken,pdp: permitPdpUrl,projectId: projectId, envId: enviromentId);
        _logger = logger;
        _httpContext = httpContext;
        _permitApi = permitApi;
    }


    public async Task<bool> IsAllowedAsync(
        UserKey user,
        string action,
        string resource,
        string? resourceId = null,
        Dictionary<string, dynamic>? resourceAttributes = null)
    {
        try
        {
            var userTenant = user.attributes.TryGetValue("tenant", out var tenantObj) ? tenantObj?.ToString() : "default";


            var resourceObj = new ResourceInput(
                type: resource,
                key: resourceId,
                tenant: userTenant,  // "sg2" or "default". oly for test,vizient
                attributes: resourceAttributes
            );

            var permitted = await _permit.Check(user, action, resourceObj);

            _logger.LogInformation(
                "Permission check: User={UserKey}, Action={Action}, Resource={Resource}, ResourceId={ResourceId}, Result={Result}",
                user.key, action, resource, resourceId ?? "null", permitted);

            return permitted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking permission for user {UserKey}", user.key);
            return false;
        }
    }

    public async Task<UserKey> GetUserKeyFromClaims(ClaimsPrincipal user)
    {
        var email = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst(ClaimTypes.Email)?.Value
            ?? user.FindFirst("email")?.Value
            ?? throw new InvalidOperationException("User email not found in claims");

        string[] zipcodes = user.FindAll("zipcode")
                        .Select(c => ZipHelper.Normalize(c.Value))
                        .ToArray();

        string[] department = user.FindAll("department").Select(c => c.Value).ToArray();

        string[] roles = user.FindAll("roles").Select(c => c.Value)
                        .ToArray();

        string tenant = user.FindFirst("tenant")?.Value ?? string.Empty;

        string[] organizations = user.FindAll("organizations").Select(c => c.Value).ToArray();

        return new UserKey(
            key: email,
            firstName: string.Empty,
            lastName: string.Empty,
            email: email,
            attributes: new Dictionary<string, object>
            {
                   { "roles", roles },
                   { "department", department },
                   { "zipcode", zipcodes.Select(ZipHelper.Normalize).ToArray() },
                   { "organizations", organizations},
                   { "tenant", tenant},
            }
        );
    }


    public async Task CreateUserAsync(PermitSyncUserRequest userRequest, CancellationToken ct = default)
    {
        await _permitApi.CreateUserAsync(userRequest, ct);

    }

    public async Task<PermitUserResponse> GetUserAsync(string email, CancellationToken ct = default)
    {
        var permitUser = await _permitApi.GetUserAsync(email, ct);

        return permitUser;
    }

    public async Task CreateUserAsync(string email, string firstName, string lastName)
    {
        await _permitApi.CreateUserAsync(email, firstName, lastName);
    }

    public async Task AssignRoleAsync(string userKey, PermitAssignRoleRequest request, CancellationToken ct = default)
    {
        await _permitApi.AssignRoleAsync(userKey, request, ct);
    }

    public async Task EnsureTenantAsync(string tenantKey, string tenantName)
    {
        await _permitApi.EnsureTenantAsync(tenantKey, tenantName);
    }

    public async Task<JObject?> GetOktaUserProfile(string userId)
    {
        var oktaDomain = _configuration["OktaHook:BaseUrl"];
        var apiToken = _configuration["Okta:ApiToken"]; // Okta API token

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"SSWS {apiToken}");

        var response = await client.GetAsync($"{oktaDomain}/api/v1/users/{userId}");
        if (!response.IsSuccessStatusCode) return null;

        return JObject.Parse(await response.Content.ReadAsStringAsync());
    }
    private static string SanitizeEmail(string email)
    {
        return email.Replace(".invalid", "", StringComparison.OrdinalIgnoreCase).Trim();
    }

    private static string ToPermitPattern(string metisRole)
    {
        return metisRole
            .Trim()
            .ToLower()
            .Replace(" ", "-")
            .Replace("/", "-");
    }

    public async Task EnsureRoleAsync(string roleKey, string roleName, CancellationToken ct = default)
    {
        await _permitApi.EnsureRoleAsync(roleKey, roleName, ct);
    }

    public async Task AssignRoleAsync(string email,string role,string tenant)
    {
        try
        {
            await _permit.Api.AssignRole(email, role, tenant);
        }
        catch (PermitApiException ex)
        {

            _logger.LogError(ex, $"Fail to Assign role to {email}");
            throw;
        }


    }

    public async Task<RoleRead> CreateRoleAsync(string roleKey,string roleName,string resourceName = "", string action = "", CancellationToken ct = default)
    {
        try
        {
            var roleData = new RoleCreate
            {
                Key = ToPermitPattern(roleKey),
                Name = roleName,
                Description = $"Role for {roleName} from Okta sync",
                Permissions = !string.IsNullOrWhiteSpace(resourceName) && !string.IsNullOrWhiteSpace(action) ? new List<string> { $"{ToPermitPattern(resourceName.ToLower())}:{action}" } : null
            };

            RoleRead admin = await _permit.Api.CreateRole(roleData);

            return admin;
        }
        catch (PermitApiException ex) when (ex.Message.Contains("409"))
        {
            _logger.LogError(ex, "Role Already exists");
            return null;
        }
    }

    public async Task CreateUserAsync(UserCreate user)
    {
        try
        {
            await _permit.Api.CreateUser(user);
        }
        catch (PermitApiException ex)
        {
            throw;
        }

    }

    public async Task<RoleRead> GetOrCreateRoleAsync(string roleKey, string roleName = "")
    {
        RoleRead role;
        try
        {
            role = await _permit.Api.GetRole(roleKey);
            return role;
        }
        catch (PermitApiException ex) when (ex.Message.Contains("404"))
        {
            role = await _permit.Api.CreateRole(new RoleCreate
            {
                Key = roleKey,
                Name = roleName,
                Description = $"Role for {roleName} from Okta sync"
            });

            return role;
        }

    }
}