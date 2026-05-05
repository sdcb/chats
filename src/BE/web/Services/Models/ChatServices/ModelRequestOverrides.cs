using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.DB;
using Json.Patch;
using System.Net.Http.Headers;
using System.Text.Json;
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
        JsonPatch? mergedPatch = MergeCustomBodies(snapshot.ModelKeySnapshot.CustomBody, snapshot.CustomBody);
        if (mergedPatch is null)
        {
            return;
        }

        ApplyJsonPatch(body, mergedPatch);
    }

    public static MultipartFormDataContent ApplyMultipartBody(MultipartFormDataContent form, ModelSnapshot snapshot)
    {
        JsonPatch? mergedPatch = MergeCustomBodies(snapshot.ModelKeySnapshot.CustomBody, snapshot.CustomBody);
        if (mergedPatch is null)
        {
            return form;
        }

        JsonObject textFields = ExtractMultipartTextFields(form);
        ApplyJsonPatch(textFields, mergedPatch);

        MultipartFormDataContent patchedForm = RebuildMultipartForm(form, textFields);
        return patchedForm;
    }

    public static void ApplyHeaders(HttpRequestMessage request, ModelSnapshot snapshot)
    {
        IReadOnlyDictionary<string, string> mergedHeaders = MergeHeaders(snapshot.ModelKeySnapshot.CustomHeaders, snapshot.CustomHeaders);
        foreach (KeyValuePair<string, string> header in mergedHeaders)
        {
            TryRemoveHeader(request.Headers, header.Key);
            if (request.Content is not null)
            {
                TryRemoveHeader(request.Content.Headers, header.Key);
            }

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value))
            {
                request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
    }

    private static void TryRemoveHeader(HttpHeaders headers, string headerName)
    {
        try
        {
            headers.Remove(headerName);
        }
        catch (InvalidOperationException)
        {
            // Header belongs to a different header collection type.
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

    private static JsonPatch? MergeCustomBodies(string? keyBody, string? modelBody)
    {
        JsonPatch? keyPatch = ParseJsonPatch(keyBody);
        JsonPatch? modelPatch = ParseJsonPatch(modelBody);

        if (keyPatch is null)
        {
            return modelPatch;
        }

        if (modelPatch is null)
        {
            return keyPatch;
        }

        JsonNode? mergedPatchNode = JsonSerializer.SerializeToNode(keyPatch, JSON.JsonSerializerOptions)?.DeepClone();
        JsonArray mergedPatchArray = mergedPatchNode as JsonArray ?? [];

        JsonArray? modelPatchArray = JsonSerializer.SerializeToNode(modelPatch, JSON.JsonSerializerOptions) as JsonArray;
        if (modelPatchArray is not null)
        {
            foreach (JsonNode? operation in modelPatchArray)
            {
                if (operation is not null)
                {
                    mergedPatchArray.Add(operation.DeepClone());
                }
            }
        }

        return JsonSerializer.Deserialize<JsonPatch>(mergedPatchArray.ToJsonString(JSON.JsonSerializerOptions), JSON.JsonSerializerOptions);
    }

    private static JsonPatch? ParseJsonPatch(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return null;
        }

        JsonPatch? patch = JsonSerializer.Deserialize<JsonPatch>(rawJson, JSON.JsonSerializerOptions);
        if (patch is null)
        {
            throw new InvalidOperationException("CustomBody must be a valid RFC 6902 JSON Patch array.");
        }

        return patch;
    }

    private static void ApplyJsonPatch(JsonObject target, JsonPatch patch)
    {
        PatchResult result = patch.Apply(target);
        if (!result.IsSuccess)
        {
            string errorMessage = result.Error?.ToString() ?? "Unknown JSON Patch error.";
            throw new InvalidOperationException($"Failed to apply custom body JSON Patch: {errorMessage}");
        }

        if (result.Result is JsonObject patchedObject)
        {
            target.Clear();
            foreach (KeyValuePair<string, JsonNode?> property in patchedObject)
            {
                target[property.Key] = property.Value?.DeepClone();
            }
            return;
        }

        throw new InvalidOperationException("Custom body JSON Patch must produce a JSON object.");
    }

    private static JsonObject ExtractMultipartTextFields(MultipartFormDataContent form)
    {
        JsonObject fields = [];
        foreach (HttpContent content in form)
        {
            if (content is not StringContent)
            {
                continue;
            }

            string? name = content.Headers.ContentDisposition?.Name?.Trim('"');
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            string value = content.ReadAsStringAsync().GetAwaiter().GetResult();
            fields[name] = value;
        }

        return fields;
    }

    private static MultipartFormDataContent RebuildMultipartForm(MultipartFormDataContent originalForm, JsonObject textFields)
    {
        MultipartFormDataContent patchedForm = new();

        foreach (HttpContent content in originalForm)
        {
            if (content is StringContent)
            {
                continue;
            }

            string? name = content.Headers.ContentDisposition?.Name?.Trim('"');
            string? fileName = content.Headers.ContentDisposition?.FileName?.Trim('"');

            if (string.IsNullOrWhiteSpace(name))
            {
                patchedForm.Add(content);
                continue;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                patchedForm.Add(content, name);
                continue;
            }

            patchedForm.Add(content, name, fileName);
        }

        foreach (KeyValuePair<string, JsonNode?> item in textFields)
        {
            if (item.Value is null)
            {
                continue;
            }

            string value = item.Value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out string? stringValue)
                ? stringValue ?? string.Empty
                : item.Value.ToJsonString();

            patchedForm.Add(new StringContent(value), item.Key);
        }

        return patchedForm;
    }
}