namespace Chats.Web.DB;

public partial class StepContentToolCallResponse
{
    public StepContentToolCallResponse Clone()
    {
        return new StepContentToolCallResponse
        {
            ToolCallId = ToolCallId,
            Response = Response,
            DurationMs = DurationMs,
            IsSuccess = IsSuccess,
        };
    }
}
