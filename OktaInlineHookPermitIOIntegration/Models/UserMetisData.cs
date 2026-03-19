namespace OktaInlineHookPermitIOIntegration.Models;

internal sealed class UserMetisRow
{
    public string? User_GUID { get; set; }
    public string? Tenant { get; set; }
    public string? Email { get; set; }
    public string? Organization { get; set; }
    public string? Department_s__c { get; set; }
    public string? Role_s__c { get; set; }
    public string? ShippingPostalCode { get; set; }
}

public sealed class UserMetisData
{
    public Guid UserGuid { get; set; }
    public string Tenant { get; set; } = string.Empty;
    public string? Organization { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public List<string> ZipCodes { get; set; } = [];
}