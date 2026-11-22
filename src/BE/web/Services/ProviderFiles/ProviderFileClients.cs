using Chats.BE.DB;
using Chats.BE.Services.Models.ChatServices;
using Chats.BE.Services.RequestTracing;
using Chats.DB;
using Chats.DB.Enums;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Chats.BE.Services.ProviderFiles;

public sealed record ProviderFileUploadRequest(byte[] Content, string FileName, string MediaType, string Purpose, TimeSpan? ExpiresAfter);

public sealed record ProviderUploadedFile(string Id, DateTime? ExpiresAt);

public sealed class ProviderFileApiException(int statusCode, string body)
    : Exception($"Provider Files API returned HTTP {statusCode}: {Truncate(body)}")
{
    public int StatusCode => statusCode;

    public string Body => body;

    /// <summary>Endpoint missing / method not allowed / not implemented: the provider (or relay) most likely has no Files API.</summary>
    public bool LooksUnsupported => statusCode is 404 or 405 or 501;

    private static string Truncate(string s) => s.Length <= 500 ? s : s[..500] + "...";
}

/// <summary>
/// Provider Files API client for one wire protocol (OpenAI-compatible or Anthropic-compatible).
/// </summary>
public interface IProviderFileClient
{
    string Protocol { get; }

    Task<ProviderUploadedFile> UploadAsync(ModelKeySnapshot modelKey, ProviderFileUploadRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(ModelKeySnapshot modelKey, string remoteFileId, CancellationToken cancellationToken);
}

public interface IProviderFileClientFactory
{
    IProviderFileClient? GetClient(string protocol);
}

public sealed class ProviderFileClientFactory(IEnumerable<IProviderFileClient> clients) : IProviderFileClientFactory
{
    private readonly Dictionary<string, IProviderFileClient> _clients = clients.ToDictionary(x => x.Protocol, StringComparer.OrdinalIgnoreCase);

    public IProviderFileClient? GetClient(string protocol) => _clients.GetValueOrDefault(protocol);
}

public abstract class ProviderFileClientBase(IHttpClientFactory httpClientFactory) : IProviderFileClient
{
    public abstract string Protocol { get; }

    protected abstract string GetFilesUrl(ModelKeySnapshot modelKey);

    protected abstract void AddAuthHeaders(HttpRequestMessage request, ModelKeySnapshot modelKey);

    protected abstract MultipartFormDataContent BuildUploadContent(ProviderFileUploadRequest request);

    public async Task<ProviderUploadedFile> UploadAsync(ModelKeySnapshot modelKey, ProviderFileUploadRequest request, CancellationToken cancellationToken)
    {
        using HttpRequestMessage httpRequest = new(HttpMethod.Post, GetFilesUrl(modelKey));
        httpRequest.Content = BuildUploadContent(request);
        ApplyHeaders(httpRequest, modelKey);

        using HttpClient client = httpClientFactory.CreateClient(HttpClientNames.ChatServiceProviderFiles);
        using HttpResponseMessage response = await client.SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new ProviderFileApiException((int)response.StatusCode, body);
        }

        return ParseUploadResponse(body);
    }

