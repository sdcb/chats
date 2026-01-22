namespace Chats.DB;

public partial class StepContentToolCall
{
    public StepContentToolCall Clone()
    {
        return new StepContentToolCall
        {
            ToolCallId = ToolCallId,
            Name = Name,
            Parameters = Parameters,
        };
    }
}
