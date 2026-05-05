using Chats.BE.Services.Models.ChatServices;
using Chats.DB;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace Chats.BE.UnitTest.ChatServices;

public class ModelRequestOverridesTests
{
    [Fact]
    public void ApplyHeaders_ModelShouldOverrideModelKey()
    {
        ModelSnapshot snapshot = CreateSnapshot(
            keyHeaders: "Authorization: Bearer key\nX-Trace-Id: key",
            modelHeaders: "Authorization: Bearer model\nX-Model: enabled");

        using HttpRequestMessage request = new(HttpMethod.Post, "https://example.com")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        ModelRequestOverrides.ApplyHeaders(request, snapshot);

        Assert.Equal("Bearer model", request.Headers.Authorization?.ToString());
        Assert.Equal("key", request.Headers.GetValues("X-Trace-Id").Single());
        Assert.Equal("enabled", request.Headers.GetValues("X-Model").Single());
    }

    [Fact]
    public void ApplyBody_ModelPatchShouldOverrideModelKeyPatch()
    {
        ModelSnapshot snapshot = CreateSnapshot(
            keyBody: "[{\"op\":\"replace\",\"path\":\"/temperature\",\"value\":0.5},{\"op\":\"add\",\"path\":\"/metadata/key\",\"value\":\"base\"}]",
            modelBody: "[{\"op\":\"replace\",\"path\":\"/temperature\",\"value\":0.7},{\"op\":\"add\",\"path\":\"/metadata/model\",\"value\":\"override\"}]");

        JsonObject body = new()
        {
            ["temperature"] = 0.3,
            ["metadata"] = new JsonObject
            {
                ["source"] = "original"
            }
        };

        ModelRequestOverrides.ApplyBody(body, snapshot);

        Assert.Equal(0.7, body["temperature"]!.GetValue<double>());
        Assert.Equal("original", body["metadata"]!["source"]!.GetValue<string>());
        Assert.Equal("base", body["metadata"]!["key"]!.GetValue<string>());
        Assert.Equal("override", body["metadata"]!["model"]!.GetValue<string>());
    }

    [Fact]
    public async Task ApplyMultipartBody_ShouldPatchTextFieldsAndPreserveFileParts()
    {
        ModelSnapshot snapshot = CreateSnapshot(
            keyBody: "[{\"op\":\"replace\",\"path\":\"/quality\",\"value\":\"medium\"}]",
            modelBody: "[{\"op\":\"add\",\"path\":\"/metadata\",\"value\":\"admin\"}]");

        using MultipartFormDataContent original = new();
        original.Add(new StringContent("low"), "quality");
        original.Add(new StringContent("1024x1024"), "size");
        original.Add(new ByteArrayContent([1, 2, 3]), "image", "image.png");

        using MultipartFormDataContent patched = ModelRequestOverrides.ApplyMultipartBody(original, snapshot);

        Dictionary<string, List<string>> textFields = [];
        int fileCount = 0;
        foreach (HttpContent content in patched)
        {
            string? name = content.Headers.ContentDisposition?.Name?.Trim('"');
            string? fileName = content.Headers.ContentDisposition?.FileName?.Trim('"');
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                fileCount++;
                continue;
            }

            if (name is null)
            {
                continue;
            }

            if (!textFields.TryGetValue(name, out List<string>? values))
            {
                values = [];
                textFields[name] = values;
            }

            values.Add(await content.ReadAsStringAsync());
        }

        Assert.Equal(1, fileCount);
        Assert.Equal(["medium"], textFields["quality"]);
        Assert.Equal(["1024x1024"], textFields["size"]);
        Assert.Equal(["admin"], textFields["metadata"]);
    }

    private static ModelSnapshot CreateSnapshot(string? keyHeaders = null, string? modelHeaders = null, string? keyBody = null, string? modelBody = null)
    {
        return new ModelSnapshot
        {
            ModelKeySnapshot = new ModelKeySnapshot
            {
                ModelProviderId = 1,
                Name = "key",
                CustomHeaders = keyHeaders,
                CustomBody = keyBody,
                Secret = "secret"
            },
            CustomHeaders = modelHeaders,
            CustomBody = modelBody
        };
    }
}