    public async Task DeleteAsync(ModelKeySnapshot modelKey, string remoteFileId, CancellationToken cancellationToken)
    {
        using HttpRequestMessage httpRequest = new(HttpMethod.Delete, GetFilesUrl(modelKey) + "/" + Uri.EscapeDataString(remoteFileId));
        ApplyHeaders(httpRequest, modelKey);

        using HttpClient client = httpClientFactory.CreateClient(HttpClientNames.ChatServiceProviderFiles);
        using HttpResponseMessage response = await client.SendAsync(httpRequest, cancellationToken);
        // 404 means the file is already gone, which is the desired end state.
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            throw new ProviderFileApiException((int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
    }

    private void ApplyHeaders(HttpRequestMessage httpRequest, ModelKeySnapshot modelKey)
    {
        // Key-level custom headers first so that auth/beta headers required by the Files API win.
        ModelRequestOverrides.ApplyHeaders(httpRequest, new ModelSnapshot { ModelKeySnapshot = modelKey });
        AddAuthHeaders(httpRequest, modelKey);
    }

    internal static ProviderUploadedFile ParseUploadResponse(string body)
    {
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        string id = root.TryGetProperty("id", out JsonElement idElement) && idElement.ValueKind == JsonValueKind.String
            ? idElement.GetString()!
            : throw new ProviderFileApiException(200, "Upload response does not contain a file id: " + body);

        DateTime? expiresAt = null;
        if (root.TryGetProperty("expires_at", out JsonElement exp))
        {
            if (exp.ValueKind == JsonValueKind.Number && exp.TryGetInt64(out long unix) && unix > 0)
            {
                expiresAt = DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            }
            else if (exp.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(exp.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset dto))
            {
                expiresAt = dto.UtcDateTime;
            }
        }

        return new ProviderUploadedFile(id, expiresAt);
    }

    protected static string ResolveHost(ModelKeySnapshot modelKey)
    {
        string? host = modelKey.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            host = ModelProviderInfo.GetInitialHost((DBModelProvider)modelKey.ModelProviderId);
        }
        return (host ?? string.Empty).Trim().TrimEnd('/');
    }

    protected static ByteArrayContent CreateFileContent(ProviderFileUploadRequest request)
    {
        ByteArrayContent file = new(request.Content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(string.IsNullOrWhiteSpace(request.MediaType) ? "application/octet-stream" : request.MediaType);
        return file;
    }

    protected static string SafeFileName(string fileName)
    {
        string name = Path.GetFileName(fileName ?? string.Empty);
        return string.IsNullOrWhiteSpace(name) ? "file" : name;
    }
}

/// <summary>
/// OpenAI-compatible Files API: POST {base}/files (multipart: purpose, [expires_after], file), DELETE {base}/files/{id}.
/// Used by OpenAI, Azure AI Foundry (/openai/v1/files) and DeepSeek (OpenAI-compatible).
/// </summary>
public sealed class OpenAIProviderFileClient(IHttpClientFactory httpClientFactory) : ProviderFileClientBase(httpClientFactory)
{
    public override string Protocol => ProviderFileProtocols.OpenAI;

    protected override string GetFilesUrl(ModelKeySnapshot modelKey) => ResolveFilesUrl(modelKey);

    internal static string ResolveFilesUrl(ModelKeySnapshot modelKey)
    {
        string host = ResolveHost(modelKey);
        DBModelProvider provider = (DBModelProvider)modelKey.ModelProviderId;

        if (provider == DBModelProvider.AzureAIFoundry)
        {
            if (host.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase)) return host + "/files";
            if (host.EndsWith("/openai", StringComparison.OrdinalIgnoreCase)) return host + "/v1/files";
            return host + "/openai/v1/files";
        }

        if (provider == DBModelProvider.DeepSeek)
        {
            if (host.EndsWith("/anthropic", StringComparison.OrdinalIgnoreCase))
            {
                host = host[..^"/anthropic".Length];
            }
            return host + "/files";
        }

        return host.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? host + "/files" : host + "/v1/files";
    }

    protected override void AddAuthHeaders(HttpRequestMessage request, ModelKeySnapshot modelKey)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", modelKey.Secret);
    }

    protected override MultipartFormDataContent BuildUploadContent(ProviderFileUploadRequest request)
    {
        // DeepSeek requires purpose before file.
        MultipartFormDataContent form = new()
        {
            { new StringContent(request.Purpose), "purpose" }
        };
        if (request.ExpiresAfter is TimeSpan ttl && ttl > TimeSpan.Zero)
        {
            form.Add(new StringContent("created_at"), "expires_after[anchor]");
            form.Add(new StringContent(((long)ttl.TotalSeconds).ToString(CultureInfo.InvariantCulture)), "expires_after[seconds]");
        }
        form.Add(CreateFileContent(request), "file", SafeFileName(request.FileName));
        return form;
    }
}

/// <summary>
/// Anthropic-compatible Files API: POST {base}/v1/files with header anthropic-beta: files-api-2025-04-14.
/// Used by Anthropic and DeepSeek (https://api.deepseek.com/anthropic).
/// </summary>
public sealed class AnthropicProviderFileClient(IHttpClientFactory httpClientFactory) : ProviderFileClientBase(httpClientFactory)
{
    public const string FilesApiBeta = "files-api-2025-04-14";

    public override string Protocol => ProviderFileProtocols.Anthropic;

    protected override string GetFilesUrl(ModelKeySnapshot modelKey) => ResolveFilesUrl(modelKey);

    internal static string ResolveFilesUrl(ModelKeySnapshot modelKey)
    {
        DBModelProvider provider = (DBModelProvider)modelKey.ModelProviderId;
        string host = string.IsNullOrWhiteSpace(modelKey.Host)
            ? provider == DBModelProvider.DeepSeek ? "https://api.deepseek.com/anthropic" : "https://api.anthropic.com"
            : modelKey.Host.Trim().TrimEnd('/');

        if (host.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            host = host[..^3];
        }
        if (host.EndsWith(".ai.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            host += "/anthropic";
        }
        return host + "/v1/files";
    }

    protected override void AddAuthHeaders(HttpRequestMessage request, ModelKeySnapshot modelKey)
    {
        request.Headers.Remove("x-api-key");
        request.Headers.TryAddWithoutValidation("x-api-key", modelKey.Secret);
        if (!request.Headers.Contains("anthropic-version"))
        {
            request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        }
        AddFilesBetaHeader(request);
    }

    public static void AddFilesBetaHeader(HttpRequestMessage request)
    {
        if (request.Headers.TryGetValues("anthropic-beta", out IEnumerable<string>? values)
            && values.Any(v => v.Contains(FilesApiBeta, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        request.Headers.TryAddWithoutValidation("anthropic-beta", FilesApiBeta);
    }

    protected override MultipartFormDataContent BuildUploadContent(ProviderFileUploadRequest request)
    {
        MultipartFormDataContent form = new()
        {
            { CreateFileContent(request), "file", SafeFileName(request.FileName) }
        };
        return form;
    }
}
