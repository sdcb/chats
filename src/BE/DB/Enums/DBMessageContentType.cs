namespace Chats.BE.DB.Enums;

/// <summary>
/// Represents the content type of a database message.
/// </summary>
public enum DBMessageContentType : byte
{
    /// <summary>
    /// Error content type, stored in MessageContentText table
    /// </summary>
    Error = 0,

    /// <summary>
    /// Text content type, stored in MessageContentText table
    /// </summary>
    Text = 1,

    /// <summary>
    /// File ID content type, stored in MessageContentFile table
    /// </summary>
    FileId = 2,

    /// <summary>
    /// Reasoning content(think) type, stored in StepContentThink table
    /// </summary>
    Reasoning = 3,

    /// <summary>
    /// Tool call content type, stored in MessageContentToolCall table
    /// </summary>
    ToolCall = 4, 

    /// <summary>
    /// Tool call response content type, stored in MessageContentToolCallResponse table
    /// </summary>
    ToolCallResponse = 5, 
}
