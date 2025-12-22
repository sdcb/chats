namespace Chats.Web.DB;

public partial class StepContentBlob
{
    public StepContentBlob Clone()
    {
        return new StepContentBlob
        {
            Content = Content,
        };
    }
}
