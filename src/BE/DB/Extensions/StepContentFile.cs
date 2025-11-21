namespace Chats.BE.DB;

public partial class StepContentFile
{
    public StepContentFile Clone()
    {
        return new StepContentFile
        {
            FileId = FileId,
            File = File,
        };
    }
}
