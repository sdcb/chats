using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using System.Net;
using Chats.BE.Tests.ChatServices.Http;

namespace Chats.BE.Tests.ChatServices.OpenAI;

public class MimoChatServiceTest
{
    private const string TestDataPath = "ChatServices/OpenAI/FiddlerDump";

    private static IHttpClientFactory CreateMockHttpClientFactory(FiddlerHttpDumpParser.HttpDump dump)
    {
        var statusCode = (HttpStatusCode)dump.Response.StatusCode;
        return new FiddlerDumpHttpClientFactory(dump.Response.Chunks, statusCode, dump.Request.Body);
    }

    [Fact]
    public async Task NonStreaming_WithNullToolCalls_ShouldNotThrow()
    {
        // Arrange
        var filePath = Path.Combine(TestDataPath, "XiaomiMimo-NonStream.txt");
        var dump = FiddlerHttpDumpParser.ParseFile(filePath);
        var httpClientFactory = CreateMockHttpClientFactory(dump);
        
        var service = new MimoChatService(httpClientFactory);

        var modelKey = new ModelKey
        {
            Id = 1,
            Name = "TestKey",
            Secret = "test-api-key",
            ModelProviderId = (int)DBModelProvider.OpenAI,
        };

        var model = new Model
        {
            Id = 1,
            Name = "Test Model",
            DeploymentName = "mimo-v2-flash",
            ModelKeyId = 1,
            ModelKey = modelKey,
            AllowStreaming = true,
        };

        var chatConfig = new ChatConfig
        {
            Id = 1,
            ModelId = 1,
            Model = model,
        };

        var request = new ChatRequest
        {
            Messages = [NeutralMessage.FromUserText("hello")],
            ChatConfig = chatConfig,
            Source = UsageSource.Api,
            Streamed = false, // This triggers ChatNonStreaming
            EndUserId = "8"
        };

        // Act
        var segments = new List<ChatSegment>();
        await foreach (var segment in service.ChatStreamed(request, CancellationToken.None))
        {
            segments.Add(segment);
        }

        // Assert
        Assert.NotEmpty(segments);
        var textSegment = segments.OfType<TextChatSegment>().FirstOrDefault();
        Assert.NotNull(textSegment);
        Assert.Equal("Hello! How can I help you today?", textSegment.Text);
    }
}
