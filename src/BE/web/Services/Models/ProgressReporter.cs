using ModelContextProtocol;

namespace Chats.Web.Services.Models;

public class ProgressReporter(Action<ProgressNotificationValue> reporter) : IProgress<ProgressNotificationValue>
{
    public void Report(ProgressNotificationValue value) => reporter(value);
}
