namespace Chats.BE.Infrastructure.Functional;

public class ResultFailedException : Exception
{
    public ResultFailedException() : base()
    {
    }

    public ResultFailedException(string message) : base(message)
    {
    }
}
