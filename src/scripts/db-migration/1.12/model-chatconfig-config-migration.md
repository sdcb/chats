# 1.12 Model / ChatConfig 配置迁移计划

本文档只讨论数据迁移与目标表结构，不讨论接口、服务、前端或运行时业务代码实现。目标是在 1.12 版本里，把即将新增的模型配置与会话配置能力一次性放到正确的表层级，并保证 live 配置与历史快照的一致性。

## 目标

- 为图片生成增加“输出格式 + 压缩率”配置能力。
- 将 reasoning effort 从数字枚举迁移为字符串语义。
- 为模型增加 override url、custom headers、custom body。
- 保证 ChatConfig 与 ChatConfigSnapshot 能完整表达一次实际请求的配置。
- 保证 ModelSnapshot 能完整表达一次实际请求所依赖的模型能力与传输层配置。

## 范围

本次迁移只覆盖以下数据库对象：

- ModelSnapshot
- ChatConfig
- ChatConfigSnapshot
- 相关索引、约束、哈希语义

本次迁移不直接包含以下内容，但会为它们提供数据基础：

- 管理端模型编辑页改造
- ChatSpan / ChatPresetSpan 的接口字段改造
- 图片生成请求体构造逻辑改造
- OpenAI compatible API 入参与出参改造

本次迁移暂不处理：

- thinking replay / 思考信息回传能力的模型级配置

## 当前状态

### 1. Model 配置已不在 Model head 表

当前 1.12 代码基线里，模型的实际配置字段已经位于 ModelSnapshot，而不是 Model head 表。Model 只保留：

- Id
- CurrentSnapshotId
- Order
- Enabled
- CreatedAt
- UpdatedAt

这意味着本次所有“模型能力”和“模型传输层”新字段，都应该优先落在 ModelSnapshot，而不是重新加回 Model。

### 2. Chat 当前配置与历史配置分层明确

当前结构中：

- ChatConfig 承担 live 用户配置模板
- ChatConfigSnapshot 承担历史请求事实快照

因此，凡是会影响单次请求实际参数的字段，如果落在 ChatConfig，就必须同步进入 ChatConfigSnapshot。

### 3. 当前存在的几个结构性问题

- 图片生成虽然已经能识别 png、jpeg、jpg、webp 响应格式，但请求侧没有稳定的 DB 字段来配置输出格式与压缩率，实际默认仍偏向 png。
- ModelSnapshot.ReasoningEffortOptions 现在虽然是字符串列，但语义仍是“逗号分隔的数字”。
- ChatConfig.ReasoningEffortId 与 ChatConfigSnapshot.ReasoningEffortId 仍是 byte 枚举，无法同时自然承载文本模型的 reasoning effort 与图片模型的 quality 语义。
- 模型请求的 override url、custom headers、custom body 还没有稳定的模型级持久化字段。

## 设计原则

### 1. 字段落点按职责分层

- 模型能力、模型传输层、模型默认行为，落在 ModelSnapshot。
- 单次会话可选项、用户当前选择项，落在 ChatConfig。
- 会影响历史消息重放结果的 ChatConfig 字段，必须同步落在 ChatConfigSnapshot。

### 2. 不把配置重新塞回 Model head 表

当前 1.12 的主模型设计已经是 head 表 + snapshot 表。本次不应逆向回退到“配置重新堆回 Model head 表”。

### 3. effort 去枚举化

- Model 层保存“可选值集合”，采用逗号分隔字符串。
- ChatConfig 层保存“当前选中值”，采用单个字符串。
- 数据库不再强依赖固定枚举数值。
- null 同时表示未指定，也等价于 omit。

### 4. 图片输出配置与图片尺寸配置分离

当前 ChatConfig.ImageSize 表示图片尺寸。本次新增字段只负责：

- 输出格式
- Compression

不复用 ImageSize，也不继续挤占 Effort 的语义空间。

