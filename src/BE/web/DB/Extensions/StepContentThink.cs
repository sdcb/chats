namespace Chats.Web.DB;

public partial class StepContentThink
{
    public StepContentThink Clone()
    {
        return new StepContentThink
        {
            Content = Content,
        };
    }
}
