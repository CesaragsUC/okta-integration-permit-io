using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using OktaInlineHookPermitIOIntegration.Repository;
using OktaInlineHookPermitIOIntegration.Services;

namespace OktaInlineHookPermitIOIntegration.Controllers;

[ApiController]
[Route("api/okta")]
public class OktaController : ControllerBase
{
    private readonly IPermitApiClient _permitApi;
    private readonly ILogger<OktaController> _logger;
    private readonly IOktaUserRepository _oktaUserRepository;
    private readonly  IConfiguration _configuration;

    public OktaController(
        IPermitApiClient permitApiClient,
        ILogger<OktaController> logger,
        IConfiguration configuration,
        IOktaUserRepository oktaUserRepository)
    {
        _permitApi = permitApiClient;
        _logger = logger;
        _configuration = configuration;
        _oktaUserRepository = oktaUserRepository;
    }

    [HttpPost("token-hook")]
    public async Task<IActionResult> HandleTokenHook([FromBody] JObject payload)
    {
        _logger.LogInformation("Payload: {Payload}", payload.ToString());

        var email = payload
            .SelectToken("data.context.session.login")?
            .ToString();

        if (string.IsNullOrEmpty(email))
            return Ok(new { commands = Array.Empty<object>() });

        // Find user in Permit and get their roles and associated tenants. If the user doesn't exist in Permit, return an empty command list.
        var permitUser = await _permitApi.SyncUserAsync(email);

        if (permitUser is null)
            return Ok(new { commands = Array.Empty<object>() });

        return Ok(new
        {
            commands = new object[]
            {
                new
                {
                    type = "com.okta.access.patch",
                    value = new object[]
                    {
                        new
                        {
                            op = "add",
                            path = "/claims/roles",
                            value = permitUser.Roles
                        },
                        new
                        {
                            op = "add",
                            path = "/claims/associated_tenants",
                            value = permitUser.AssociatedTenants
                        }
                    }
                }
            }
        });
    }

    // Okta Event Hook verification (one-time)
    [HttpGet("user-created")]
    public IActionResult VerifyEventHook()
    {
        var verificationValue = Request.Headers["x-okta-verification-challenge"].ToString();
        return Ok(new { verification = verificationValue });
    }

    [HttpPost("user-created")]
    public async Task<IActionResult> HandleUserCreated([FromBody] JObject payload)
    {
        var events = payload.SelectToken("data.events");
        if (events == null)
            return Ok();

        foreach (var evt in events)
        {
            var userId = evt.SelectToken("target[0].id")?.ToString();
            var email = evt.SelectToken("target[0].alternateId")?.ToString();
            var displayName = evt.SelectToken("target[0].displayName")?.ToString();

            if (string.IsNullOrEmpty(email))
                continue;

            var nameParts = displayName?.Split(' ', 2) ?? [];
            var firstName = nameParts.ElementAtOrDefault(0) ?? "";
            var lastName = nameParts.ElementAtOrDefault(1) ?? "";

            await _permitApi.CreateUserAsync(email, firstName, lastName);
        }

        return Ok();
    }

    //[HttpPost("user-created")]
    //public async Task<IActionResult> HandleUserCreated([FromBody] JObject payload)
    //{
    //    _logger.LogInformation("Event Hook Payload: {Payload}", payload.ToString());

    //    var events = payload.SelectToken("data.events");
    //    if (events == null)
    //        return Ok();

    //    foreach (var evt in events)
    //    {
    //        var email = evt.SelectToken("target[0].alternateId")?.ToString();

    //        if (string.IsNullOrEmpty(email))
    //            continue;

    //        // Busca o perfil completo do usuário via Okta API
    //        var userId = evt.SelectToken("target[0].id")?.ToString();
    //        var user = await GetOktaUserProfile(userId);

    //        var tenantId = user?.SelectToken("profile.tenantId")?.ToString() ?? "default";
    //        var permitRoles = user?.SelectToken("profile.permitRoles")?.ToString() ?? "";
    //        var firstName = user?.SelectToken("profile.firstName")?.ToString() ?? "";
    //        var lastName = user?.SelectToken("profile.lastName")?.ToString() ?? "";

    //        var roles = permitRoles
    //            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    //            .ToArray();

    //        // 1. Cria usuário no Permit.io
    //        await CreatePermitUser(email, firstName, lastName);

    //        // 2. Atribui roles no tenant
    //        foreach (var role in roles)
    //        {
    //            await AssignPermitRole(email, role, tenantId);
    //        }
    //    }

    //    return Ok();
    //}

    private async Task<JObject?> GetOktaUserProfile(string userId)
    {
        var oktaDomain = _configuration["OktaHook:BaseUrl"];
        var apiToken = _configuration["Okta:ApiToken"]; // Okta API token

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"SSWS {apiToken}");

        var response = await client.GetAsync($"{oktaDomain}/api/v1/users/{userId}");
        if (!response.IsSuccessStatusCode) return null;

        return JObject.Parse(await response.Content.ReadAsStringAsync());
    }

    private async Task CreatePermitUser(string email, string firstName, string lastName)
    {
        var permitKey = _configuration["Permit:ApiKey"];
        var projectId = _configuration["Permit:ProjectId"];
        var envId = _configuration["Permit:EnvironmentId"];

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {permitKey}");

        var body = new
        {
            key = email,
            email,
            first_name = firstName,
            last_name = lastName
        };

        await client.PostAsJsonAsync(
            $"https://api.permit.io/v2/facts/{projectId}/{envId}/users", body);
    }

    private async Task AssignPermitRole(string email, string role, string tenant)
    {
        var permitKey = _configuration["Permit:ApiKey"];
        var projectId = _configuration["Permit:ProjectId"];
        var envId = _configuration["Permit:EnvironmentId"];

        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {permitKey}");

        var body = new
        {
            user = email,
            role,
            tenant
        };

        await client.PostAsJsonAsync(
            $"https://api.permit.io/v2/facts/{projectId}/{envId}/role_assignments", body);
    }

    private async Task GetOrUpdateSiteType([FromBody] JObject payload)
    {
        // Estrategy 1:  in case siteType is already populated in the token hook payload (after backfill completes)
        var siteType = payload
            .SelectToken("data.context.user.profile.siteType")?
            .ToString();

        // Estrategy 2: fallback via salesforceid while backfill is in progress, then eventually as the main strategy after backfill completes and siteType is populated in Okta profile.
        // This is to avoid having to call Okta API to get user profile for every token hook invocation, which would add latency.
        if (string.IsNullOrEmpty(siteType))
        {
            var salesforceId = payload
                .SelectToken("data.context.user.profile.salesforceId")?
                .ToString();

            if (!string.IsNullOrEmpty(salesforceId))
            {
                siteType = await _oktaUserRepository.GetSiteTypeBySalesforceIdAsync(salesforceId);
                if (!string.IsNullOrEmpty(siteType))
                {
                    await _oktaUserRepository.SetSiteType(siteType, salesforceId);

                    _logger.LogInformation("SiteType {SiteType} set for user with SalesforceId {SalesforceId}", siteType, salesforceId);
                }
            }
        }
    }
}