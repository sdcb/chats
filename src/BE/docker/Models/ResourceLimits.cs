namespace Chats.DockerInterface.Models;

/// <summary>
/// 容器资源限制
/// </summary>
public class ResourceLimits
{
    /// <summary>
    /// 内存限制（字节）。默认 512MB
    /// </summary>
    public long MemoryBytes { get; set; } = 512 * 1024 * 1024;

    /// <summary>
    /// CPU 限制（核心数，支持小数）。默认 1.0 核
    /// </summary>
    public double CpuCores { get; set; } = 1.0;

    /// <summary>
    /// 最大进程数。默认 100
    /// </summary>
    public long MaxProcesses { get; set; } = 100;

    /// <summary>
    /// 验证资源限制是否在允许范围内
    /// </summary>
    public void Validate(ResourceLimits maxLimits)
    {
        if (MemoryBytes > maxLimits.MemoryBytes)
            throw new ArgumentException($"Memory limit {MemoryBytes} exceeds maximum {maxLimits.MemoryBytes}");
        if (CpuCores > maxLimits.CpuCores)
            throw new ArgumentException($"CPU limit {CpuCores} exceeds maximum {maxLimits.CpuCores}");
        if (MaxProcesses > maxLimits.MaxProcesses)
            throw new ArgumentException($"Process limit {MaxProcesses} exceeds maximum {maxLimits.MaxProcesses}");
        if (MemoryBytes <= 0)
            throw new ArgumentException("Memory limit must be positive");
        if (CpuCores <= 0)
            throw new ArgumentException("CPU limit must be positive");
        if (MaxProcesses <= 0)
            throw new ArgumentException("Process limit must be positive");
    }

    /// <summary>
    /// 克隆资源限制
    /// </summary>
    public ResourceLimits Clone() => new()
    {
        MemoryBytes = MemoryBytes,
        CpuCores = CpuCores,
        MaxProcesses = MaxProcesses
    };

    /// <summary>
    /// 常用预设：最小配置（适合简单计算）
    /// </summary>
    public static ResourceLimits Minimal => new()
    {
        MemoryBytes = 128 * 1024 * 1024,  // 128MB
        CpuCores = 0.5,
        MaxProcesses = 50
    };

    /// <summary>
    /// 常用预设：标准配置
    /// </summary>
    public static ResourceLimits Standard => new()
    {
        MemoryBytes = 512 * 1024 * 1024,  // 512MB
        CpuCores = 1.0,
        MaxProcesses = 100
    };

    /// <summary>
    /// 常用预设：大型任务（适合数据处理）
    /// </summary>
    public static ResourceLimits Large => new()
    {
        MemoryBytes = 2L * 1024 * 1024 * 1024,  // 2GB
        CpuCores = 2.0,
        MaxProcesses = 200
    };
}
