namespace Chats.BE.DB;

public partial class StepContentText
{
    public StepContentText Clone()
    {
        return new StepContentText
        {
            Content = Content,
        };
    }
}
