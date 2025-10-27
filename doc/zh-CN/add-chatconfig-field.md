# ChatConfig 表增加字段指南

本文档详细说明在 ChatConfig 表中增加新字段时需要修改的所有位置,以确保功能完整性和数据一致性。

## 目录

- [概述](#概述)
- [数据库层](#数据库层)
- [后端修改](#后端修改)
  - [实体类](#实体类)
  - [DTO 层](#dto-层)
  - [控制器层](#控制器层)
  - [服务层](#服务层)
  - [哈希计算](#哈希计算)
- [前端修改](#前端修改)
  - [类型定义](#类型定义)
  - [UI 组件](#ui-组件)
  - [数据传递](#数据传递)
- [测试](#测试)
- [检查清单](#检查清单)
- [案例研究](#案例研究)

## 概述

ChatConfig 是系统中的核心配置表,存储聊天会话的各种配置参数。增加新字段需要从数据库到前端的全栈修改,本文档提供完整的修改指南。

### 字段类型说明

新增字段通常分为以下几类:
- **功能开关型**: 如 `WebSearchEnabled`、`CodeExecutionEnabled`(布尔值)
- **参数型**: 如 `Temperature`、`MaxOutputTokens`(数值或可空数值)
- **枚举型**: 如 `ReasoningEffort`、`ImageSizeId`(枚举值)
- **关联型**: 如 `ChatConfigMcps`(关联表)

## 数据库层

### 1. 数据库迁移

在 `src/BE/DB/ChatConfig.cs` 实体类中添加新字段:

```csharp
public partial class ChatConfig
{
    // ... 其他字段 ...
    
    /// <summary>
    /// 新字段说明
    /// </summary>
    public bool YourNewField { get; set; }
}
```

**注意事项**:
- 添加 XML 文档注释说明字段用途
- 考虑默认值(对于非空类型)
- 考虑向后兼容性

### 2. 生成数据库迁移脚本

```bash
cd src/BE
dotnet ef migrations add AddYourNewFieldToChatConfig
```

## 后端修改

### 实体类

#### 1. ChatConfig 扩展方法

**文件**: `src/BE/DB/Extensions/ChatConfig.cs`

##### Clone 方法
在 `Clone()` 方法中添加新字段的复制:

```csharp
public ChatConfig Clone()
{
    return new ChatConfig
    {
        // ... 其他字段 ...
        WebSearchEnabled = WebSearchEnabled,
        CodeExecutionEnabled = CodeExecutionEnabled,
        YourNewField = YourNewField,  // ← 添加这里
        // ... 其他字段 ...
    };
}
```

##### GenerateDBHashCode 方法

在 `GenerateDBHashCode()` 方法中添加新字段的哈希计算:

```csharp
// 4. WebSearchEnabled (bool)：用 1 字节表示
flagBuffer[0] = (byte)(WebSearchEnabled ? 1 : 0);
AppendField(flagBuffer);

// 4.5. YourNewField (bool): 仅当为true时才包含以保持向后兼容
if (YourNewField)
{
    flagBuffer[0] = 1;
    AppendField(flagBuffer);
}
```

**重要**: 
- 如果字段有默认值(如 `false`, `0`, `null`),建议仅在非默认值时包含,以保持向后兼容
- 确保哈希计算的顺序与现有字段保持一致
- 对于可选字段,先写入存在标志,再写入实际值

**哈希计算向后兼容性原则**:
```csharp
// ✅ 推荐：默认值不影响哈希(向后兼容)
if (YourNewField != defaultValue)
{
    // 写入字段数据
}

// ❌ 不推荐：总是包含字段(破坏向后兼容)
// 写入字段数据
```

### DTO 层

#### 1. AdminModelDto.cs

**文件**: `src/BE/Controllers/Admin/AdminModels/Dtos/AdminModelDto.cs`

如果新字段与模型能力相关(如某些模型支持,某些不支持),需要添加到 AdminModelDto:

```csharp
[JsonPropertyName("allowYourFeature")]
public bool AllowYourFeature { get; init; }
```

#### 2. ModelReference.cs

**文件**: `src/BE/DB/Enums/ModelReference.cs`

添加判断模型是否支持新功能的扩展方法:

```csharp
public static bool SupportsYourFeature(this DBModelReference modelReference)
{
    string modelReferenceName = modelReference.ToString();
    
    // 根据模型名称判断是否支持
    if (modelReferenceName.StartsWith("gemini-"))
    {
        // 排除不支持的特定模型
        return modelReferenceName switch
        {
            "gemini-exp-1114" => false,
            "gemini-exp-1121" => false,
            "gemini-2.0-flash-thinking-exp-01-21" => false,
            _ => true
        };
    }
    
    return false;
}
```

#### 3. 控制器返回 DTO

在所有返回模型信息的控制器中添加新字段:

**文件**: 
- `src/BE/Controllers/Admin/AdminModels/AdminModelsController.cs`
- `src/BE/Controllers/Common/Models/ModelsController.cs`
- `src/BE/Controllers/Admin/AdminModels/AdminUserModelController.cs`

```csharp
AllowYourFeature = m.ModelReference.SupportsYourFeature(),
```

#### 4. ChatSpanDto

**文件**: `src/BE/Controllers/Chats/UserChats/Dtos/ChatsResponse.cs`

在 `ChatSpanDto` 记录中添加新字段:

```csharp
public record ChatSpanDto
{
    // ... 其他字段 ...
    
    [JsonPropertyName("yourNewField")]
    public required bool YourNewField { get; init; }
    
    // ... 其他字段 ...
}
```

在所有 `FromDB` 静态方法中映射新字段:

```csharp
public static ChatSpanDto FromDB(ChatSpan span) => new()
{
    // ... 其他字段 ...
    WebSearchEnabled = span.ChatConfig.WebSearchEnabled,
    CodeExecutionEnabled = span.ChatConfig.CodeExecutionEnabled,
    YourNewField = span.ChatConfig.YourNewField,  // ← 添加这里
    // ... 其他字段 ...
};

public static ChatSpanDto FromDB(ChatPresetSpan span) => new()
{
    // ... 其他字段 ...
    WebSearchEnabled = span.ChatConfig.WebSearchEnabled,
    CodeExecutionEnabled = span.ChatConfig.CodeExecutionEnabled,
    YourNewField = span.ChatConfig.YourNewField,  // ← 添加这里
    // ... 其他字段 ...
};
```

#### 5. UpdateChatSpanRequest

**文件**: `src/BE/Controllers/Chats/Chats/Dtos/UpdateChatSpanRequest.cs`

添加请求字段:

```csharp
[JsonPropertyName("yourNewField")]
public bool YourNewField { get; init; }
```

在所有 `ApplyTo` 和 `ToDB` 方法中应用新字段:

```csharp
public void ApplyTo(ChatSpan span)
{
    // ... 其他字段 ...
    config.WebSearchEnabled = WebSearchEnabled;
    config.CodeExecutionEnabled = CodeExecutionEnabled;
    config.YourNewField = YourNewField;  // ← 添加这里
    // ... 其他字段 ...
}

public void ApplyTo(ChatPresetSpan span, Model model)
{
    // ... 其他字段 ...
    config.WebSearchEnabled = WebSearchEnabled;
    config.CodeExecutionEnabled = CodeExecutionEnabled;
    config.YourNewField = YourNewField;  // ← 添加这里
    // ... 其他字段 ...
}

public ChatPresetSpan ToDB(Model model, byte spanId)
{
    ChatConfig chatConfig = new ChatConfig()
    {
        // ... 其他字段 ...
        WebSearchEnabled = WebSearchEnabled,
        CodeExecutionEnabled = CodeExecutionEnabled,
        YourNewField = YourNewField,  // ← 添加这里
        // ... 其他字段 ...
    };
    // ... 其余代码 ...
}
```

#### 6. ChatPresetDto

**文件**: `src/BE/Controllers/Chats/ChatPresets/Dtos/ChatPresetDto.cs`

在 `FromDB` 方法中添加新字段:

```csharp
public static ChatPresetDto FromDB(ChatPreset preset, IUrlEncryptionService idEncryption)
{
    return new ChatPresetDto
    {
        // ... 其他字段 ...
        Spans = [.. preset.ChatPresetSpans.Select(x => new ChatSpanDto
        {
            // ... 其他字段 ...
            YourNewField = x.ChatConfig.YourNewField,  // ← 添加这里
        })]
    };
}
```

### 控制器层

需要在所有创建或查询 ChatConfig 的控制器中添加新字段。

#### 1. ChatSpanController.cs

**文件**: `src/BE/Controllers/Chats/Chats/ChatSpanController.cs`

在创建新 Span 的方法中初始化新字段:

```csharp
// CreateChatSpan 方法中
ChatConfig = new()
{
    // ... 其他字段 ...
    WebSearchEnabled = false,
    CodeExecutionEnabled = false,
    YourNewField = false,  // ← 添加这里(使用合适的默认值)
    // ... 其他字段 ...
};
```

#### 2. UserChatsController.cs

**文件**: `src/BE/Controllers/Chats/UserChats/UserChatsController.cs`

##### 创建新 Chat 时初始化

```csharp
ChatConfig = new ChatConfig
{
    // ... 其他字段 ...
    WebSearchEnabled = false,
    CodeExecutionEnabled = false,
    YourNewField = false,  // ← 添加这里
    // ... 其他字段 ...
}
```

##### 查询方法中映射字段

在所有手动创建 `ChatSpanDto` 的地方添加字段映射:

```csharp
Spans = x.ChatSpans.Select(span => new ChatSpanDto
{
    // ... 其他字段 ...
    WebSearchEnabled = span.ChatConfig.WebSearchEnabled,
    CodeExecutionEnabled = span.ChatConfig.CodeExecutionEnabled,
    YourNewField = span.ChatConfig.YourNewField,  // ← 添加这里
    // ... 其他字段 ...
}).ToArray(),
```

**需要修改的具体位置**:
- `GetChat` 方法
- `GetChats` 方法
- `GetChatsPaging` 方法

#### 3. ChatPresetController.cs

**文件**: `src/BE/Controllers/Chats/ChatPresets/ChatPresetController.cs`

##### 创建新 Preset Span

```csharp
ChatConfig = new ChatConfig
{
    // ... 其他字段 ...
    WebSearchEnabled = false,
    CodeExecutionEnabled = false,
    YourNewField = false,  // ← 添加这里
    // ... 其他字段 ...
}
```

##### 复制 Preset

```csharp
ChatConfig = new ChatConfig
{
    // ... 其他字段 ...
    WebSearchEnabled = x.ChatConfig.WebSearchEnabled,
    CodeExecutionEnabled = x.ChatConfig.CodeExecutionEnabled,
    YourNewField = x.ChatConfig.YourNewField,  // ← 添加这里
    // ... 其他字段 ...
}
```

##### 查询方法中映射字段

```csharp
Spans = x.ChatPresetSpans.Select(x => new ChatSpanDto()
{
    // ... 其他字段 ...
    WebSearchEnabled = x.ChatConfig.WebSearchEnabled,
    CodeExecutionEnabled = x.ChatConfig.CodeExecutionEnabled,
    YourNewField = x.ChatConfig.YourNewField,  // ← 添加这里
    // ... 其他字段 ...
}).ToArray()
```

#### 4. AdminMessageController.cs

**文件**: `src/BE/Controllers/Admin/AdminMessage/AdminMessageController.cs`

在所有查询方法中添加字段映射(共 2 处):

```csharp
Spans = x.ChatSpans.Select(span => new ChatSpanDto
{
    // ... 其他字段 ...
    WebSearchEnabled = span.ChatConfig.WebSearchEnabled,
    CodeExecutionEnabled = span.ChatConfig.CodeExecutionEnabled,
    YourNewField = span.ChatConfig.YourNewField,  // ← 添加这里
    // ... 其他字段 ...
}).ToArray(),
```

### 服务层

#### 1. ChatExtraDetails

**文件**: `src/BE/Services/ChatService/ChatExtraDetails.cs`

如果新字段需要在聊天生成时使用,添加到 ChatExtraDetails:

```csharp
public record ChatExtraDetails
{
    // ... 其他字段 ...
    public bool WebSearchEnabled { get; init; }
    public bool CodeExecutionEnabled { get; init; }
    public bool YourNewField { get; init; }  // ← 添加这里
}
```

#### 2. ChatService

**文件**: `src/BE/Services/ChatService/ChatService.cs`

##### 添加虚方法

```csharp
public virtual void SetYourFeature(bool enabled)
{
    // 默认实现为空，由子类按需重写
}
```

##### 在 FEPreprocess 中调用

```csharp
protected void FEPreprocess(/* ... */)
{
    // ... 其他代码 ...
    
    SetWebSearch(chatExtraDetails.WebSearchEnabled);
    SetCodeExecutionEnabled(chatExtraDetails.CodeExecutionEnabled);
    SetYourFeature(chatExtraDetails.YourNewField);  // ← 添加这里
}
```

#### 3. 特定服务实现

如果新字段是特定于某个 AI 提供商的,需要在对应的服务类中实现:

**文件**: `src/BE/Services/ChatService/GoogleAI2ChatService.cs` (示例)

```csharp
private bool _yourFeatureEnabled = false;

public override void SetYourFeature(bool enabled)
{
    _yourFeatureEnabled = enabled;
}

// 在 StreamChatCompletionAsync 中使用
protected override async Task StreamChatCompletionAsync(/* ... */)
{
    // ... 其他代码 ...
    
    if (_yourFeatureEnabled)
    {
        // 添加相应的功能逻辑
    }
}
```

#### 4. ChatController

**文件**: `src/BE/Controllers/Chats/Chats/ChatController.cs`

在创建 ChatExtraDetails 时传递新字段:

```csharp
new ChatExtraDetails(
    WebSearchEnabled = chatSpan.ChatConfig.WebSearchEnabled,
    CodeExecutionEnabled = chatSpan.ChatConfig.CodeExecutionEnabled,
    YourNewField = chatSpan.ChatConfig.YourNewField,  // ← 添加这里
)
```

### 哈希计算

**文件**: `src/BE.Tests/Services/ChatConfigHashTest.cs`

添加单元测试确保哈希计算正确:

```csharp
[Fact]
public void GenerateDBHashCode_ShouldGenerateDifferentHash_ForDifferentYourNewField()
{
    // Arrange
    ChatConfig config1 = new() { YourNewField = true };
    ChatConfig config2 = new() { YourNewField = false };

    // Act
    long hash1 = config1.GenerateDBHashCode();
    long hash2 = config2.GenerateDBHashCode();

    // Assert
    Assert.NotEqual(hash1, hash2);
}

[Fact]
public void GenerateDBHashCode_ShouldMaintainCompatibility_ForDefaultYourNewField()
{
    // Arrange - 测试向后兼容性：false(默认值)不应影响哈希
    ChatConfig configOld = new() 
    { 
        ModelId = 1,
        SystemPrompt = "Test"
        // YourNewField 未显式设置，默认为 false
    };
    
    ChatConfig configNew = new() 
    { 
        ModelId = 1,
        SystemPrompt = "Test",
        YourNewField = false // 显式设置为 false
    };

    // Act
    long hash1 = configOld.GenerateDBHashCode();
    long hash2 = configNew.GenerateDBHashCode();

    // Assert - false 值应该产生相同的哈希以保持向后兼容
    Assert.Equal(hash1, hash2);
}

[Fact]
public void GenerateDBHashCode_ShouldIncludeYourNewField_WhenTrue()
{
    // Arrange - 验证当 YourNewField 为 true 时确实影响哈希
    ChatConfig configWithoutFeature = new() 
    { 
        ModelId = 1,
        YourNewField = false
    };
    
    ChatConfig configWithFeature = new() 
    { 
        ModelId = 1,
        YourNewField = true
    };

    // Act
    long hash1 = configWithoutFeature.GenerateDBHashCode();
    long hash2 = configWithFeature.GenerateDBHashCode();

    // Assert - true 值应该产生不同的哈希
    Assert.NotEqual(hash1, hash2);
}
```

## 前端修改

### 类型定义

#### 1. adminApis.ts

**文件**: `src/FE/types/adminApis.ts`

在 `AdminModelDto` 接口中添加新字段:

```typescript
export interface AdminModelDto {
  // ... 其他字段 ...
  allowWebSearch: boolean;
  allowCodeExecution: boolean;
  allowYourFeature: boolean;  // ← 添加这里
}
```

#### 2. clientApis.ts

**文件**: `src/FE/types/clientApis.ts`

##### ChatSpanDto 接口

```typescript
export interface ChatSpanDto {
  // ... 其他字段 ...
  webSearchEnabled: boolean;
  codeExecutionEnabled: boolean;
  yourNewField: boolean;  // ← 添加这里
}
```

##### PutChatSpanParams 接口

```typescript
export interface PutChatSpanParams {
  // ... 其他字段 ...
  webSearchEnabled: boolean;
  codeExecutionEnabled: boolean;
  yourNewField: boolean;  // ← 添加这里
}
```

### UI 组件

#### 1. 创建专用图标组件(可选)

**文件**: `src/FE/components/Icons/IconYourFeature.tsx`

```typescript
import React from 'react';

export const IconYourFeature = ({ className }: { className?: string }) => {
  return (
    <svg
      className={className}
      xmlns="http://www.w3.org/2000/svg"
      width="24"
      height="24"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
    >
      {/* 你的图标 SVG 路径 */}
    </svg>
  );
};
```

导出图标:

**文件**: `src/FE/components/Icons/index.tsx`

```typescript
export { IconYourFeature } from './IconYourFeature';
```

#### 2. ChatModelSettingsModal

**文件**: `src/FE/components/Chat/ChatModelSettingsModal.tsx`

##### 状态管理

确保状态包含新字段:

```typescript
const [span, setSpan] = useState<ChatSpanDto>({
  // ... 其他字段 ...
  webSearchEnabled: false,
  codeExecutionEnabled: false,
  yourNewField: false,  // ← 添加这里
});
```

##### UI 渲染

添加功能切换组件:

```tsx
{model?.allowYourFeature && (
  <FeatureToggle
    icon={<IconYourFeature />}
    label={t('Your Feature')}
    enabled={span.yourNewField}
    onToggle={(enabled) =>
      setSpan({ ...span, yourNewField: enabled })
    }
  />
)}
```

##### 保存数据

在保存函数中包含新字段:

```typescript
await putChatSpan(selectedChat.id, span.spanId, {
  // ... 其他字段 ...
  webSearchEnabled: !!span.webSearchEnabled,
  codeExecutionEnabled: !!span.codeExecutionEnabled,
  yourNewField: !!span.yourNewField,  // ← 添加这里
});
```

#### 3. ChatPresetModal

**文件**: `src/FE/components/Chat/ChatPresetModal.tsx`

需要进行与 ChatModelSettingsModal 相同的修改:

##### 初始化新 Span

```typescript
const span: ChatSpanDto = {
  // ... 其他字段 ...
  webSearchEnabled: false,
  codeExecutionEnabled: false,
  yourNewField: false,  // ← 添加这里
};
```

##### 切换模型时重置

```typescript
setSelectedSpan({
  ...s,
  // ... 其他字段 ...
  webSearchEnabled: false,
  codeExecutionEnabled: false,
  yourNewField: false,  // ← 添加这里
});
```

##### UI 渲染

```tsx
{model?.allowYourFeature && (
  <FeatureToggle
    icon={<IconYourFeature />}
    label={t('Your Feature')}
    enabled={selectedSpan?.yourNewField || false}
    onToggle={(enabled) =>
      setSelectedSpan({ ...selectedSpan!, yourNewField: enabled })
    }
  />
)}
```

##### 保存数据

```typescript
await putChatPresetSpan(preset.id, selectedSpan.spanId, {
  // ... 其他字段 ...
  webSearchEnabled: !!selectedSpan.webSearchEnabled,
  codeExecutionEnabled: !!selectedSpan.codeExecutionEnabled,
  yourNewField: !!selectedSpan.yourNewField,  // ← 添加这里
});
```

#### 4. 翻译文件

**文件**: `src/FE/locales/zh-CN.json`

添加翻译:

```json
{
  "Your Feature": "你的功能名称"
}
```

**文件**: `src/FE/locales/en-US.json`

```json
{
  "Your Feature": "Your Feature Name"
}
```

### 数据传递

#### ChatHeader.tsx

**文件**: `src/FE/components/Chat/ChatHeader.tsx`

在所有创建或更新 span 对象的地方添加新字段:

##### handleAddChatModel

```typescript
spans: spans.map(span => ({
  // ... 其他字段 ...
  webSearchEnabled: span.webSearchEnabled,
  codeExecutionEnabled: span.codeExecutionEnabled,
  yourNewField: span.yourNewField,  // ← 添加这里
}))
```

##### handleUpdateChatModel

```typescript
return {
  ...s,
  // ... 其他字段 ...
  webSearchEnabled: data.webSearchEnabled,
  codeExecutionEnabled: data.codeExecutionEnabled,
  yourNewField: data.yourNewField,  // ← 添加这里
};
```

## 测试

### 后端单元测试

1. **哈希计算测试** (必需)
   - 文件: `src/BE.Tests/Services/ChatConfigHashTest.cs`
   - 测试不同值产生不同哈希
   - 测试默认值的向后兼容性
   - 测试非默认值影响哈希

2. **API 集成测试** (推荐)
   - 测试创建带有新字段的 ChatSpan
   - 测试更新新字段
   - 测试查询返回新字段

### 前端测试

1. **类型检查**
   ```bash
   cd src/FE
   npm run type-check
   ```

2. **手动测试检查项**
   - [ ] 创建新对话时新字段初始化正确
   - [ ] 添加新模型时新字段初始化正确
   - [ ] 切换模型时新字段状态正确
   - [ ] 保存配置后新字段值正确持久化
   - [ ] 从服务器加载数据时新字段正确显示
   - [ ] 预设功能中新字段正确工作

## 检查清单

使用此清单确保所有必要的修改都已完成:

### 数据库层
- [ ] `ChatConfig` 实体添加新字段
- [ ] 生成并应用数据库迁移

### 后端 - 实体扩展
- [ ] `ChatConfig.Clone()` 包含新字段
- [ ] `ChatConfig.GenerateDBHashCode()` 包含新字段
- [ ] 添加哈希计算单元测试(3个测试)

### 后端 - DTO
- [ ] `AdminModelDto` 添加能力字段(如适用)
- [ ] `ModelReference` 添加支持判断方法(如适用)
- [ ] 模型控制器返回能力字段(3个文件)
- [ ] `ChatSpanDto` 添加新字段
- [ ] `ChatSpanDto.FromDB` 映射新字段(2个方法)
- [ ] `UpdateChatSpanRequest` 添加新字段
- [ ] `UpdateChatSpanRequest.ApplyTo` 应用新字段(2个方法)
- [ ] `UpdateChatSpanRequest.ToDB` 包含新字段
- [ ] `ChatPresetDto.FromDB` 映射新字段

### 后端 - 控制器
- [ ] `ChatSpanController` 创建方法初始化新字段(2处)
- [ ] `UserChatsController` 创建方法初始化新字段
- [ ] `UserChatsController` 查询方法映射新字段(3处)
- [ ] `ChatPresetController` 创建方法初始化新字段
- [ ] `ChatPresetController` 复制方法包含新字段
- [ ] `ChatPresetController` 查询方法映射新字段
- [ ] `AdminMessageController` 查询方法映射新字段(2处)

### 后端 - 服务层(如适用)
- [ ] `ChatExtraDetails` 添加新字段
- [ ] `ChatService` 添加设置方法
- [ ] `ChatService.FEPreprocess` 调用设置方法
- [ ] 特定服务类实现功能逻辑
- [ ] `ChatController` 传递新字段到 `ChatExtraDetails`

### 前端 - 类型
- [ ] `adminApis.ts` 的 `AdminModelDto` 添加字段
- [ ] `clientApis.ts` 的 `ChatSpanDto` 添加字段
- [ ] `clientApis.ts` 的 `PutChatSpanParams` 添加字段

### 前端 - UI
- [ ] 创建图标组件(可选)
- [ ] 导出图标组件
- [ ] `ChatModelSettingsModal` 状态包含新字段
- [ ] `ChatModelSettingsModal` UI 渲染
- [ ] `ChatModelSettingsModal` 保存包含新字段
- [ ] `ChatPresetModal` 初始化包含新字段
- [ ] `ChatPresetModal` UI 渲染
- [ ] `ChatPresetModal` 保存包含新字段
- [ ] `ChatHeader` 添加模型时包含新字段
- [ ] `ChatHeader` 更新模型时包含新字段
- [ ] 添加中英文翻译

### 测试
- [ ] 后端单元测试通过
- [ ] 前端类型检查通过
- [ ] 手动功能测试通过

## 案例研究

### 案例: 添加 CodeExecutionEnabled 字段

这是一个完整的实际案例,展示了如何添加 `CodeExecutionEnabled` 布尔字段。

#### 需求
Google Gemini 的某些模型支持代码执行功能,需要添加一个开关来控制此功能。

#### 实现步骤

1. **数据库层**
   ```csharp
   // ChatConfig.cs
   public bool CodeExecutionEnabled { get; set; }
   ```

2. **能力检测**
   ```csharp
   // ModelReference.cs
   public static bool SupportsCodeExecution(this DBModelReference modelReference)
   {
       string modelReferenceName = modelReference.ToString();
       if (modelReferenceName.StartsWith("gemini-"))
       {
           return modelReferenceName switch
           {
               "gemini-exp-1114" => false,
               "gemini-exp-1121" => false,
               "gemini-2.0-flash-thinking-exp-01-21" => false,
               _ => true
           };
       }
       return false;
   }
   ```

3. **后端 DTO**
   - AdminModelDto: `allowCodeExecution`
   - ChatSpanDto: `codeExecutionEnabled`
   - UpdateChatSpanRequest: `codeExecutionEnabled`

4. **服务层实现**
   ```csharp
   // GoogleAI2ChatService.cs
   private bool _codeExecutionEnabled = false;

   public override void SetCodeExecutionEnabled(bool enabled)
   {
       _codeExecutionEnabled = enabled;
   }

   protected override async Task StreamChatCompletionAsync(...)
   {
       if (_codeExecutionEnabled)
       {
           request.Tools.Add(new CodeExecutionTool());
       }
   }
   ```

5. **前端 UI**
   - 创建 `IconCode` 组件(显示 `</>` 符号)
   - 使用通用 `FeatureToggle` 组件
   - 在设置和预设模态框中添加切换开关

6. **测试**
   - 添加 3 个哈希计算单元测试
   - 手动测试所有功能点

#### 关键点
- 向后兼容: `CodeExecutionEnabled` 为 `false` 时不影响哈希值
- 模型检测: 只有支持的 Gemini 模型才显示此选项
- UI 一致性: 使用通用组件保持界面一致

## 常见错误

### 1. 遗漏某个 FromDB 方法
**错误**: 只更新了一个 `FromDB` 方法,忘记了 `ChatSpanDto` 有两个静态方法。

**解决**: 使用 grep 搜索确保所有地方都已更新:
```bash
grep -r "FromDB.*ChatSpan" src/BE/
```

### 2. 前端类型不匹配
**错误**: 前端手动创建 span 对象时遗漏新字段,导致类型错误。

**解决**: 搜索所有创建 span 对象的位置:
```bash
grep -r "webSearchEnabled:" src/FE/
```

### 3. 哈希计算破坏兼容性
**错误**: 总是在哈希中包含新字段,导致旧数据的哈希值改变。

**解决**: 使用条件判断,只在非默认值时包含:
```csharp
if (YourNewField != defaultValue)
{
    AppendField(data);
}
```

### 4. 控制器初始化遗漏
**错误**: 在某些创建 ChatConfig 的地方忘记初始化新字段。

**解决**: 搜索所有 `new ChatConfig` 的位置:
```bash
grep -r "new ChatConfig" src/BE/
```

## 总结

添加 ChatConfig 字段是一个涉及全栈的系统性工作:

1. **数据库层**: 实体和迁移
2. **后端层**: DTO、控制器、服务、哈希计算
3. **前端层**: 类型、UI、数据传递
4. **测试层**: 单元测试和集成测试

关键原则:
- ✅ **完整性**: 确保所有层都正确更新
- ✅ **一致性**: 保持命名和处理方式一致
- ✅ **兼容性**: 注意向后兼容,特别是哈希计算
- ✅ **可测试性**: 编写单元测试确保正确性

使用本文档的检查清单可以确保不遗漏任何关键步骤。
