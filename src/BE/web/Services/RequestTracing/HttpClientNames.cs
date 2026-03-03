namespace Chats.BE.Services.RequestTracing;

public static class HttpClientNames
{
    public const string ChatServiceOpenAI = "ChatService.OpenAI";
    public const string ChatServiceAnthropic = "ChatService.Anthropic";
    public const string ChatServiceGemini = "ChatService.Gemini";
    public const string ChatServiceResponseApi = "ChatService.ResponseApi";
    public const string ChatServiceImageGeneration = "ChatService.ImageGeneration";
    public const string ChatServiceImageDownload = "ChatService.ImageDownload";
    public const string SecurityKeycloakOAuth = "Security.KeycloakOAuth";
    public const string FileServiceUrlProvider = "FileService.UrlProvider";
    public const string AdminGitHubReleaseChecker = "Admin.GitHubReleaseChecker";
    public const string ProxyOldBE = "Proxy.OldBE";
    public const string McpController = "McpController.HttpTransport";
    public const string ChatControllerMcp = "ChatController.McpTransport";

    public static readonly string[] All =
    [
        ChatServiceOpenAI,
        ChatServiceAnthropic,
        ChatServiceGemini,
        ChatServiceResponseApi,
        ChatServiceImageGeneration,
        ChatServiceImageDownload,
        SecurityKeycloakOAuth,
        FileServiceUrlProvider,
        AdminGitHubReleaseChecker,
        ProxyOldBE,
        McpController,
        ChatControllerMcp,
    ];
}