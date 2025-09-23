using ModelContextProtocol;

namespace Chats.BE.Services.Models;

public class ProgressReporter(Action<ProgressNotificationValue> reporter) : IProgress<ProgressNotificationValue>
{
    public void Report(ProgressNotificationValue value) => reporter(value);
}
