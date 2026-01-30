namespace Chats.DockerInterface.Models;

/// <summary>
/// 字节格式化工具类
/// </summary>
public static class BytesFormatter
{
    private const long KB = 1024;
    private const long MB = KB * 1024;
    private const long GB = MB * 1024;

    /// <summary>
    /// 将字节数格式化为人类可读的字符串（如 1.5MB, 2.3GB）
    /// </summary>
    public static string Format(long bytes)
    {
        return bytes switch
        {
            >= GB => $"{(double)bytes / GB:0.##}GB",
            >= MB => $"{(double)bytes / MB:0.##}MB",
            >= KB => $"{(double)bytes / KB:0.##}KB",
            _ => $"{bytes}B"
        };
    }
}
