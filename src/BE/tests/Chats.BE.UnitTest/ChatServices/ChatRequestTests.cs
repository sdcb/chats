using Chats.BE.Controllers.Chats.Chats;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.UnitTest.ChatServices;

public class ChatRequestTests
{
    [Fact]
    public void GetRequiredModel_WhenModelIsMissing_ThrowsConfigurationError()
    {
        ChatRequest request = new()
        {
            Messages = [NeutralMessage.FromUserText("test")],
            ChatConfig = new ChatConfig(),
            Source = UsageSource.Api,
        };

        CustomChatServiceException exception = Assert.Throws<CustomChatServiceException>(request.GetRequiredModel);

        Assert.Equal(DBFinishReason.InternalConfigIssue, exception.ErrorCode);
        Assert.Equal("Chat model is not configured.", exception.Message);
    }
}
