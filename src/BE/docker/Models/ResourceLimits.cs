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
        // 0 means "unlimited".
        if (MemoryBytes < 0)
            throw new ArgumentException("Memory limit must be >= 0");
        if (CpuCores < 0)
            throw new ArgumentException("CPU limit must be >= 0");
        if (MaxProcesses < 0)
            throw new ArgumentException("Process limit must be >= 0");

        if (maxLimits.MemoryBytes > 0 && MemoryBytes == 0)
            throw new ArgumentException("Memory unlimited exceeds maximum");
        if (maxLimits.CpuCores > 0 && CpuCores == 0)
            throw new ArgumentException("CPU unlimited exceeds maximum");
        if (maxLimits.MaxProcesses > 0 && MaxProcesses == 0)
            throw new ArgumentException("Process unlimited exceeds maximum");

        if (maxLimits.MemoryBytes > 0 && MemoryBytes > maxLimits.MemoryBytes)
            throw new ArgumentException($"Memory limit {MemoryBytes} exceeds maximum {maxLimits.MemoryBytes}");
        if (maxLimits.CpuCores > 0 && CpuCores > maxLimits.CpuCores)
            throw new ArgumentException($"CPU limit {CpuCores} exceeds maximum {maxLimits.CpuCores}");
        if (maxLimits.MaxProcesses > 0 && MaxProcesses > maxLimits.MaxProcesses)
            throw new ArgumentException($"Process limit {MaxProcesses} exceeds maximum {maxLimits.MaxProcesses}");
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
