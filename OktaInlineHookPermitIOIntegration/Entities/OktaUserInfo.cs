using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OktaInlineHookPermitIOIntegration.Entities;

[Table("OktaUserInfo")]
public sealed class OktaUserInfo
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }


    public string? OktaId { get; set; }


    public string? Status { get; set; }

    public DateTime? CreatedOnUtc { get; set; }

    public DateTime? ActivatedOnUtc { get; set; }

    public DateTime? LastLoginUtc { get; set; }

    public DateTime? LastUpdatedUtc { get; set; }

    public DateTime? PasswordChangedOnUtc { get; set; }

    public string? OktaUsername { get; set; }


    public string? FirstName { get; set; }


    public string? LastName { get; set; }


    public string? Email { get; set; }


    public string? Title { get; set; }


    public string? Nickname { get; set; }


    public string? Organization { get; set; }

    public string? Department { get; set; }


    public string? SalesforceId { get; set; }


    public string? VhaUsername { get; set; }


    public string? UhcUsername { get; set; }

    public int? OrgId { get; set; }


    public string? Alliance { get; set; }

    public Guid? Guid { get; set; }

    public string? Role { get; set; }

    public string? ClinicalSpecialties { get; set; }

    public string? NonClinicalSpecialties { get; set; }

    public bool? IsFederated { get; set; }


    public string? MiddleName { get; set; }

    public string? Salutation { get; set; }


    public string? Credentials { get; set; }


    public string? PrimaryPhone { get; set; }


    public string? City { get; set; }


    public string? State { get; set; }


    public string? ZipCode { get; set; }


    public string? CountryCode { get; set; }


    public string? Street { get; set; }


    public string? RalProducts { get; set; }


    public string? Sg2OrgId { get; set; }


    public string? Sg2PersonOrgId { get; set; }


    public string? Sg2ContactId { get; set; }

    public string? SiteType { get; set; }
}