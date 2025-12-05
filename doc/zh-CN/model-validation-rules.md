# Model 验证规则文档

## 概述

Model 表单的验证规则已经在前端和后端都实现，确保数据完整性和一致性。

## 前端验证 (ModelModal.tsx)

### 基础字段验证

- **模型显示名称 (name)**: 必填，1-100个字符
- **部署名称 (deploymentName)**: 必填，1-100个字符
- **模型密钥 (modelKeyId)**: 必填，必须大于0
- **输入价格 (inputPrice1M)**: 必填，非负数
- **输出价格 (outputPrice1M)**: 必填，非负数

### ChatCompletion/Response API (apiType=0/1) 验证

1. **温度范围验证**
   - 最小温度: 0-2 之间
   - 最大温度: 0-2 之间
   - 最小温度必须 ≤ 最大温度

2. **Token 配置验证**
   - 上下文窗口 (contextWindow): 必须有值 (> 0)
   - 最大响应token数 (maxResponseTokens): 必须有值 (> 0)
   - 最大响应token数必须 < 上下文窗口

### ImageGeneration API (apiType=2) 验证

1. **支持的图片尺寸 (supportedImageSizes)**
   - 必须有值（非空字符串）
   - 格式验证: 每个尺寸必须符合 `宽x高` 格式（如：`1024x1024`）
   - 正则表达式: `/^\d+x\d+$/`
   - 有效示例: `1024x1024, 1792x1024, 512x512`
   - 无效示例: `1024*1024`, `1024X1024`, `1024 x 1024`

2. **最大批量生成图片数量 (maxResponseTokens)**
   - 必须有值
   - 范围: 1-128 之间

## 后端验证 (UpdateModelRequest.cs)

### 验证特性类 (ModelValidationAttributes.cs)

#### 1. ValidateTemperatureRangeAttribute
验证温度范围关系：
```csharp
minTemperature <= maxTemperature
```

#### 2. ValidateChatResponseTokensAttribute
针对 ChatCompletion/Response API 验证：
- 上下文窗口 > 0
- 最大响应token数 > 0
- 最大响应token数 < 上下文窗口

#### 3. ValidateImageSizesAttribute
针对 ImageGeneration API 验证：
- 支持的图片尺寸数组不为空
- 每个尺寸符合 `宽x高` 格式
- 使用正则表达式: `^\d+x\d+$`

#### 4. ValidateImageBatchCountAttribute
针对 ImageGeneration API 验证：
- 最大批量生成图片数量在 1-128 之间

### UpdateModelRequest 字段验证

```csharp
[Required] [StringLength(100, MinimumLength = 1)]
public required string Name { get; init; }

[Required] [StringLength(100, MinimumLength = 1)]
public required string DeploymentName { get; init; }

[Range(1, short.MaxValue)]
public required short ModelKeyId { get; init; }

[Range(0, double.MaxValue)]
public required decimal InputFreshTokenPrice1M { get; init; }

[Range(0, double.MaxValue)]
public required decimal OutputTokenPrice1M { get; init; }

[Range(0, double.MaxValue)]
public required decimal InputCachedTokenPrice1M { get; init; }

[Range(0, 2)]
public required decimal MinTemperature { get; init; }

[Range(0, 2)]
public required decimal MaxTemperature { get; init; }

[Range(0, int.MaxValue)]
public required int ContextWindow { get; init; }

[Range(0, int.MaxValue)]
public required int MaxResponseTokens { get; init; }
```

## 验证错误消息

### 中文错误消息 (zh-CN.json)

- `"This field is require"`: "此字段为必填项"
- `"minTemperature must be less than or equal to maxTemperature"`: "最低温度必须小于或等于最高温度"
- `"Context window is required"`: "上下文窗口必须有值"
- `"Max response tokens is required"`: "最大响应token数必须有值"
- `"Max response tokens must be less than context window"`: "最大响应token数必须小于上下文窗口"
- `"Supported image sizes is required"`: "支持的图片尺寸必须有值"
- `"Invalid image size format, use format like: 1024x1024"`: "图片尺寸格式错误，请使用格式：1024x1024"
- `"Max batch count must be between 1 and 128"`: "最大批量数量必须在1到128之间"

## 实时验证

前端使用 React Hook Form 的 `mode: 'onChange'` 配置，确保：
- 用户输入时立即显示验证错误
- `formState.isValid` 状态准确
- 提交前自动进行完整验证

## 后端验证流程

1. ASP.NET Core 自动应用 `[DataAnnotations]` 验证
2. 如果验证失败，返回 400 Bad Request 和详细错误消息
3. 自定义验证特性在模型绑定后自动执行
4. 验证错误包含字段路径，方便前端显示

## 测试建议

### ChatCompletion/Response API
- [ ] 最小温度 > 最大温度（应失败）
- [ ] 上下文窗口 = 0（应失败）
- [ ] 最大响应token数 = 0（应失败）
- [ ] 最大响应token数 >= 上下文窗口（应失败）
- [ ] 温度范围 0-2 外的值（应失败）

### ImageGeneration API
- [ ] 支持的图片尺寸为空（应失败）
- [ ] 图片尺寸格式错误: `1024*1024`（应失败）
- [ ] 图片尺寸格式错误: `1024X1024`（应失败）
- [ ] 最大批量数量 = 0（应失败）
- [ ] 最大批量数量 > 128（应失败）
- [ ] 正确格式: `1024x1024, 1792x1024`（应成功）

## 维护注意事项

如果添加新的验证规则：
1. 在前端 `formSchema` 的 `.refine()` 中添加验证逻辑
2. 在后端创建对应的 `ValidationAttribute` 类
3. 在 `UpdateModelRequest` 上应用验证特性
4. 在 `zh-CN.json` 中添加错误消息翻译
5. 更新本文档

## 相关文件

- 前端验证: `src/FE/components/admin/Models/ModelModal.tsx`
- 后端验证: `src/BE/Controllers/Admin/AdminModels/Validators/ModelValidationAttributes.cs`
- DTO: `src/BE/Controllers/Admin/AdminModels/Dtos/UpdateModelRequest.cs`
- 翻译: `src/FE/locales/zh-CN.json`
