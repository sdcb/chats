namespace Chats.DockerInterface.Models;

/// <summary>
/// 输出配置
/// </summary>
public class OutputOptions
{
    /// <summary>
    /// 最大输出字节数。默认 64KB
    /// </summary>
    public int MaxOutputBytes { get; set; } = 64 * 1024;

    /// <summary>
    /// 截断策略。默认保留首尾
    /// </summary>
    public TruncationStrategy Strategy { get; set; } = TruncationStrategy.HeadAndTail;

    /// <summary>
    /// 截断提示信息
    /// </summary>
    public string TruncationMessage { get; set; } = "\n... [Output truncated: {0} bytes omitted] ...\n";
}
