namespace Chats.Web.Services.Common;

public static class DateTimeExtensions
{
    public static bool IsExpired(this DateTime dateTime) => dateTime < DateTime.UtcNow;
}
