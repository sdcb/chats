namespace Chats.Web.Services;

public class AppConfigService(IConfiguration configuration)
{
    public string OldBEUrl => configuration["OLD_BE_URL"] ?? throw new InvalidOperationException("Environment variable 'OLD_BE_URL' not set.");

    public string FEUrl => configuration["FE_URL"] ?? throw new InvalidOperationException("Environment variable 'FE_URL' not set.");
}
