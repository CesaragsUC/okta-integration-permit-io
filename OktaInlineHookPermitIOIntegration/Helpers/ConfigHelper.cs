namespace OktaInlineHookPermitIOIntegration.Helpers;

public interface IConfigHelper
{
    string GetMetisConnectionString();
}
public class ConfigHelper : IConfigHelper
{
    private IConfiguration _configuration;

    public ConfigHelper(IConfiguration configuration)
    {
        _configuration = configuration;

    }

    public string GetMetisConnectionString()
    {
        return _configuration.GetConnectionString("MetisConnection");
    }

}