### 5. 自定义 headers/body 先按 JSON 文本存储

对 override 请求结构，优先采用单列 JSON 文本，而不是拆子表。理由是：

- 变更频率高
- provider 差异大
- 结构不稳定
- 迁移与回滚成本低

### 6. HashCode 语义必须同步升级

由于 ChatConfigSnapshot 依赖 HashCode 参与去重与快速定位，凡是会改变单次请求参数的新增 ChatConfig 字段，都必须纳入新的哈希语义。

## 目标表结构

## 1. 调整 ModelSnapshot

### 1.1 保留但重命名的字段

- ReasoningEffortOptions -> SupportedEfforts：语义从“数字 CSV”改成“字符串 CSV”

建议同时把列长度从当前 100 扩大到 200，避免后续 provider token 变长时截断。

示例：

- 旧值：2,3,4
- 新值：low,medium,high

### 1.2 新增字段

建议新增：

- SupportedFormats VARCHAR(100) NULL
- OverrideUrl VARCHAR(1000) NULL
- CustomHeaders VARCHAR(MAX) NULL
- CustomBody VARCHAR(MAX) NULL

字段说明：

- SupportedFormats：模型允许的输出格式后缀名列表，逗号分隔，例如 jpg,png,webp。
- OverrideUrl：模型级覆盖地址。非空时优先于 ModelKeySnapshot.Host。
- CustomHeaders：模型级额外请求头，建议保存 JSON object 字符串。
- CustomBody：模型级额外请求体 patch，语义为 JSON merge patch。

### 1.3 不新增到 Model head 表的理由

这些字段都属于“模型当前版本配置”，不是模型 identity，也不是 live 管理态字段，因此不应该放入 Model head 表。

## 2. 调整 ChatConfig

### 2.1 重命名字段

建议将：

- ReasoningEffortId -> Effort

并将类型改为：

- VARCHAR(50) NULL

说明：

- 该字段改为单个字符串，而不是 CSV。
- null 表示未指定，也等价于 omit。
- 该字段同时用于文本模型的 reasoning effort 与图片模型的 quality。

### 2.2 新增字段

建议新增：

- Format VARCHAR(20) NULL
- Compression SMALLINT NULL

字段说明：

- Format：本次会话选择的输出格式后缀名，如 png、jpg、webp。
- Compression：本次会话选择的压缩率，统一约定为 0-100。null 表示不显式指定。

## 3. 调整 ChatConfigSnapshot

ChatConfigSnapshot 需要与 ChatConfig 对齐，保证历史请求事实可重放。

### 3.1 重命名字段

建议将：

- ReasoningEffortId -> Effort

类型改为：

- VARCHAR(50) NULL

### 3.2 新增字段

建议新增：

- Format VARCHAR(20) NULL
- Compression SMALLINT NULL

### 3.3 HashCode 纳入字段

新的 ChatConfigSnapshot 哈希语义应覆盖：

- ModelSnapshotId
- SystemPrompt
- Temperature
- WebSearchEnabled
- MaxOutputTokens
- Effort
- CodeExecutionEnabled
- ImageSize
- Format
- Compression
- ThinkingBudget
- EnabledMcpNames

## 4. 字段落点总表

| 能力 | ModelSnapshot | ChatConfig | ChatConfigSnapshot |
|---|---|---|---|
| 输出格式后缀名支持列表 | 是 | 否 | 否 |
| 本次输出格式选择 | 否 | 是 | 是 |
| 本次 Compression 选择 | 否 | 是 | 是 |
| effort 可选值列表 | 是 | 否 | 否 |
| effort 当前值 | 否 | 是 | 是 |
| override url | 是 | 否 | 否 |
| custom headers | 是 | 否 | 否 |
| custom body | 是 | 否 | 否 |

## 数据迁移策略

## 阶段 0：迁移前盘点

建议先盘点以下数据：

