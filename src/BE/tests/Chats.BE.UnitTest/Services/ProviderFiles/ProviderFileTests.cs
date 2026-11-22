using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices.Anthropic;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.ChatServices.OpenAI.Special;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.Services.ProviderFiles;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Chats.DB.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DBFile = Chats.DB.File;

namespace Chats.BE.UnitTest.Services.ProviderFiles;

public sealed class ProviderFileTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class ManualTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class StaticOptions(ProviderFileOptions value) : IOptionsMonitor<ProviderFileOptions>
    {
        public ProviderFileOptions CurrentValue => value;
        public ProviderFileOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<ProviderFileOptions, string?> listener) => null;
    }

    private sealed class FakeFileService : IFileService
    {
        public Task<string> Upload(FileUploadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Stream> Download(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult<Stream>(new MemoryStream([1, 2, 3]));
        public string CreateDownloadUrl(CreateDownloadUrlRequest request) => "https://example.com/file.png";
        public Task<bool> Delete(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class FakeFileServiceFactory : IFileServiceFactory
    {
        public IFileService Create(FileService dbfs) => new FakeFileService();
    }

    private sealed record CapturedRequest(HttpMethod Method, string Url, Dictionary<string, string> Headers, string? Body);

    private sealed class FakeHttp(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler, IHttpClientFactory
    {
        public List<CapturedRequest> Requests { get; } = [];

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Dictionary<string, string> headers = request.Headers.ToDictionary(h => h.Key.ToLowerInvariant(), h => string.Join(",", h.Value));
            string? body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            lock (Requests)
            {
                Requests.Add(new CapturedRequest(request.Method, request.RequestUri!.ToString(), headers, body));
            }
            return responder(request);
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string json) => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class Harness
    {
        public required ProviderFileService Service { get; init; }
        public required FakeHttp Http { get; init; }
        public required ManualTime Time { get; init; }
        public required ServiceProvider Provider { get; init; }

        public List<ProviderFileMapping> Mappings()
        {
            using IServiceScope scope = Provider.CreateScope();
            return [.. scope.ServiceProvider.GetRequiredService<ChatsDB>().ProviderFileMappings.AsNoTracking().OrderBy(x => x.Id)];
        }
    }

    private static Harness CreateHarness(Func<HttpRequestMessage, HttpResponseMessage>? responder = null, ProviderFileOptions? options = null)
    {
        int counter = 0;
        FakeHttp http = new(responder ?? (req => req.Method == HttpMethod.Delete
            ? Json(HttpStatusCode.OK, "{}")
            : Json(HttpStatusCode.OK, $$"""{"id":"file-{{Interlocked.Increment(ref counter)}}"}""")));
        ServiceCollection services = new();
        string dbName = Guid.NewGuid().ToString("N");
        services.AddDbContext<ChatsDB>(o => o.UseInMemoryDatabase(dbName));
        ServiceProvider sp = services.BuildServiceProvider();
        ManualTime time = new(T0);
        ProviderFileClientFactory clients = new([new OpenAIProviderFileClient(http), new AnthropicProviderFileClient(http)]);
        ProviderFileService service = new(
            sp.GetRequiredService<IServiceScopeFactory>(),
            new FakeFileServiceFactory(),
            clients,
            new StaticOptions(options ?? new ProviderFileOptions()),
            NullLogger<ProviderFileService>.Instance,
            time);
        return new Harness { Service = service, Http = http, Time = time, Provider = sp };
    }

    private static ModelKeySnapshot Key(DBModelProvider provider, string? host = null, short keyId = 7) => new()
    {
        Id = 70,
        ModelKeyId = keyId,
        Name = "k",
        Secret = "secret-value",
        Host = host,
        ModelProviderId = (short)provider,
    };

    private static DBFile PngFile(int id = 42) => new()
    {
        Id = id,
        FileName = "a.png",
        MediaType = "image/png",
        StorageKey = "k/a.png",
        Size = 3,
        FileServiceId = 1,
        FileService = new FileService { Id = 1, Name = "local", Configs = "{}" },
    };

    private static readonly ProviderFileTarget OpenAITarget = new(ProviderFileProtocols.OpenAI, ProviderFilePurposes.UserData);

    [Theory]
    [InlineData(DBModelProvider.OpenAI, DBApiType.OpenAIResponse, ProviderFileProtocols.OpenAI)]
    [InlineData(DBModelProvider.AzureAIFoundry, DBApiType.OpenAIResponse, ProviderFileProtocols.OpenAI)]
    [InlineData(DBModelProvider.DeepSeek, DBApiType.OpenAIResponse, ProviderFileProtocols.OpenAI)]
    [InlineData(DBModelProvider.DeepSeek, DBApiType.OpenAIChatCompletion, ProviderFileProtocols.OpenAI)]
    [InlineData(DBModelProvider.DeepSeek, DBApiType.AnthropicMessages, ProviderFileProtocols.Anthropic)]
    [InlineData(DBModelProvider.Anthropic, DBApiType.AnthropicMessages, ProviderFileProtocols.Anthropic)]
    [InlineData(DBModelProvider.OpenAI, DBApiType.OpenAIChatCompletion, null)]
    [InlineData(DBModelProvider.MiniMax, DBApiType.AnthropicMessages, null)]
    public void Capabilities_Resolve(DBModelProvider provider, DBApiType apiType, string? expectedProtocol)
    {
        Assert.Equal(expectedProtocol, ProviderFileCapabilities.Resolve(provider, apiType)?.Protocol);
    }

    [Fact]
    public async Task AzureFoundry_UsesAssistantsPurpose_WithoutExpiresAfter()
    {
        Assert.Equal(ProviderFilePurposes.Assistants, ProviderFileCapabilities.Resolve(DBModelProvider.AzureAIFoundry, DBApiType.OpenAIResponse)!.Purpose);
        Harness h = CreateHarness();
        ModelKeySnapshot key = Key(DBModelProvider.AzureAIFoundry, "https://x.services.ai.azure.com");

        await h.Service.GetOrUploadAsync(PngFile(), key, h.Service.ResolveTarget(key, DBApiType.OpenAIResponse)!, CancellationToken.None);

        string body = Assert.Single(h.Http.Requests).Body!;
        Assert.Contains("assistants", body);
        Assert.DoesNotContain("expires_after", body);
        Assert.Null(Assert.Single(h.Mappings()).ExpiresAt);
    }

    [Theory]
    [InlineData(DBModelProvider.AzureAIFoundry, "https://x.services.ai.azure.com/", "https://x.services.ai.azure.com/openai/v1/files")]
    [InlineData(DBModelProvider.AzureAIFoundry, "https://x.openai.azure.com/openai/v1", "https://x.openai.azure.com/openai/v1/files")]
    [InlineData(DBModelProvider.DeepSeek, null, "https://api.deepseek.com/v1/files")]
    [InlineData(DBModelProvider.DeepSeek, "https://api.deepseek.com/anthropic", "https://api.deepseek.com/files")]
    [InlineData(DBModelProvider.OpenAI, null, "https://api.openai.com/v1/files")]
    [InlineData(DBModelProvider.OpenAI, "https://relay.example.com", "https://relay.example.com/v1/files")]
    public void OpenAIClient_ResolveFilesUrl(DBModelProvider provider, string? host, string expected)
    {
        Assert.Equal(expected, OpenAIProviderFileClient.ResolveFilesUrl(Key(provider, host)));
    }

    [Theory]
    [InlineData(DBModelProvider.DeepSeek, null, "https://api.deepseek.com/anthropic/v1/files")]
    [InlineData(DBModelProvider.Anthropic, null, "https://api.anthropic.com/v1/files")]
    [InlineData(DBModelProvider.Anthropic, "https://relay.example.com/v1/", "https://relay.example.com/v1/files")]
    public void AnthropicClient_ResolveFilesUrl(DBModelProvider provider, string? host, string expected)
    {
        Assert.Equal(expected, AnthropicProviderFileClient.ResolveFilesUrl(Key(provider, host)));
    }

    [Fact]
    public void ParseUploadResponse_ReadsIdAndUnixExpiry()
    {
        ProviderUploadedFile f = ProviderFileClientBase.ParseUploadResponse("""{"id":"file-1","expires_at":1767225600}""");
        Assert.Equal("file-1", f.Id);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), f.ExpiresAt);
        Assert.Null(ProviderFileClientBase.ParseUploadResponse("""{"id":"file-2","expires_at":null}""").ExpiresAt);
    }

    [Fact]
    public async Task OpenAIClient_Upload_SendsBearerAndPurposeBeforeFile()
    {
        FakeHttp http = new(_ => Json(HttpStatusCode.OK, """{"id":"file-abc","expires_at":1767225600}"""));
        OpenAIProviderFileClient client = new(http);

        ProviderUploadedFile result = await client.UploadAsync(Key(DBModelProvider.DeepSeek),
            new ProviderFileUploadRequest([1, 2, 3], "a.png", "image/png", "user_data", TimeSpan.FromHours(2)), CancellationToken.None);

        Assert.Equal("file-abc", result.Id);
        CapturedRequest req = Assert.Single(http.Requests);
        Assert.Equal("https://api.deepseek.com/v1/files", req.Url);
        Assert.Equal("Bearer secret-value", req.Headers["authorization"]);
        string body = req.Body!;
        int purpose = body.IndexOf("name=purpose", StringComparison.Ordinal);
        int seconds = body.IndexOf("expires_after[seconds]", StringComparison.Ordinal);
        int file = body.IndexOf("name=file", StringComparison.Ordinal);
        Assert.True(purpose >= 0 && seconds > purpose && file > seconds, body);
        Assert.Contains("7200", body);
    }

    [Fact]
    public async Task AnthropicClient_UploadAndDelete_SendBetaHeader_And404DeleteIsSuccess()
    {
        FakeHttp http = new(req => req.Method == HttpMethod.Delete
            ? Json(HttpStatusCode.NotFound, "{}")
            : Json(HttpStatusCode.OK, """{"id":"file_01"}"""));
        AnthropicProviderFileClient client = new(http);
        ModelKeySnapshot key = Key(DBModelProvider.DeepSeek);

        ProviderUploadedFile result = await client.UploadAsync(key, new ProviderFileUploadRequest([1], "a.png", "image/png", "user_data", null), CancellationToken.None);
        await client.DeleteAsync(key, "file_01", CancellationToken.None);

        Assert.Equal("file_01", result.Id);
        Assert.All(http.Requests, r =>
        {
            Assert.Equal("secret-value", r.Headers["x-api-key"]);
            Assert.Equal(AnthropicProviderFileClient.FilesApiBeta, r.Headers["anthropic-beta"]);
        });
        Assert.Equal("https://api.deepseek.com/anthropic/v1/files/file_01", http.Requests[1].Url);
    }

    [Fact]
    public async Task GetOrUpload_ReusesActiveMapping_AndUpdatesLastUsedAt()
    {
        Harness h = CreateHarness();
        ModelKeySnapshot key = Key(DBModelProvider.DeepSeek);

        ProviderFileReference first = await h.Service.GetOrUploadAsync(PngFile(), key, OpenAITarget, CancellationToken.None);
        h.Time.Now = T0.AddHours(1);
        ProviderFileReference second = await h.Service.GetOrUploadAsync(PngFile(), key, OpenAITarget, CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Single(h.Http.Requests);
        ProviderFileMapping mapping = Assert.Single(h.Mappings());
        Assert.Equal(T0.AddHours(1).UtcDateTime, mapping.LastUsedAt);
        // No provider expiry returned: ExpiresAt is derived from the configured expires_after.
        Assert.Equal(T0.UtcDateTime.AddSeconds(new ProviderFileOptions().OpenAIExpiresAfterSeconds), mapping.ExpiresAt);
    }

    [Fact]
    public async Task GetOrUpload_ConcurrentCallers_UploadOnce()
    {
        Harness h = CreateHarness();
        ModelKeySnapshot key = Key(DBModelProvider.DeepSeek);

        ProviderFileReference[] refs = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => h.Service.GetOrUploadAsync(PngFile(), key, OpenAITarget, CancellationToken.None))));

        Assert.Single(refs.Select(x => x.RemoteFileId).Distinct());
        Assert.Single(h.Http.Requests);
    }

    [Fact]
    public async Task GetOrUpload_ReuploadsWhenWithinExpiryMargin()
    {
        Harness h = CreateHarness(options: new ProviderFileOptions { OpenAIExpiresAfterSeconds = 3600, ExpirySafetyMarginSeconds = 600 });
        ModelKeySnapshot key = Key(DBModelProvider.DeepSeek);

        ProviderFileReference first = await h.Service.GetOrUploadAsync(PngFile(), key, OpenAITarget, CancellationToken.None);
        h.Time.Now = T0.AddMinutes(51);
        ProviderFileReference second = await h.Service.GetOrUploadAsync(PngFile(), key, OpenAITarget, CancellationToken.None);

        Assert.NotEqual(first.RemoteFileId, second.RemoteFileId);
        List<ProviderFileMapping> mappings = h.Mappings();
        Assert.Equal([(byte)ProviderFileStatus.Expired, (byte)ProviderFileStatus.Active], mappings.Select(x => x.Status));
    }

    [Fact]
    public void IsUsable_RespectsStatusAndMargin()
    {
        DateTime now = T0.UtcDateTime;
        TimeSpan margin = TimeSpan.FromMinutes(10);
        Assert.True(ProviderFileService.IsUsable(new ProviderFileMapping { Status = 1, ExpiresAt = null }, now, margin));
        Assert.True(ProviderFileService.IsUsable(new ProviderFileMapping { Status = 1, ExpiresAt = now.AddMinutes(11) }, now, margin));
        Assert.False(ProviderFileService.IsUsable(new ProviderFileMapping { Status = 1, ExpiresAt = now.AddMinutes(9) }, now, margin));
        Assert.False(ProviderFileService.IsUsable(new ProviderFileMapping { Status = 3, ExpiresAt = null }, now, margin));
    }

    [Fact]
    public async Task TryGetOrUpload_UnsupportedEndpoint_ReturnsNullAndBacksOff()
    {
        Harness h = CreateHarness(_ => Json(HttpStatusCode.NotFound, """{"error":"not found"}"""));
        ModelKeySnapshot key = Key(DBModelProvider.Anthropic, "https://relay.example.com");
        ProviderFileTarget? target = h.Service.ResolveTarget(key, DBApiType.AnthropicMessages);
        Assert.NotNull(target);

        ProviderFileReference? reference = await h.Service.TryGetOrUploadAsync(PngFile(), key, target, CancellationToken.None);

        Assert.Null(reference);
        Assert.Empty(h.Mappings());
        Assert.Null(h.Service.ResolveTarget(key, DBApiType.AnthropicMessages));
        h.Time.Now = T0.AddHours(2);
        Assert.NotNull(h.Service.ResolveTarget(key, DBApiType.AnthropicMessages));
    }

    [Fact]
    public void ResolveTarget_HonoursConfigSwitches()
    {
        ModelKeySnapshot key = Key(DBModelProvider.DeepSeek);
        Assert.Null(CreateHarness(options: new ProviderFileOptions { Enabled = false }).Service.ResolveTarget(key, DBApiType.AnthropicMessages));
        Assert.Null(CreateHarness(options: new ProviderFileOptions { DisabledModelKeyIds = [7] }).Service.ResolveTarget(key, DBApiType.AnthropicMessages));
        Assert.Null(CreateHarness(options: new ProviderFileOptions { DisabledProviderIds = [(short)DBModelProvider.DeepSeek] }).Service.ResolveTarget(key, DBApiType.AnthropicMessages));
        Assert.NotNull(CreateHarness().Service.ResolveTarget(key, DBApiType.AnthropicMessages));
    }

    [Fact]
    public async Task ReleaseForFile_MarksDeletedAndDeletesRemote()
    {
        Harness h = CreateHarness();
        ModelKeySnapshot key = Key(DBModelProvider.DeepSeek);
        using (IServiceScope scope = h.Provider.CreateScope())
        {
            ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();
            db.ModelKeys.Add(new ModelKey { Id = key.ModelKeyId, CurrentSnapshotId = key.Id, CurrentSnapshot = key });
            await db.SaveChangesAsync();
        }
        await h.Service.GetOrUploadAsync(PngFile(), key, OpenAITarget, CancellationToken.None);

        await h.Service.ReleaseForFileAsync(42, CancellationToken.None);

        Assert.Equal((byte)ProviderFileStatus.Deleted, Assert.Single(h.Mappings()).Status);
        Assert.Contains(h.Http.Requests, r => r.Method == HttpMethod.Delete && r.Url == "https://api.deepseek.com/v1/files/file-1");
    }

    [Theory]
    [InlineData(400, """{"error":{"message":"File file-abc not found"}}""", true)]
    [InlineData(200, """{"type":"error","error":{"type":"invalid_request_error","message":"Invalid file_id"}}""", true)]
    [InlineData(404, """{"error":"model not found"}""", false)]
    [InlineData(500, """{"error":"file not found"}""", false)]
    [InlineData(400, """{"error":"context length exceeded"}""", false)]
    public void IsInvalidFileError_Detection(int status, string body, bool expected)
    {
        Assert.Equal(expected, ProviderFileRequestContext.IsInvalidFileError(new RawChatServiceException(status, body)));
    }

    [Fact]
    public void DecideRetry_ReuploadsThenFallsBack_OnlyBeforeStreaming()
    {
        RawChatServiceException ex = new(400, "file not found");
        Assert.Equal(ProviderFileRetryAction.ReuploadAndRetry, ProviderFileRequestContext.DecideRetry(ex, false, 1, 0, false));
        Assert.Equal(ProviderFileRetryAction.FallbackInline, ProviderFileRequestContext.DecideRetry(ex, false, 1, 1, false));
        Assert.Equal(ProviderFileRetryAction.None, ProviderFileRequestContext.DecideRetry(ex, false, 1, 2, false));
        Assert.Equal(ProviderFileRetryAction.None, ProviderFileRequestContext.DecideRetry(ex, true, 1, 0, false));
        Assert.Equal(ProviderFileRetryAction.None, ProviderFileRequestContext.DecideRetry(ex, false, 0, 0, false));
        Assert.Equal(ProviderFileRetryAction.None, ProviderFileRequestContext.DecideRetry(ex, false, 1, 0, true));
    }

    [Fact]
    public void ResponsesApi_RendersProviderFileIds()
    {
        MethodInfo method = typeof(ResponseApiService).GetMethod("ContentToInputParts", BindingFlags.Static | BindingFlags.NonPublic)!;
        JsonArray parts = (JsonArray)method.Invoke(null, [new List<NeutralContent>
        {
            NeutralProviderFileContent.Create("file-img", "image/png"),
            NeutralProviderFileContent.Create("file-doc", "application/pdf"),
        }])!;

        Assert.Equal("input_image", (string?)parts[0]!["type"]);
        Assert.Equal("file-img", (string?)parts[0]!["file_id"]);
        Assert.Equal("input_file", (string?)parts[1]!["type"]);
        Assert.Equal("file-doc", (string?)parts[1]!["file_id"]);
    }

    [Fact]
    public void Anthropic_RendersProviderFileSourceBlock()
    {
        MethodInfo method = typeof(AnthropicChatService).GetMethod("ConvertMessages", BindingFlags.Static | BindingFlags.NonPublic)!;
        IList<NeutralMessage> messages = [NeutralMessage.FromUser(NeutralProviderFileContent.Create("file_01", "image/png"), NeutralTextContent.Create("describe"))];

        JsonArray result = (JsonArray)method.Invoke(null, [messages, true, UsageSource.Api, false])!;

        JsonObject block = result[0]!["content"]![0]!.AsObject();
        Assert.Equal("image", (string?)block["type"]);
        Assert.Equal("file", (string?)block["source"]!["type"]);
        Assert.Equal("file_01", (string?)block["source"]!["file_id"]);
    }

    private sealed class ExposedDeepSeekChatService() : DeepSeekChatService(new FakeHttp(_ => Json(HttpStatusCode.OK, "{}")))
    {
        public JsonObject? Render(NeutralContent c) => ToOpenAIContentPart(c);
    }

    private sealed class ExposedChatCompletionService() : ChatCompletionService(new FakeHttp(_ => Json(HttpStatusCode.OK, "{}")))
    {
        public JsonObject? Render(NeutralContent c) => ToOpenAIContentPart(c);
    }

    [Fact]
    public void ChatCompletions_RendersProviderFileIds()
    {
        NeutralProviderFileContent content = NeutralProviderFileContent.Create("file-x", "image/png");
        Assert.Equal("""{"type":"file","file_id":"file-x"}""", new ExposedDeepSeekChatService().Render(content)!.ToJsonString());
        Assert.Equal("""{"type":"file","file":{"file_id":"file-x"}}""", new ExposedChatCompletionService().Render(content)!.ToJsonString());
    }

    private sealed class ScriptedChatService(Func<int, IAsyncEnumerable<ChatSegment>> script) : ChatService
    {
        public List<ChatRequest> Requests { get; } = [];

        protected override bool SupportsProviderFileReferences => true;

        public override IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return script(Requests.Count - 1);
        }
    }

