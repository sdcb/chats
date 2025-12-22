namespace Chats.Web.Services.Models;

public static class DBFinishReasonExtensions
{
    /// <summary>
    /// Converts DBFinishReason to OpenAI-compatible finish reason text.
    /// </summary>
    /// <param name="finishReason">The DBFinishReason to convert.</param>
    /// <returns>OpenAI-compatible finish reason string, or "stop" as default.</returns>
    public static string ToOpenAIFinishReason(this DBFinishReason finishReason)
    {
        return finishReason switch
        {
            DBFinishReason.Stop or DBFinishReason.Success => "stop",
            DBFinishReason.Length => "length",
            DBFinishReason.ToolCalls => "tool_calls",
            DBFinishReason.ContentFilter => "content_filter",
            DBFinishReason.FunctionCall => "function_call",
            _ => "stop"
        };
    }
}
