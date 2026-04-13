using Chats.BE.Services;

namespace AzureFileMigrator;

public class DummyHostUrlService() : HostUrlService(null!)
{
    public override string GetBEUrl() => "http://localhost:5146";

    public override string GetFEUrl() => "http://localhost:3000";
}
