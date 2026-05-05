using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.DB;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models.ChatServices;

internal static class ModelRequestOverrides
{
    public static string GetBaseUrl(ModelKeySnapshot modelKey)
    {
        string? host = modelKey.Host;
        if (string.IsNullOrWhiteSpace(host))
        {
            host = ModelProviderInfo.GetInitialHost((DBModelProvider)modelKey.ModelProviderId);
        }

        return host?.TrimEnd('/') ?? string.Empty;
    }

    public static string GetBaseUrl(ModelSnapshot snapshot)
    {
        string baseUrl = GetBaseUrl(snapshot.ModelKeySnapshot);
        DBModelProvider provider = (DBModelProvider)snapshot.ModelKeySnapshot.ModelProviderId;
        DBApiType apiType = (DBApiType)snapshot.ApiTypeId;

        return NormalizeBaseUrl(baseUrl, provider, apiType);
    }

    public static string ResolveEndpoint(ModelSnapshot snapshot)
    {
        string baseUrl = GetBaseUrl(snapshot);
        if (string.IsNullOrWhiteSpace(snapshot.OverrideUrl))
        {
            return baseUrl;
        }

        string resolved = snapshot.OverrideUrl.Contains("{baseUrl}", StringComparison.Ordinal)
            ? snapshot.OverrideUrl.Replace("{baseUrl}", baseUrl, StringComparison.Ordinal)
            : snapshot.OverrideUrl;

        return resolved.TrimEnd('/');
    }

    private static string NormalizeBaseUrl(string baseUrl, DBModelProvider provider, DBApiType apiType)
    {
        if (provider != DBModelProvider.AzureAIFoundry || string.IsNullOrWhiteSpace(baseUrl))
        {
            return baseUrl;
        }

        string trimmed = baseUrl.TrimEnd('/');

        return apiType switch
        {
            DBApiType.OpenAIChatCompletion => NormalizeAzureChatCompletionBaseUrl(trimmed),
            DBApiType.OpenAIResponse => NormalizeAzureOpenAIBaseUrl(trimmed),
            DBApiType.OpenAIImageGeneration => NormalizeAzureOpenAIBaseUrl(trimmed),
            _ => trimmed,
        };
    }

    private static string NormalizeAzureChatCompletionBaseUrl(string baseUrl)
    {
        if (baseUrl.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            return baseUrl;
        }

        if (baseUrl.EndsWith("/openai", StringComparison.OrdinalIgnoreCase))
        {
            return baseUrl + "/v1";
        }

        return baseUrl + "/openai/v1";
    }

    private static string NormalizeAzureOpenAIBaseUrl(string baseUrl)
    {
        if (baseUrl.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            return baseUrl[..^3].TrimEnd('/');
        }

        if (baseUrl.EndsWith("/openai", StringComparison.OrdinalIgnoreCase))
        {
            return baseUrl;
        }

        return baseUrl + "/openai";
    }

    public static void ApplyBody(JsonObject body, ModelSnapshot snapshot)
    {
        JsonObject? mergedPatch = MergeCustomBodies(snapshot.ModelKeySnapshot.CustomBody, snapshot.CustomBody);
        if (mergedPatch is null)
        {
            return;
        }

        MergeInto(body, mergedPatch);
    }

    public static void ApplyMultipartBody(MultipartFormDataContent form, ModelSnapshot snapshot)
    {
        JsonObject? mergedPatch = MergeCustomBodies(snapshot.ModelKeySnapshot.CustomBody, snapshot.CustomBody);
        if (mergedPatch is null)
        {
            return;
        }

        foreach (KeyValuePair<string, JsonNode?> item in mergedPatch)
        {
            if (item.Value is null)
            {
                continue;
            }

            string value = item.Value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string? stringValue)
                ? stringValue ?? string.Empty
                : item.Value.ToJsonString();

            form.Add(new StringContent(value), item.Key);
        }
    }

    public static void ApplyHeaders(HttpRequestMessage request, ModelSnapshot snapshot)
    {
        IReadOnlyDictionary<string, string> mergedHeaders = MergeHeaders(snapshot.ModelKeySnapshot.CustomHeaders, snapshot.CustomHeaders);
        foreach (KeyValuePair<string, string> header in mergedHeaders)
        {
            request.Headers.Remove(header.Key);
            request.Content?.Headers.Remove(header.Key);

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }

    private static IReadOnlyDictionary<string, string> MergeHeaders(string? keyHeaders, string? modelHeaders)
    {
        Dictionary<string, string> merged = new(StringComparer.OrdinalIgnoreCase);

        foreach ((string name, string value) in ParseHeaders(keyHeaders))
        {
            merged[name] = value;
        }

        foreach ((string name, string value) in ParseHeaders(modelHeaders))
        {
            merged[name] = value;
        }

        return merged;
    }

    private static IEnumerable<(string Name, string Value)> ParseHeaders(string? rawHeaders)
    {
        if (string.IsNullOrWhiteSpace(rawHeaders))
        {
            yield break;
        }

        string[] lines = rawHeaders.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            int separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException($"Invalid custom header line: {line}");
            }

            string name = line[..separatorIndex].Trim();
            string value = line[(separatorIndex + 1)..].Trim();
            if (name.Length == 0)
            {
                throw new InvalidOperationException($"Invalid custom header line: {line}");
            }

            yield return (name, value);
        }
    }

    private static JsonObject? MergeCustomBodies(string? keyBody, string? modelBody)
    {
        JsonObject? merged = ParseJsonObject(keyBody);
        JsonObject? modelPatch = ParseJsonObject(modelBody);

        if (merged is null)
        {
            return modelPatch;
        }

        if (modelPatch is null)
        {
            return merged;
        }

        MergeInto(merged, modelPatch);
        return merged;
    }

    private static JsonObject? ParseJsonObject(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        JsonNode? parsed = JsonNode.Parse(rawJson);
        if (parsed is not JsonObject parsedObject)
        {
            throw new InvalidOperationException("CustomBody must be a JSON object.");
        }

        return (JsonObject)parsedObject.DeepClone();
    }

    private static void MergeInto(JsonObject target, JsonObject patch)
    {
        foreach (KeyValuePair<string, JsonNode?> property in patch)
        {
            if (property.Value is JsonObject patchObject && target[property.Key] is JsonObject targetObject)
            {
                MergeInto(targetObject, patchObject);
                continue;
            }

            target[property.Key] = property.Value?.DeepClone();
        }
    }
}