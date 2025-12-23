namespace Chats.DB;

/// <summary>
/// 具有 Order 属性的实体接口
/// </summary>
public interface IOrderable
{
    short Order { get; set; }
}

/// <summary>
/// 具有 UpdatedAt 属性的实体接口
/// </summary>
public interface IUpdatable
{
    DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Model 实体的部分类扩展，实现重排序接口
/// </summary>
public partial class Model : IOrderable, IUpdatable
{
    // Order 和 UpdatedAt 属性已在生成的部分类中定义
}

/// <summary>
/// ModelKey 实体的部分类扩展，实现重排序接口
/// </summary>
public partial class ModelKey : IOrderable, IUpdatable
{
    // Order 和 UpdatedAt 属性已在生成的部分类中定义
}

/// <summary>
/// ChatPreset 实体的部分类扩展，实现重排序接口
/// </summary>
public partial class ChatPreset : IOrderable, IUpdatable
{
    // Order 和 UpdatedAt 属性已在生成的部分类中定义
}