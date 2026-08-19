using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Chats.DB.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chats.BE.UnitTest.ChatServices;

public sealed class ChatServiceVisionFilterTests
{
    private sealed class TestChatService : ChatService
    {
        public ChatRequest? CapturedRequest { get; private set; }

        public override IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, CancellationToken cancellationToken)
        {
            CapturedRequest = request;
            return EmptySegments();
        }

        public Task<IList<NeutralMessage>> FilterAsync(bool supportsVisionLink, bool allowVision, IList<NeutralMessage> messages, FileUrlProvider fup, CancellationToken cancellationToken)
            => RewriteVisionMessages(supportsVisionLink, allowVision, messages, fup, cancellationToken);

        private static async IAsyncEnumerable<ChatSegment> EmptySegments()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    [Fact]
    public async Task ChatEntry_WebChatSystemPrompt_PreservesTemplateLikeText()
    {
        const string systemPrompt = "{{CURRENT_DATE}} {{CURRENT_TIME}} {{MODEL_NAME}} {{name}}";
        ModelSnapshot modelSnapshot = new()
        {
            Name = "Test model",
            ApiTypeId = (byte)DBApiType.OpenAIChatCompletion,
        };
        Model model = new()
        {
            CurrentSnapshot = modelSnapshot,
        };
        modelSnapshot.Model = model;
        ChatRequest request = new()
        {
            Messages = [NeutralMessage.FromUserText("Hello")],
            System = NeutralSystemMessage.FromText(systemPrompt),
            ChatConfig = new ChatConfig { Model = model },
            Source = UsageSource.WebChat,
        };
        TestChatService service = new();

        await foreach (ChatSegment _ in service.ChatEntry(request, null!, CancellationToken.None))
        {
        }

        Assert.Equal(systemPrompt, service.CapturedRequest?.GetEffectiveSystemPrompt());
    }

    private static FileUrlProvider CreateFileUrlProvider()
    {
        ServiceCollection services = new();
        services.AddDbContext<ChatsDB>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        ServiceProvider sp = services.BuildServiceProvider();

        IHttpContextAccessor accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        HostUrlService host = new(accessor);
        IFileServiceFactory fsf = new FileServiceFactory(host, new NoOpUrlEncryptionService());

        return new FileUrlProvider(
            sp.GetRequiredService<ChatsDB>(),
            fsf,
            new NoOpUrlEncryptionService(),
            new DefaultHttpClientFactory());
    }

    [Fact]
    public async Task FilterVision_ShouldKeepUserImageUrl_WhenVisionEnabled()
    {
        TestChatService service = new();
        FileUrlProvider fup = CreateFileUrlProvider();

        IList<NeutralMessage> messages = [NeutralMessage.FromUser(NeutralFileUrlContent.Create("https://example.com/image.png"))];

        NeutralMessage filtered = Assert.Single(await service.FilterAsync(true, true, messages, fup, CancellationToken.None));

        NeutralFileUrlContent file = Assert.Single(filtered.Contents.OfType<NeutralFileUrlContent>());
        Assert.Equal("https://example.com/image.png", file.Url);
    }

    [Fact]
    public async Task FilterVision_ShouldKeepOnlyViewImageAttachments_FromToolMessages()
    {
        TestChatService service = new();
        FileUrlProvider fup = CreateFileUrlProvider();

        IList<NeutralMessage> messages =
        [
            NeutralMessage.FromAssistant(
                NeutralToolCallContent.Create("call_1", "view_image", "{}"),
                NeutralToolCallContent.Create("call_2", "run_command", "{}")),
            NeutralMessage.FromTool(
                NeutralToolCallResponseContent.Create("call_1", string.Empty),
                NeutralFileUrlContent.Create("https://example.com/view.png"),
                NeutralToolCallResponseContent.Create("call_2", "artifact ready"),
                NeutralFileUrlContent.Create("https://example.com/other.png"))
        ];

        NeutralMessage filtered = (await service.FilterAsync(true, true, messages, fup, CancellationToken.None))[1];

        Assert.Equal(3, filtered.Contents.Count);
        Assert.IsType<NeutralToolCallResponseContent>(filtered.Contents[0]);
        Assert.IsType<NeutralFileUrlContent>(filtered.Contents[1]);
        Assert.IsType<NeutralToolCallResponseContent>(filtered.Contents[2]);
        Assert.DoesNotContain(filtered.Contents.OfType<NeutralFileUrlContent>(), x => x.Url == "https://example.com/other.png");
    }

    [Fact]
    public async Task FilterVision_ShouldDropUserFileLinks_WhenVisionDisabled()
    {
        TestChatService service = new();
        FileUrlProvider fup = CreateFileUrlProvider();

        IList<NeutralMessage> messages = [NeutralMessage.FromUser(NeutralFileUrlContent.Create("https://example.com/image.png"))];

        NeutralMessage filtered = Assert.Single(await service.FilterAsync(true, false, messages, fup, CancellationToken.None));

        Assert.Empty(filtered.Contents);
    }

    private sealed class DefaultHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
