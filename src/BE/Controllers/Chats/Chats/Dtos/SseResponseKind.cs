namespace Chats.BE.Controllers.Chats.Chats.Dtos;

public enum SseResponseKind
{
    End = -1,
    StopId = 0,
    Segment = 1,
    Error = 2,
    UserMessage = 3,
    UpdateTitle = 4,
    TitleSegment = 5,
    ResponseMessage = 6,
    ChatLeafMessageId = 7,
    ReasoningSegment = 8,
    StartResponse = 9,
    StartReasoning = 10,
    ImageGenerating = 11, 
    ImageGenerated = 12,
}