- ModelSnapshot 总数
- ChatConfig 总数
- ChatConfigSnapshot 总数
- SupportedEfforts 非空分布
- ChatConfig.ReasoningEffortId 各枚举值分布
- ChatConfigSnapshot.ReasoningEffortId 各枚举值分布
- 图片生成模型总数
- ChatConfigSnapshot.HashCode 非空总数

额外确认：

- 当前线上是否已经存在 1.12 生成出的 ChatConfigSnapshot 数据
- 当前是否有依赖旧哈希算法的去重逻辑正在使用中

## 阶段 1：扩表与并存期字段

为了降低风险，建议先采用“新旧字段并存”的方式扩表：

- ModelSnapshot：新增新字段，暂不改写旧数据
- ChatConfig：先新增 Effort 新列，不立即删除旧的 ReasoningEffortId
- ChatConfigSnapshot：先新增 Effort 新列，不立即删除旧的 ReasoningEffortId
- ChatConfig：新增 Format、Compression
- ChatConfigSnapshot：新增 Format、Compression

建议索引调整：

- 保留现有 ChatConfigSnapshot(HashCode) 相关索引结构
- 如需大规模重算 HashCode，可先允许 HashCode 为 null 并使用 filtered index

## 阶段 2：迁移 ModelSnapshot.SupportedEfforts

### 2.1 迁移规则

把旧的数字 token 映射为字符串 token：

- 1 -> minimal
- 2 -> low
- 3 -> medium
- 4 -> high

示例：

- 旧值：1,2,3,4
- 新值：minimal,low,medium,high

### 2.2 风险控制

- 如果遇到未知数字 token，迁移脚本应直接报错并输出样本，不要静默跳过。
- 不建议在迁移阶段自动写入 xhigh、max，因为旧库里本来没有这些值。

## 阶段 3：迁移 ChatConfig / ChatConfigSnapshot 的 Effort

### 3.1 迁移规则

把旧 byte 枚举迁移为字符串：

- 0 -> null 或 omit
- 1 -> minimal
- 2 -> low
- 3 -> medium
- 4 -> high

建议在迁移脚本里统一先迁成：

- 0 -> null

理由：

- 与“未指定/omit”语义一致
- 同时兼容文本模型的 reasoning effort 和图片模型的 quality 语义

### 3.2 迁移对象

- ChatConfig 全量回填新列
- ChatConfigSnapshot 全量回填新列

### 3.3 收口

在业务代码完成切换后再删除：

- ChatConfig.ReasoningEffortId
- ChatConfigSnapshot.ReasoningEffortId

## 阶段 4：回填图片输出格式与压缩字段

### 4.1 ModelSnapshot 回填

建议采用保守回填：

- SupportedFormats：对现有图片生成模型先写 png

原因：

- 当前数据库里没有可靠历史数据表明哪些模型已经被验证支持 jpg 或 webp

如果发布前已经有人肉确认的模型白名单，可以在 migration seed 中单独改写为：

- png,jpg
- jpg,png,webp

本次不在 ModelSnapshot 增加 Compression 能力范围字段。

### 4.2 ChatConfig / ChatConfigSnapshot 回填

对于历史与当前数据，建议先统一回填：

- Format = null
- Compression = null

理由：

- 旧系统没有这两个显式字段
- 旧请求等价于“不额外指定，由模型默认行为决定”
- Compression 新语义统一为 0-100，但历史数据无法可靠反推具体值

## 阶段 5：回填 override url / custom headers / custom body

回填策略建议统一为：

- OverrideUrl = null
- CustomHeaders = null
- CustomBody = null

理由：

- 旧库没有这些概念
- 不能从当前 Host、provider 或业务代码可靠反推出模型级 override
- CustomBody 的目标语义固定为 JSON merge patch，不是完整 body 覆盖

## 阶段 6：HashCode 迁移与兼容

这是这次迁移最容易被忽略的点。

### 6.1 为什么旧 HashCode 可能失效