#pragma warning disable CS1998, CS0162
    private static async IAsyncEnumerable<ChatSegment> Throw(Exception ex)
    {
        throw ex;
        yield break;
    }

    private static async IAsyncEnumerable<ChatSegment> Text(string text)
    {
        yield return ChatSegment.FromText(text);
    }

    private static async IAsyncEnumerable<ChatSegment> TextThenThrow(string text, Exception ex)
    {
        yield return ChatSegment.FromText(text);
        throw ex;
    }
#pragma warning restore CS1998, CS0162

    private static (ChatRequest Request, FileUrlProvider Fup) CreateChat(Harness h)
    {
        ModelKeySnapshot key = Key(DBModelProvider.DeepSeek);
        ModelSnapshot snapshot = new()
        {
            Name = "deepseek",
            DeploymentName = "deepseek-flash",
            ApiTypeId = (byte)DBApiType.OpenAIChatCompletion,
            AllowVision = true,
            SupportsVisionLink = true,
            ModelKeySnapshot = key,
        };
        Model model = new() { CurrentSnapshot = snapshot };
        snapshot.Model = model;
        ChatRequest request = new()
        {
            Messages = [NeutralMessage.FromUser(NeutralFileContent.Create(PngFile()), NeutralTextContent.Create("describe"))],
            ChatConfig = new ChatConfig { Model = model },
            Source = UsageSource.Api,
        };
        FileUrlProvider fup = new(
            h.Provider.GetRequiredService<ChatsDB>(),
            new FakeFileServiceFactory(),
            new NoOpUrlEncryptionService(),
            h.Http,
            h.Service);
        return (request, fup);
    }

    private static async Task<List<ChatSegment>> Drain(IAsyncEnumerable<ChatSegment> s)
    {
        List<ChatSegment> list = [];
        await foreach (ChatSegment seg in s) list.Add(seg);
        return list;
    }

    [Fact]
    public async Task ChatEntry_UsesProviderFileId()
    {
        Harness h = CreateHarness();
        (ChatRequest request, FileUrlProvider fup) = CreateChat(h);
        ScriptedChatService svc = new(_ => Text("ok"));

        await Drain(svc.ChatEntry(request, fup, CancellationToken.None));

        NeutralProviderFileContent part = Assert.IsType<NeutralProviderFileContent>(Assert.Single(svc.Requests).Messages[0].Contents[0]);
        Assert.Equal("file-1", part.RemoteFileId);
    }

    [Fact]
    public async Task ChatEntry_InvalidFileId_ReuploadsOnce_ThenFallsBackInline()
    {
        Harness h = CreateHarness();
        (ChatRequest request, FileUrlProvider fup) = CreateChat(h);
        ScriptedChatService svc = new(attempt => attempt < 2
            ? Throw(new RawChatServiceException(400, """{"error":{"message":"file not found"}}"""))
            : Text("ok"));

        List<ChatSegment> segments = await Drain(svc.ChatEntry(request, fup, CancellationToken.None));

        Assert.Single(segments);
        Assert.Equal(3, svc.Requests.Count);
        Assert.Equal("file-1", Assert.IsType<NeutralProviderFileContent>(svc.Requests[0].Messages[0].Contents[0]).RemoteFileId);
        Assert.Equal("file-2", Assert.IsType<NeutralProviderFileContent>(svc.Requests[1].Messages[0].Contents[0]).RemoteFileId);
        Assert.IsNotType<NeutralProviderFileContent>(svc.Requests[2].Messages[0].Contents[0]);
        Assert.All(h.Mappings(), m => Assert.Equal((byte)ProviderFileStatus.Invalid, m.Status));
    }

    [Fact]
    public async Task ChatEntry_DoesNotRetryAfterStreamingStarted()
    {
        Harness h = CreateHarness();
        (ChatRequest request, FileUrlProvider fup) = CreateChat(h);
        ScriptedChatService svc = new(_ => TextThenThrow("partial", new RawChatServiceException(200, "file not found")));

        await Assert.ThrowsAsync<RawChatServiceException>(() => Drain(svc.ChatEntry(request, fup, CancellationToken.None)));
        Assert.Single(svc.Requests);
    }

    [Fact]
    public async Task ChatEntry_UploadFailure_FallsBackInline()
    {
        Harness h = CreateHarness(_ => Json(HttpStatusCode.InternalServerError, "boom"));
        (ChatRequest request, FileUrlProvider fup) = CreateChat(h);
        ScriptedChatService svc = new(_ => Text("ok"));

        await Drain(svc.ChatEntry(request, fup, CancellationToken.None));

        Assert.IsNotType<NeutralProviderFileContent>(Assert.Single(svc.Requests).Messages[0].Contents[0]);
    }
}
