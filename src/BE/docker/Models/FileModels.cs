namespace Chats.DockerInterface.Models;

/// <summary>
/// 文件/目录条目
/// </summary>
public class FileEntry
{
    /// <summary>
    /// 路径
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// 名称
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// 是否为目录
    /// </summary>
    public bool IsDirectory { get; init; }

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// 最后修改时间
    /// </summary>
    public DateTimeOffset? LastModified { get; init; }
}
