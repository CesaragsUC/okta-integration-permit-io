using Dapper;
using Microsoft.Data.SqlClient;
using OktaInlineHookPermitIOIntegration.Entities;
using OktaInlineHookPermitIOIntegration.Helpers;
using OktaInlineHookPermitIOIntegration.Models;

namespace OktaInlineHookPermitIOIntegration.Repository;

public interface IOktaUserRepository
{
    Task<OktaUserInfo> GetUserByEmailAsync(string email);
    Task<string?> GetSiteTypeBySalesforceIdAsync(string salesforceId);
    Task<UserMetisData> GetUserMetisData(string email);
    Task<bool> UpdateSiteType(string siteType, string salesforceId);
}

public class OktaUserRepository : IOktaUserRepository
{

    private readonly IConfigHelper _configHelper;
    public OktaUserRepository(IConfigHelper configHelper)
    {
        _configHelper = configHelper;
    }


    public async Task<string?> GetSiteTypeBySalesforceIdAsync(string salesforceId)
    {
        try
        {
            const string sql = @"
            SELECT SiteType
            FROM dbo.OktaUserInfo
            WHERE SalesforceId = @SalesforceId";

            using var connection = new SqlConnection(_configHelper.GetMetisConnectionString());
            var result = await connection.QuerySingleOrDefaultAsync<string>(sql, new { SalesforceId = salesforceId });
            return result;
        }
        catch (Exception ex)
        {

            throw;
        }

    }

    public async Task<OktaUserInfo> GetUserByEmailAsync(string email)
    {
        try
        {
            const string sql = @"
            SELECT TOP 1 *
            FROM dbo.OktaUserInfo
            WHERE Email = @Email";

            using var connection = new SqlConnection(_configHelper.GetMetisConnectionString());
            var result = await connection.QuerySingleOrDefaultAsync<OktaUserInfo>(sql, new { Email = email });

            if (result == null)
                throw new Exception($"User with email {email} not found.");

            return result;
        }
        catch (Exception ex)
        {

            throw;
        }

    }

    public async Task<bool> UpdateSiteType(string siteType, string salesforceId)
    {
        try
        {
            const string sql = @"
            UPDATE dbo.OktaUser
            SET SiteType = @SiteType
            WHERE SalesforceId = @SalesforceId
            AND SiteType IS NULL";

            using var connection = new SqlConnection(_configHelper.GetMetisConnectionString());
            var rowsAffected = await connection.ExecuteAsync(sql, new { SiteType = siteType, SalesforceId = salesforceId });

            return rowsAffected > 0;
        }
        catch (Exception ex)
        {

            throw;
        }
    }

    public async Task<UserMetisData?> GetUserMetisData(string email)
    {
        try
        {
            const string sql = @"
            SELECT DISTINCT
                a.ShippingPostalCode,
                up.[User_GUID],
                c.Email,
                c.Role_s__c,
                c.Department_s__c,
                c.Site_Type__c AS Tenant,
                a.Name AS Organization
            FROM Contact c
            JOIN User_Linked_Org ulo ON ulo.User_GUID = c.Guid__c 
            JOIN Account a ON a.Member_ID__c = ulo.Member_ID
            JOIN User_Product_Roles_Alliance up ON up.User_GUID = c.Guid__c 
            JOIN Product_Roles pr ON up.Role_ID = pr.Role_ID AND up.Product_ID = pr.Product_ID
            JOIN Products pro ON pro.Product_ID = up.Product_ID
            WHERE c.Email = @Email";

            using var connection = new SqlConnection(_configHelper.GetMetisConnectionString());
            var rows = await connection.QueryAsync<UserMetisRow>(sql, new { Email = email });

            var rowList = rows.ToList();
            if (rowList.Count == 0)
                return null;

            var first = rowList[0];

            return new UserMetisData
            {
                UserGuid = Guid.TryParse(first.User_GUID, out var guid) ? guid : Guid.Empty,
                Tenant = first.Tenant ?? string.Empty,
                Email = first.Email ?? string.Empty,
                Department = first.Department_s__c ?? string.Empty,
                Organization = first.Organization ?? string.Empty,
                Roles = rowList
                    .Where(r => !string.IsNullOrEmpty(r.Role_s__c))
                    .Select(r => r.Role_s__c!)
                    .Distinct()
                    .ToList(),
                ZipCodes = rowList
                    .Where(r => !string.IsNullOrEmpty(r.ShippingPostalCode))
                    .Select(r => r.ShippingPostalCode!)
                    .Distinct()
                    .ToList()
            };
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
