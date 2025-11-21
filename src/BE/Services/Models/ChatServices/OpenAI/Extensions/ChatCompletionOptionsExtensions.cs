using OpenAI.Chat;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.Extensions;

public static class ChatCompletionOptionsExtensions
{
    public static void SetMaxTokens(this ChatCompletionOptions options, int value)
    {
        options.Patch.Set("$.max_tokens"u8, value);
    }

    public static void SetMaxTokens(this ChatCompletionOptions options, int value, bool useMaxCompletionTokens)
    {
        if (useMaxCompletionTokens)
        {
            // OpenAI/Azure OpenAI 使用 max_completion_tokens
            options.MaxOutputTokenCount = value;
        }
        else
        {
            // 其他提供商使用 max_tokens
            SetMaxTokens(options, value);
        }
    }
}