ChatConfigSnapshot 新增并修改以下字段后，配置身份已经发生变化：

- Effort 从 byte 变成 string
- Format 新增
- Compression 新增

如果继续沿用旧 HashCode，就会出现：

- 语义不同但 hash 相同
- 新老快照去重结果不一致

### 6.2 建议方案

建议采用以下策略之一：

- 方案 A：把历史 ChatConfigSnapshot.HashCode 全部置 null，等待后续按新算法懒计算
- 方案 B：通过一段 C# 批处理按新算法全量重算 HashCode，再回写数据库

不建议：

- 在 T-SQL 里临时复制一份近似哈希算法

推荐优先级：

- 优先方案 A

原因：

- 风险最小
- 与当前 1.12 方案里“迁移阶段允许 HashCode 为 null”的思路一致

## 阶段 7：收口

在业务代码完成切换后，再进行收口：

- 删除 ChatConfig.ReasoningEffortId
- 删除 ChatConfigSnapshot.ReasoningEffortId
- 把所有读写逻辑切到新的 Effort string 字段
- 把模型编辑接口切到字符串版 SupportedEfforts
- 把图片生成逻辑切到 Format / Compression
- 把 endpoint 解析逻辑切到 OverrideUrl 优先
- 把 custom headers / custom body 合并到请求构造

## 推荐发布顺序

建议拆成 4 段：

1. 扩表，新增并存字段
2. 回填 ModelSnapshot 的字符串能力字段与模型级新配置字段
3. 回填 ChatConfig / ChatConfigSnapshot 的 reasoning 与图片输出字段
4. 切业务代码、重建哈希语义、删除旧列

这样做的好处：

- 迁移可回滚
- 问题定位更容易
- 不会把“字段改造”和“业务行为改造”绑死在同一脚本里

## 风险与注意事项

### 1. 最容易出问题的是 reasoning effort 的旧值映射

旧库里目前只有数字语义，新库要允许 provider 自定义字符串。迁移脚本必须先把历史值映射成一组稳定的基础 token，再由后续业务代码决定是否显示 provider 别名。

### 2. Compression 语义已经固定为 0-100

这次文档已经明确 Compression 统一采用 0-100，因此迁移脚本和后续校验逻辑都应直接围绕这个约定展开，不再保留 provider 自定义区间字段。

### 3. OverrideUrl 建议按模型级而不是 key 级存储

因为用户的诉求是“用模型配置覆盖 model key baseUrl”，不是“替换整把 key 的 host”。这两个粒度不同，不能混放。

### 4. CustomHeaders / CustomBody 需要做 JSON object 约束

虽然数据库先存 VARCHAR(MAX)，但最终接口与服务层应限制为 JSON object，而不是任意 JSON 文本。

同时，CustomBody 应按 JSON merge patch 解释，而不是完整 body 覆盖。

## 已确认结论

在真正开始写 SQL 前，本次已经拍板的结论如下：

1. `ReasoningEffortOptions/ReasoningEffort` 这组命名改为 `SupportedEfforts/Effort`。
2. `Effort` 存 null，null 同时表示未指定，也等价于 omit。
3. 压缩字段统一改名为 `Compression`，语义固定为 0-100，不再增加模型级压缩区间字段。
4. 输出格式支持列表改名为 `SupportedFormats`，只存后缀名列表，例如 jpg,png,webp。
5. `CustomBody` 的语义固定为 JSON merge patch。

## 结论

这次迁移的核心不是“多加几个列”，而是把新增能力放在正确的层级：

- ModelSnapshot 负责模型能力与传输层
- ChatConfig 负责 live 用户选择
- ChatConfigSnapshot 负责历史事实重放

只要这个分层不动，后续无论是 jpg/webp、xhigh/max，还是 override url / custom headers / custom body，都能在 1.12 的 snapshot 架构内自然演进，而不需要再做一次模型结构翻修。