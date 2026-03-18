namespace OktaInlineHookPermitIOIntegration.Helpers;

public static class ZipHelper
{
    public static string Normalize(string zipcode)
    {
        if (string.IsNullOrEmpty(zipcode)) return zipcode;
        return zipcode.Split('-')[0].Trim(); // "75062-2730" → "75062"
    }
}
