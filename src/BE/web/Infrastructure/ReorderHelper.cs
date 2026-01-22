using Chats.DB;

namespace Chats.BE.Infrastructure;

/// <summary>
/// 重排序配置选项
/// </summary>
public record ReorderOptions
{
    /// <summary>
    /// 重排序的步长数组，按优先级从高到低排列
    /// </summary>
    public required int[] ReorderSteps { get; init; }

    /// <summary>
    /// 重排序的起始值
    /// </summary>
    public required short ReorderStart { get; init; }

    /// <summary>
    /// 移动操作时使用的步长
    /// </summary>
    public required int MoveStep { get; init; }
}

/// <summary>
/// 重排序帮助类，提供通用的重排序算法
/// </summary>
public class ReorderHelper
{
    private readonly ReorderOptions _options;

    /// <summary>
    /// 默认的重排序帮助器实例
    /// </summary>
    public static readonly ReorderHelper Default = new(new ReorderOptions
    {
        ReorderSteps = [1000, 100, 10],
        ReorderStart = -30000,
        MoveStep = 1000
    });

    /// <summary>
    /// 获取移动步长
    /// </summary>
    public int MoveStep => _options.MoveStep;

    /// <summary>
    /// 获取重排序起始值
    /// </summary>
    public short ReorderStart => _options.ReorderStart;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="options">重排序配置选项</param>
    public ReorderHelper(ReorderOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        
        if (_options.ReorderSteps == null || _options.ReorderSteps.Length == 0)
        {
            throw new ArgumentException("ReorderSteps cannot be null or empty", nameof(options));
        }
    }

    /// <summary>
    /// 重新排序实体数组，优先使用预设步长和起始值
    /// </summary>
    /// <typeparam name="T">实体类型，必须有 Order 属性</typeparam>
    /// <param name="entities">要重新排序的实体数组</param>
    public void ReorderEntities<T>(T[] entities) 
        where T : IOrderable
    {
        if (entities.Length == 0) return;
        
        // 计算可用的总范围
        long totalRange = (long)short.MaxValue - short.MinValue + 1;
        
        // 确保有足够的空间，至少每个元素间隔1
        if (entities.Length >= totalRange)
        {
            throw new InvalidOperationException($"Too many entities ({entities.Length}). Maximum supported: {totalRange - 1}");
        }
        
        // 尝试不同的步长值，优先使用预设值
        int actualStep = 1;
        int actualStart = _options.ReorderStart;
        
        foreach (int stepSize in _options.ReorderSteps)
        {
            // 计算使用这个步长需要的最大范围
            long requiredRange = (long)stepSize * entities.Length;
            long maxPossibleEnd = _options.ReorderStart + requiredRange;
            
            // 检查是否在 short 范围内
            if (maxPossibleEnd <= short.MaxValue)
            {
                actualStep = stepSize;
                break;
            }
            
            // 如果从 ReorderStart 开始不行，尝试调整起始位置
            long adjustedStart = short.MaxValue - requiredRange;
            if (adjustedStart >= short.MinValue)
            {
                actualStep = stepSize;
                actualStart = (int)adjustedStart;
                break;
            }
        }
        
        // 如果所有预设步长都不行，使用平均分布
        if (actualStep == 1 && (actualStart + (long)entities.Length > short.MaxValue))
        {
            // 平均分布算法
            long stepSize = Math.Max(1, totalRange / (entities.Length + 1));
            long currentOrder = short.MinValue + stepSize;
            
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i].Order = (short)Math.Min(currentOrder, short.MaxValue);
                currentOrder += stepSize;
                
                if (currentOrder > short.MaxValue)
                {
                    currentOrder = short.MaxValue;
                }
            }
        }
        else
        {
            // 使用找到的步长和起始位置
            for (int i = 0; i < entities.Length; i++)
            {
                long newOrder = actualStart + (long)i * actualStep;
                entities[i].Order = (short)Math.Min(Math.Max(newOrder, short.MinValue), short.MaxValue);
            }
        }
    }

    /// <summary>
    /// 尝试在两个位置之间插入新的 Order 值
    /// </summary>
    /// <typeparam name="T">实体类型，必须有 Order 属性</typeparam>
    /// <param name="sourceEntity">要移动的实体</param>
    /// <param name="previousEntity">前一个实体（可为null）</param>
    /// <param name="nextEntity">后一个实体（可为null）</param>
    /// <returns>是否成功应用移动，false表示需要重新排序</returns>
    public bool TryApplyMove<T>(T sourceEntity, T? previousEntity, T? nextEntity) 
        where T : class, IOrderable
    {
        // 计算新的 Order 值
        int newOrder = 0;
        if (previousEntity != null && nextEntity != null)
        {
            // 在两个实体之间插入
            if (previousEntity.Order + 1 >= nextEntity.Order)
            {
                return false; // 没有足够的空间，需要重新排序
            }
            newOrder = (previousEntity.Order + nextEntity.Order) / 2;
        }
        else if (previousEntity != null)
        {
            // 插入到 previous 之后
            newOrder = previousEntity.Order + _options.MoveStep;
        }
        else if (nextEntity != null)
        {
            // 插入到 next 之前
            newOrder = nextEntity.Order - _options.MoveStep;
        }

        // 检查新的 Order 值是否在有效范围内
        if (newOrder > short.MaxValue || newOrder < short.MinValue)
        {
            return false;
        }

        sourceEntity.Order = (short)newOrder;
        return true;
    }

    /// <summary>
    /// 在指定范围内重新分布实体的 Order 值
    /// </summary>
    /// <typeparam name="T">实体类型，必须有 Order 属性和 UpdatedAt 属性</typeparam>
    /// <param name="entities">要重新分布的实体数组</param>
    /// <param name="minOrder">最小 Order 值</param>
    /// <param name="maxOrder">最大 Order 值</param>
    public void RedistributeInRange<T>(T[] entities, short minOrder, short maxOrder) 
        where T : IOrderable, IUpdatable
    {
        if (entities.Length == 0) return;
        
        // 计算在指定范围内的可用空间
        long availableSpace = maxOrder - minOrder + 1;
        
        // 确保有足够的空间分配给所有实体
        if (entities.Length >= availableSpace)
        {
            // 如果空间不足，只能平均分配，最小间隔为1
            for (int i = 0; i < entities.Length; i++)
            {
                long newOrder = minOrder + (long)i * availableSpace / entities.Length;
                entities[i].Order = (short)Math.Min(Math.Max(newOrder, minOrder), maxOrder);
                entities[i].UpdatedAt = DateTime.UtcNow;
            }
        }
        else
        {
            // 有足够空间，均匀分布
            long stepSize = Math.Max(1, availableSpace / (entities.Length + 1));
            long currentOrder = minOrder + stepSize;
            
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i].Order = (short)Math.Min(currentOrder, maxOrder);
                entities[i].UpdatedAt = DateTime.UtcNow;
                currentOrder += stepSize;
                
                // 确保不超出范围
                if (currentOrder > maxOrder)
                {
                    currentOrder = maxOrder;
                }
            }
        }
    }
}
