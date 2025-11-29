using Chats.BE.DB;
using Chats.BE.DB.Enums;

namespace Chats.BE.Services.Models.Neutral.Conversions;

/// <summary>
/// Conversion methods between Step (DB model) and NeutralMessage.
/// </summary>
public static class StepConversions
{
    /// <summary>
    /// Converts a Step to a NeutralMessage.
    /// </summary>
    public static NeutralMessage ToNeutral(this Step step)
    {
        return new NeutralMessage
        {
            Role = step.ChatRole.ToNeutral(),
            Contents = step.StepContents.Select(sc => sc.ToNeutral()).ToList()
        };
    }

    /// <summary>
    /// Converts a collection of Steps to a list of NeutralMessages.
    /// </summary>
    public static IList<NeutralMessage> ToNeutral(this IEnumerable<Step> steps)
    {
        return steps.Select(s => s.ToNeutral()).ToList();
    }

    /// <summary>
    /// Converts a DBChatRole to a NeutralChatRole.
    /// </summary>
    public static NeutralChatRole ToNeutral(this DBChatRole role)
    {
        return role switch
        {
            DBChatRole.User => NeutralChatRole.User,
            DBChatRole.Assistant => NeutralChatRole.Assistant,
            DBChatRole.ToolCall => NeutralChatRole.Tool,
            _ => throw new NotSupportedException($"DBChatRole {role} is not supported.")
        };
    }

    /// <summary>
    /// Converts a StepContent to a NeutralContent.
    /// </summary>
    public static NeutralContent ToNeutral(this StepContent stepContent)
    {
        return (DBStepContentType)stepContent.ContentTypeId switch
        {
            DBStepContentType.Text => NeutralTextContent.Create(stepContent.StepContentText!.Content),
            DBStepContentType.Error => NeutralErrorContent.Create(stepContent.StepContentText!.Content),
            DBStepContentType.FileUrl => NeutralFileUrlContent.Create(stepContent.StepContentText!.Content),
            DBStepContentType.FileBlob => NeutralFileBlobContent.Create(
                stepContent.StepContentBlob!.Content,
                stepContent.StepContentBlob.MediaType),
            DBStepContentType.Think => NeutralThinkContent.Create(
                stepContent.StepContentThink!.Content,
                stepContent.StepContentThink.Signature),
            DBStepContentType.ToolCall => NeutralToolCallContent.Create(
                stepContent.StepContentToolCall!.ToolCallId,
                stepContent.StepContentToolCall.Name,
                stepContent.StepContentToolCall.Parameters),
            DBStepContentType.ToolCallResponse => NeutralToolCallResponseContent.Create(
                stepContent.StepContentToolCallResponse!.ToolCallId,
                stepContent.StepContentToolCallResponse.Response,
                stepContent.StepContentToolCallResponse.IsSuccess,
                stepContent.StepContentToolCallResponse.DurationMs),
            DBStepContentType.FileId => NeutralFileContent.Create(
                stepContent.StepContentFile!.File!),
            _ => throw new NotSupportedException($"StepContent type {(DBStepContentType)stepContent.ContentTypeId} is not supported.")
        };
    }
}
