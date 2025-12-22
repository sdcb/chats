using Chats.Web.Infrastructure;

namespace Chats.Web.DB;

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