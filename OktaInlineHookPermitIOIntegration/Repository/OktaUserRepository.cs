using Dapper;
using Microsoft.Data.SqlClient;
using OktaInlineHookPermitIOIntegration.Entities;
using OktaInlineHookPermitIOIntegration.Helpers;

namespace OktaInlineHookPermitIOIntegration.Repository;

public interface IOktaUserRepository
{
    Task<OktaUserInfo> GetUserByEmailAsync(string email);
    Task<string?> GetSiteTypeBySalesforceIdAsync(string salesforceId);

    Task<bool> SetSiteType(string siteType,string salesforceId);
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
        const string sql = @"
        SELECT SiteType
        FROM dbo.OktaUserInfo
        WHERE SalesforceId = @SalesforceId";

        using var connection = new SqlConnection(_configHelper.GetMetisConnectionString());
        var result = await connection.QuerySingleOrDefaultAsync<string>(sql, new { SalesforceId = salesforceId });
        return result;
    }

    public async Task<OktaUserInfo> GetUserByEmailAsync(string email)
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

    public async Task<bool> SetSiteType(string siteType, string salesforceId)
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
}
