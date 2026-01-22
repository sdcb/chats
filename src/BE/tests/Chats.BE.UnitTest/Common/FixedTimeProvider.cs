namespace Chats.BE.UnitTest.Common;

internal class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow()
    {
        return _now;
    }

    public void SetTime(DateTimeOffset now)
    {
        _now = now;
    }
}
