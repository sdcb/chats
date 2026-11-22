using Chats.DB.Enums;

namespace Chats.BE.Services.ProviderFiles;

public static class ProviderFileProtocols
{
    public const string OpenAI = "openai";
    public const string Anthropic = "anthropic";
}

public static class ProviderFilePurposes
{
    public const string UserData = "user_data";

    /// <summary>Azure AI Foundry's Files API fails (HTTP 500) on user_data; assistants files work as Responses inputs.</summary>
    public const string Assistants = "assistants";
}

public enum ProviderFileStatus : byte
{
    Active = 1,
    Expired = 2,
    Invalid = 3,
    Deleted = 4,
}

public sealed record ProviderFileTarget(string Protocol, string Purpose);

public sealed record ProviderFileReference(long MappingId, string RemoteFileId);

public static class ProviderFileCapabilities
{
    public static readonly IReadOnlySet<string> SupportedMediaTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/webp",
    };

    /// <summary>
    /// Resolves whether the given provider + API type combination can reference uploaded files by id.
    /// Unknown combinations return null, which keeps the existing inline (base64/URL) behaviour.
    /// </summary>
    public static ProviderFileTarget? Resolve(DBModelProvider provider, DBApiType apiType)
    {
        return (apiType, provider) switch
        {
            (DBApiType.OpenAIResponse, DBModelProvider.AzureAIFoundry)
                => new ProviderFileTarget(ProviderFileProtocols.OpenAI, ProviderFilePurposes.Assistants),
            (DBApiType.OpenAIResponse, DBModelProvider.OpenAI or DBModelProvider.DeepSeek)
                => new ProviderFileTarget(ProviderFileProtocols.OpenAI, ProviderFilePurposes.UserData),
            (DBApiType.OpenAIChatCompletion, DBModelProvider.DeepSeek)
                => new ProviderFileTarget(ProviderFileProtocols.OpenAI, ProviderFilePurposes.UserData),
            (DBApiType.AnthropicMessages, DBModelProvider.Anthropic or DBModelProvider.DeepSeek)
                => new ProviderFileTarget(ProviderFileProtocols.Anthropic, ProviderFilePurposes.UserData),
            _ => null,
        };
    }

    public static bool IsSupportedMediaType(string? mediaType)
        => mediaType != null && SupportedMediaTypes.Contains(mediaType);

    /// <summary>
    /// Whether the OpenAI-compatible Files API accepts expires_after on upload (Azure AI Foundry rejects it with 400).
    /// </summary>
    public static bool SupportsExpiresAfter(DBModelProvider provider) => provider != DBModelProvider.AzureAIFoundry;
}
