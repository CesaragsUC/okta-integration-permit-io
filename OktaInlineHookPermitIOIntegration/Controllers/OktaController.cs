using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using OktaInlineHookPermitIOIntegration.Repository;
using OktaInlineHookPermitIOIntegration.Services;
using PermitSDK.OpenAPI.Models;


namespace OktaInlineHookPermitIOIntegration.Controllers;

[ApiController]
[Route("api/okta")]
public class OktaController : ControllerBase
{
    private readonly ILogger<OktaController> _logger;
    private readonly IOktaUserRepository _oktaUserRepository;
    private readonly IConfiguration _configuration;
    private readonly IPermitAuthorizationService _authorizationService;
    private static readonly HashSet<string> _knownUser = new();

    public OktaController(
        ILogger<OktaController> logger,
        IConfiguration configuration,
        IOktaUserRepository oktaUserRepository,
        IPermitAuthorizationService authorizationService)
    {
        _logger = logger;
        _configuration = configuration;
        _oktaUserRepository = oktaUserRepository;
        _authorizationService = authorizationService;
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
        var permitUser = await _authorizationService.GetUserAsync(email);

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

    [HttpGet("user-updated")]
    public IActionResult VerifyUserProfileUpdatedEventHook()
    {
        var verificationValue = Request.Headers["x-okta-verification-challenge"].ToString();
        return Ok(new { verification = verificationValue });
    }

    [HttpGet("user-deleted")]
    public IActionResult VerifyUserProfileDeletedEventHook()
    {
        var verificationValue = Request.Headers["x-okta-verification-challenge"].ToString();
        return Ok(new { verification = verificationValue });
    }

    // Option 1
    //[HttpPost("user-created")]
    //public async Task<IActionResult> HandleUserCreated([FromBody] JObject payload)
    //{
    //    var events = payload.SelectToken("data.events");
    //    if (events == null)
    //        return Ok();

    //    foreach (var evt in events)
    //    {
    //        var userId = evt.SelectToken("target[0].id")?.ToString();
    //        var email = evt.SelectToken("target[0].alternateId")?.ToString();
    //        var displayName = evt.SelectToken("target[0].displayName")?.ToString();

    //        if (string.IsNullOrEmpty(email))
    //            continue;

    //        var nameParts = displayName?.Split(' ', 2) ?? [];
    //        var firstName = nameParts.ElementAtOrDefault(0) ?? "";
    //        var lastName = nameParts.ElementAtOrDefault(1) ?? "";

    //        await _authorizationService.CreateUserAsync(email, firstName, lastName);
    //    }

    //    return Ok();
    //}


    ////Option 2
    [HttpPost("user-created")]
    public async Task<IActionResult> HandleUserCreatedV2([FromBody] JObject payload)
    {
        _logger.LogInformation("Event Hook Payload: {Payload}", payload.ToString());

        var events = payload.SelectToken("data.events");
        if (events == null)
            return Ok();

        foreach (var evt in events)
        {
            var email = evt.SelectToken("target[0].alternateId")?.ToString();

            if (string.IsNullOrEmpty(email))
                continue;

            // Busca o perfil completo do usuário via Okta API
            var userId = evt.SelectToken("target[0].id")?.ToString();
            // var user = await _authorizationService.GetOktaUserProfile(userId);
            var displayName = evt.SelectToken("target[0].displayName")?.ToString();
            var nameParts = displayName?.Split(' ', 2) ?? [];
            var firstName = nameParts.ElementAtOrDefault(0) ?? "";
            var lastName = nameParts.ElementAtOrDefault(1) ?? "";

            // var tenantId = user?.SelectToken("profile.tenantId")?.ToString() ?? "default";
            //var permitRoles = user?.SelectToken("profile.permitRoles")?.ToString() ?? "";


            //var roles = permitRoles
            //    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            //    .ToArray();

            var userMetis = await _oktaUserRepository.GetUserMetisData(email);
            if (userMetis is null)
                continue;


            // a block in case okta call api multiple times.
           // if (_knownUser.Add(email))
          //  {
                foreach (var role in userMetis.Roles)
                {
                    var roleKey = ToPermitPattern(role);
                    _logger.LogInformation("Processing role: original={Original}, key={Key}", role, roleKey);

                    await _authorizationService.GetOrCreateRoleAsync(roleKey, role);

                }

                var attributes = new Dictionary<string, object>
                {
                    { "department", userMetis.Department },
                    { "organization", userMetis.Organization ?? string.Empty },
                    { "zipcode", userMetis.ZipCodes.ToArray() },
                    { "tenant", userMetis.Tenant.ToLower() }
                };

                var roleAssignments = userMetis.Roles.Select(role => new UserRoleCreate
                {
                    Role = ToPermitPattern(role),
                    Tenant = userMetis.Tenant.ToLower()
                }).ToList();

                await _authorizationService.CreateUserAsync(new UserCreate
                {
                    Key = SanitizeEmail(email),
                    Email = SanitizeEmail(email),
                    First_name = firstName,
                    Last_name = lastName,
                    Role_assignments = roleAssignments,
                    Attributes = attributes,
                });
           // }
        }

        return Ok();
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
        // "Administrative Assistant" → "administrative-assistant"
        // "Administration / Operations" → "administration---operations"
    }

    [HttpGet("user-updated")]
    public IActionResult UserProfileUpdated()
    {
        var verificationValue = Request.Headers["x-okta-verification-challenge"].ToString();
        return Ok(new { verification = verificationValue });
    }

    [HttpGet("user-deleted")]
    public IActionResult UserProfileDeleted()
    {
        var verificationValue = Request.Headers["x-okta-verification-challenge"].ToString();
        return Ok(new { verification = verificationValue });
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
                    await _oktaUserRepository.UpdateSiteType(siteType, salesforceId);

                    _logger.LogInformation("SiteType {SiteType} set for user with SalesforceId {SalesforceId}", siteType, salesforceId);
                }
            }
        }
    }
}