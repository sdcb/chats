# ModelSnapshot 数据迁移计划

本文档只讨论数据迁移方案，不讨论接口、服务、前端或运行时业务代码调整。目标是为后续让 Model 表支持硬删除打好数据基础。

## 目标

- 将历史事实数据从 live Model 脱钩。
- 为 ChatTurn 路径建立不可变快照链路。
- 保留 ChatSpan、ChatPresetSpan 这类 live 配置对 Model 的直接引用。
- 为后续 Model 硬删除创造条件。

## 范围

本次迁移只覆盖以下数据对象：

- ModelKey
- Model
- ChatConfig
- ChatTurn
- ChatConfigMcp
- UsageTransaction
- UserModelUsage

本次迁移暂不处理以下内容，但会在文档中标记后续建议：

- 业务代码读写切换
- UserModel / UserApiModel / UserApiKey 清理逻辑
- 前端灰显、管理端删除交互、接口返回结构

补充说明：

- 本文档以“数据库迁移与业务代码同版发布”为前提描述目标数据结构，不单独讨论兼容旧代码的过渡期方案。
- 本文档采用“head 表 + 当前快照指针 + snapshot/version 表”的目标建模方式。
- 对于 ModelKey 与 Model，创建时需要生成首个 Snapshot，修改时需要追加新 Snapshot，并推动 CurrentSnapshotId 指向最新版本。
- 在当前迁移范围内，真正需要从 ChatConfig 迁移到 ChatConfigSnapshot 的事实表只有 ChatTurn。
- ChatSpan 与 ChatPresetSpan 继续保留对 live ChatConfig 的引用，不需要迁移到 ChatConfigSnapshot。
- ChatConfigArchived 会被 ChatConfigSnapshot 替代，并在本次迁移收口阶段删除。

## 设计原则

### 1. live 数据与事实数据分离

- ModelKey 退化为“密钥 head 表”，只承载身份、顺序和当前版本指针。
- ModelKeySnapshot 承担“密钥配置 version/snapshot 表”的角色。
- Model 退化为“模型 head 表”，只承载身份、排序、启停状态和当前版本指针。
- ModelSnapshot 承担“模型配置 version/snapshot 表”的角色。
- ChatConfig 继续承担“当前用户配置模板”的角色。
- ChatConfigSnapshot 承担“历史事实配置快照”的角色。

### 2. 历史记录不再依赖 live 表

迁移完成后，历史事实链路应为：

ChatTurn -> ChatConfigSnapshot -> ModelSnapshot -> ModelKeySnapshot

而不是：

ChatTurn -> ChatConfig -> Model

### 3. live 引用继续保留在原表

以下路径仍应保留对 live Model 的引用，不纳入历史快照链：

- Chat -> ChatSpan -> ChatConfig -> Model
- ChatPreset -> ChatPresetSpan -> ChatConfig -> Model

live 路径读取当前配置时，应继续沿着 head 表读取其 CurrentSnapshot：

- ChatSpan / ChatPresetSpan -> ChatConfig -> Model -> Current ModelSnapshot -> ModelKey -> Current ModelKeySnapshot

## 迁移前提与现实限制

### 1. 现有历史模型/密钥版本不可追溯

当前库中并没有 Model 与 ModelKey 的版本历史表，因此对历史 ChatTurn 来说，只能基于“迁移执行时刻的当前值”生成第一版快照。

这意味着：

- 迁移后可以保证从那一刻开始历史数据不再受 live Model 变更影响。
- 迁移后可以保证从那一刻开始历史数据不再受 live ModelKey 变更影响。
- 迁移前已经发生过的历史模型变更无法被精确还原。
- 迁移前已经发生过的密钥、Host、Provider 配置变更也无法被精确还原。

这个限制需要在迁移评审中明确接受。

### 2. ChatConfigArchived 现状不足以承载历史事实

当前 ChatConfigArchived 仅是 ChatConfig 的 1:1 扩展，只保存 HashCode，无法独立表达完整历史配置。因此不能继续沿用为最终事实表。

从目标数据模型上看：

- ChatConfigSnapshot 已经承担了它原本试图承载的“配置归档/快照”职责。
- ChatConfigArchived 不再参与新快照迁移逻辑。
- 在本次迁移收口阶段，ChatConfigArchived 应被删除。

## 目标表结构

## 1. 收缩 ModelKey 表

建议保留字段：

- Id
- CurrentSnapshotId
- Order
- CreatedAt
- UpdatedAt

说明：

- ModelKey 不再承载完整配置详情，而是承担 head 表职责。
- live 读取 ModelKey 当前配置时，应通过 CurrentSnapshotId 取得最新的 ModelKeySnapshot。
- 创建 ModelKey 时需要同时创建首个 ModelKeySnapshot，并回写 CurrentSnapshotId。
- 修改 ModelKey 时不再就地覆盖配置字段，而是追加新 ModelKeySnapshot，并推动 CurrentSnapshotId。

## 2. 新增 ModelKeySnapshot 表

建议字段：

- Id
- ModelKeyId
- ModelProviderId
- Name
- Host
- Secret
- CreatedAt

说明：

- ModelKeySnapshot 的目标是承载历史调用所依赖的完整密钥配置事实，不应再依赖 live ModelKey。
- ModelKeySnapshot 直接保存全量 Secret 历史，用于降低建模复杂度，并保留完整版本事实。
- ModelKeyId 字段不建外键约束回 ModelKey head 表。这样 ModelKey 可以被硬删除，而其版本历史仍然保留。
- 由于保存的是完整 Secret 历史，必须依赖数据库层面的敏感数据保护措施，例如访问控制、审计和静态加密。

## 3. 收缩 Model 表

建议保留字段：

- Id
- CurrentSnapshotId
- Order
- Enabled
- CreatedAt
- UpdatedAt

说明：

- Model 不再承载完整配置详情，而是承担 head 表职责。
- live 路径继续引用 Model head 表，再通过 CurrentSnapshotId 获取当前版本的 ModelSnapshot。
- 创建 Model 时需要同时创建首个 ModelSnapshot，并回写 CurrentSnapshotId。
- 修改 Model 时不再就地覆盖配置字段，而是追加新 ModelSnapshot，并推动 CurrentSnapshotId。
- Enabled 与 Order 这类明显属于 live 管理态的字段，仍保留在 Model head 表。
- Enabled 取代原来的 IsDeleted，语义取反：true 表示启用，false 表示禁用。

## 4. 新增 ModelSnapshot 表

建议字段：

- Id
- ModelId
- Name
- DeploymentName
- ModelKeyId
- ModelKeySnapshotId
- ApiTypeId
- InputFreshTokenPrice1M
- InputCachedTokenPrice1M
- OutputTokenPrice1M
- AllowSearch
- AllowVision
- AllowStreaming
- AllowToolCall
- AllowCodeExecution
- ThinkTagParserEnabled
- MinTemperature
- MaxTemperature
- ContextWindow
- MaxResponseTokens
- ReasoningEffortOptions
- SupportedImageSizes
- UseAsyncApi
- UseMaxCompletionTokens
- IsLegacy
- MaxThinkingBudget
- SupportsVisionLink
- CreatedAt

说明：

- ModelSnapshot 的目标是承载模型配置 version/snapshot 事实。
- ModelSnapshot 必须归属于某个 Model head，因此应保存 ModelId。
- ModelId 字段不建外键约束回 Model head 表。这样 Model 可以被硬删除，而其版本历史仍然保留。
- ModelSnapshot 应同时保存 ModelKeyId 与 ModelKeySnapshotId。
- ModelKeyId 用于 live 路径继续沿 ModelKey head 表读取当前密钥版本，即 Model -> Current ModelSnapshot -> ModelKey -> Current ModelKeySnapshot。
- ModelKeySnapshotId 用于历史事实链路固化创建该 ModelSnapshot 时对应的密钥版本，避免历史数据受后续 ModelKey 变更影响。
- ModelKeyId 与 ModelKeySnapshotId 都不建外键约束回 ModelKey / ModelKeySnapshot head 链路以外的 live 删除路径；历史版本保留，live 删除通过业务前置校验控制。
- 如果后续希望弱化跨表依赖，也可以继续将 provider 名称、key 名称等展示字段冗余进 ModelSnapshot。

## 5. 新增 ChatConfigSnapshot 表

建议字段：

- Id
- ModelSnapshotId
- SystemPrompt
- Temperature
- WebSearchEnabled
- MaxOutputTokens
- ReasoningEffortId
- CodeExecutionEnabled
- ImageSize
- ThinkingBudget
- EnabledMcpNames
- HashCode nullable
- CreatedAt

说明：

- ModelSnapshotId 必须指向 ModelSnapshot，而不是 Model。
- EnabledMcpNames 为可空字符串，使用逗号分隔，默认 null，仅记录启用的 MCP 名称列表，不保留 custom headers。
- HashCode 允许为 null。迁移阶段写入的历史数据可先填 null，后续由应用新写入或后台补算时再填入真实值。
- HashCode 的语义应与最终落表字段保持一致，用于后续快速缩小候选范围，但不能替代最终的全字段精确比较。

## 6. 将 ChatTurn 改为引用 ChatConfigSnapshot

建议目标结构：

- ChatTurn.ChatConfigSnapshotId

说明：

- ChatTurn 作为事实表，应从 ChatConfig 切换为引用 ChatConfigSnapshot。
- 迁移执行时可以先增加可空的 ChatConfigSnapshotId 用于回填。
- 由于当前库中存在大量 ChatTurn.ChatConfigId 为空的历史记录，最终结构中的 ChatConfigSnapshotId 也必须保持可空，不能强行收紧为非空。
- 在完成回填与校验后，应删除旧的 ChatTurn.ChatConfigId 及其外键。

## 7. 将 UsageTransaction 改为引用 ModelSnapshot

建议变更：

- 删除 UsageTransaction.ModelId 及其外键 FK_UsageTransaction_Model
- 新增 UsageTransaction.ModelSnapshotId（nullable，用于回填；回填完成后可建非空约束）

说明：

- UsageTransaction 是账单历史事实，应引用不可变的 ModelSnapshot，而不是 live Model。
- 改接后硬删除 Model 不再需要处理账单外键悬挂问题。

## 8. 将 UserModelUsage 改为引用 ModelSnapshot

建议变更：

- 删除 UserModelUsage.ModelId 及其外键 FK_UserModelUsage_Model
- 新增 UserModelUsage.ModelSnapshotId（nullable，用于回填；回填完成后可建非空约束）

说明：

- UserModelUsage 是调用历史事实，应引用不可变的 ModelSnapshot，而不是 live Model。
- 改接后硬删除 Model 不再需要处理调用历史外键悬挂问题。

## 硬删除规则

### Model 硬删除

Model 硬删除的前置条件：

- ChatConfig 中不存在任何对该 Model 的引用（ChatConfig.ModelId）
- ChatSpan、ChatPresetSpan 通过 ChatConfig 间接引用 Model，因此只要 ChatConfig 无引用即自动满足

满足前置条件后，硬删除 Model 时需要联动处理以下依赖：

| 依赖表 | 外键 | 处理方式 | 理由 |
|---|---|---|---|
| ModelSnapshot | ModelSnapshot.ModelId（无 FK） | 保留不动 | 版本历史事实，不建外键回 head，天然不受删除影响 |
| ChatConfig | ChatConfig.ModelId | 前置条件已确保无记录 | live 配置引用，不允许残留 |
| UserModel | UserModel.ModelId | 级联删除 | 授权关系，随模型消失 |
| UserApiKey ↔ Model | 中间表 ModelId | 级联删除中间表记录 | 多对多授权关系 |
| UserApiCache | UserApiCache.ModelId | 级联删除 | 临时缓存，无历史价值 |
| UsageTransaction | UsageTransaction.ModelSnapshotId | 保留不动 | 账单历史事实，已改接 ModelSnapshot |
| UserModelUsage | UserModelUsage.ModelSnapshotId | 保留不动 | 调用历史事实，已改接 ModelSnapshot |

说明：

- 由于 ModelSnapshot / ModelKeySnapshot 不建外键回 head 表，版本历史天然不受 head 删除影响。
- UsageTransaction 和 UserModelUsage 也需要从 ModelId 改接为 ModelSnapshotId，使其也指向不可变事实。
- 改接后硬删除 Model 不再有任何外键悬挂问题。

### ModelKey 硬删除

ModelKey 硬删除的前置条件：

- 不存在任何 live Model 的 CurrentSnapshot 所指向的 ModelSnapshot 仍引用该 ModelKey（Model.CurrentSnapshotId -> ModelSnapshot.ModelKeyId）

满足前置条件后，硬删除 ModelKey 时需要联动处理以下依赖：

| 依赖表 | 外键 | 处理方式 | 理由 |
|---|---|---|---|
| ModelSnapshot | ModelSnapshot.ModelKeyId / ModelSnapshot.ModelKeySnapshotId（无 FK） | 保留不动 | 历史版本事实保留；是否仍被 live 使用由删除前置条件保障 |
| ModelKeySnapshot | ModelKeySnapshot.ModelKeyId（无 FK） | 保留不动 | 版本历史事实，不建外键回 head，天然不受删除影响 |

说明：

- ModelKey 的建议删除判定应基于“是否仍被任何 live Model 的当前版本使用”，而不是基于已收缩后的 Model head 表静态列。
- ModelKeySnapshot 直接保存 Secret 全量历史，作为版本事实链的一部分，不应随 ModelKey 删除而丢失。
- 由于 ModelKeySnapshot 不建外键回 ModelKey head 表，硬删除 ModelKey 不会影响版本历史。
- ModelKeySnapshot 通过 ModelSnapshot → ChatConfigSnapshot 间接关联到历史 ChatTurn，版本链保持完整。

### 硬删除执行顺序

当一个 Model 需要硬删除时，推荐执行顺序：

1. 检查 ChatConfig 是否仍有对该 Model 的引用 → 有则拒绝删除
2. 删除 UserModel 记录
3. 删除 UserApiKey ↔ Model 中间表记录
4. 删除 UserApiCache 记录
5. 删除 Model head 表记录

当一个 ModelKey 需要硬删除时，推荐执行顺序：

1. 检查是否仍存在 Model.CurrentSnapshotId -> ModelSnapshot.ModelKeyId 指向该 ModelKey → 有则拒绝删除
2. 删除 ModelKey head 表记录

## 迁移阶段

## 阶段 0：迁移前盘点

执行以下盘点查询，产出基线数据：

- ModelKey 总数
- Model 总数
- ChatConfig 总数
- ChatTurn 中 ChatConfigId 非空总数
- ChatConfigMcp 总数
- UsageTransaction 中 ModelId 非空总数
- UserModelUsage 中 ModelId 非空总数
- ChatSpan / ChatPresetSpan 对各 Model 的引用分布
- ChatTurn 对各 ChatConfig 的引用分布

产出物：

- 一份迁移基线报告
- 一份异常数据清单

重点检查：

- 在旧结构中，是否存在 Model.ModelKeyId 指向不存在 ModelKey 的脏数据
- 是否存在 ChatTurn.ChatConfigId 指向不存在 ChatConfig 的脏数据
- 是否存在 ChatConfig.ModelId 指向不存在 Model 的脏数据

额外确认：

- 现有 ModelKey / Model 上哪些字段属于“配置字段”，哪些字段属于“head 管理字段”。
- 收口阶段需要从 ModelKey / Model 删除哪些重复配置列。

## 阶段 1：扩表，不改旧数据

先发布纯 schema migration：

- 为 ModelKey 增加 CurrentSnapshotId nullable
- 为 Model 增加 CurrentSnapshotId nullable
- 创建 ModelKeySnapshot
- 创建 ModelSnapshot
- 创建 ChatConfigSnapshot
- 为 ChatTurn 增加 ChatConfigSnapshotId nullable
- 为 UsageTransaction 增加 ModelSnapshotId nullable
- 为 UserModelUsage 增加 ModelSnapshotId nullable
- 增加必要索引和约束

建议索引：

- ChatConfigSnapshot(HashCode) where HashCode is not null
- ChatConfigSnapshot(ModelSnapshotId, HashCode) where HashCode is not null
- ChatTurn(ChatConfigSnapshotId)
- UsageTransaction(ModelSnapshotId)
- UserModelUsage(ModelSnapshotId)

建议唯一约束：

- 唯一索引：ChatConfigSnapshot(ModelSnapshotId, HashCode) where HashCode is not null

说明：

- ModelSnapshot 作为事实表可以不做内容去重，迁移阶段只需保证同一条旧 Model 记录能稳定映射到一条新快照记录。
- 由于迁移阶段会存在大量 HashCode 为 null 的历史记录，索引和唯一性约束应采用 filtered index，而不是对全表生效的普通唯一约束。
- 这一阶段不移动任何旧数据，确保 schema 变更可独立发布。
- 若实现脚本采用单次全量发布，也可以在一个脚本内串行完成扩表、回填、收口和最终校验；本节描述的是逻辑阶段，而不是必须拆分的发布次数。

## 阶段 2：回填 ModelKeySnapshot

### 2.1 生成密钥快照源数据

从当前 ModelKey 全量扫描，逐条生成 ModelKeySnapshot 候选记录。

每条 ModelKey 记录生成一条候选快照记录，字段取值来自迁移时刻的 ModelKey 当前值。

其中：

- ModelProviderId、Name、Host、Secret 直接复制。

### 2.2 直接插入

不做内容去重，直接写入 ModelKeySnapshot。

建议保留第一张临时映射表或中间结果：

- ModelKeyId
- ModelKeySnapshotId

在插入后，需要同步回填：

- ModelKey.CurrentSnapshotId = 最新插入的 ModelKeySnapshot.Id

### 2.3 校验

校验项：

- 每个现存 ModelKey 都能映射到一个 ModelKeySnapshot
- ModelKeySnapshot 记录数应等于迁移时参与回填的 ModelKey 记录数
- 每个 ModelKey 都应能找到合法的 CurrentSnapshotId
- 临时映射表中不应出现一条 ModelKey 记录对应多个 ModelKeySnapshot 的异常

## 阶段 3：回填 ModelSnapshot

### 3.1 生成模型快照源数据

从当前 Model 全量扫描，逐条生成 ModelSnapshot 候选记录。

每条 Model 记录生成一条候选快照记录，字段取值来自迁移时刻的 Model 当前值。

其中：

- ModelKeyId 直接写入 ModelSnapshot.ModelKeyId，作为 live 路径继续沿 ModelKey head 表读取当前密钥版本的锚点。
- ModelKeyId 同时需通过阶段 2 的映射，转换为 ModelKeySnapshotId，作为历史事实固化的密钥版本。
- ModelId 直接写入 ModelSnapshot.ModelId，用于保留版本归属。

### 3.2 直接插入

不做内容去重，直接写入 ModelSnapshot。

建议保留第二张临时映射表或中间结果：

- ModelId
- ModelSnapshotId

该映射在后续 ChatConfigSnapshot 回填阶段会被复用。

在插入后，需要同步回填：

- Model.CurrentSnapshotId = 最新插入的 ModelSnapshot.Id

### 3.3 校验

校验项：

- 每个现存 Model 都能映射到一个 ModelSnapshot
- ModelSnapshot 记录数应等于迁移时参与回填的 Model 记录数
- 每个 ModelSnapshot 都能找到对应的 ModelKey
- 每个 ModelSnapshot 都能找到对应的 ModelKeySnapshot
- 每个 Model 都应能找到合法的 CurrentSnapshotId
- 在迁移回填完成当下，每个 ModelSnapshot 的 ModelKeyId 与 ModelKeySnapshotId 应能通过阶段 2 映射对应上
- 临时映射表中不应出现一条 Model 记录对应多个 ModelSnapshot 的异常

## 阶段 4：回填 ChatConfigSnapshot

### 4.1 构造候选快照

从 ChatConfig 全量扫描，关联：

- ChatConfigMcp 获取已启用 MCP 名称列表
- 阶段 3 生成的 Model -> ModelSnapshot 映射

每条 ChatConfig 生成一条候选 ChatConfigSnapshot：

- 普通字段直接复制
- ModelId 替换为 ModelSnapshotId
- 将启用的 MCP 名称按稳定顺序排序后拼成逗号分隔字符串，写入 EnabledMcpNames
- HashCode 统一填 null

### 4.2 哈希策略

建议策略：

- 迁移阶段不计算 HashCode，统一填 null
- 旧 ChatConfigArchived.HashCode 不参与新快照迁移逻辑
- 因为旧 HashCode 的语义包含 ChatConfigMcp 明细和 CustomHeaders，而新快照只保留 EnabledMcpNames

后续应用新写入 ChatConfigSnapshot 时，再计算新的 HashCode。该 HashCode 只覆盖最终实际落表的字段：

- ModelSnapshotId
- SystemPrompt
- Temperature
- WebSearchEnabled
- MaxOutputTokens
- ReasoningEffortId
- CodeExecutionEnabled
- ImageSize
- ThinkingBudget
- EnabledMcpNames

这样可以保证哈希语义与快照表结构一致。

如果未来需要为历史数据补算 HashCode，建议复用同一份 C# 算法实现，不要求在迁移阶段由 T-SQL 直接计算。

### 4.3 直接插入

迁移阶段不依赖 HashCode 去重，直接插入 ChatConfigSnapshot。

建议保留第二张临时映射表：

- ChatConfigId
- ChatConfigSnapshotId

### 4.4 校验

校验项：

- 每个被 ChatTurn 引用到的 ChatConfig 都能映射到一个 ChatConfigSnapshot
- 每个 ChatConfigSnapshot 都能找到对应的 ModelSnapshot
- 迁移批次插入的 ChatConfigSnapshot，其 HashCode 应全部为 null
- EnabledMcpNames 为空的记录只应来自没有启用 MCP 的 ChatConfig
- EnabledMcpNames 非空的记录应能与原 ChatConfigMcp 按名称集合对应上

## 阶段 5：回填 ChatTurn.ChatConfigSnapshotId

### 5.1 回填规则

对所有 ChatTurn.ChatConfigId 非空的记录：

- 通过 ChatConfigId -> ChatConfigSnapshotId 临时映射表回填 ChatConfigSnapshotId

对 ChatTurn.ChatConfigId 为空的记录：

- 保持 ChatConfigSnapshotId 为空

### 5.2 校验

必须满足：

- 回填后，所有原本 ChatConfigId 非空的 ChatTurn，其 ChatConfigSnapshotId 也非空
- 回填条数与基线统计一致
- 不允许出现 ChatConfigId 非空但找不到 ChatConfigSnapshotId 的漏数

建议输出核对结果：

- 原始 ChatTurn 引用数
- 成功回填数
- 漏回填数
- 漏回填样本清单

## 阶段 6：回填 UsageTransaction.ModelSnapshotId 与 UserModelUsage.ModelSnapshotId

### 6.1 回填规则

对所有 UsageTransaction.ModelId 非空的记录：

- 通过阶段 3 生成的 ModelId -> ModelSnapshotId 映射表回填 ModelSnapshotId

对所有 UserModelUsage.ModelId 非空的记录：

- 通过阶段 3 生成的 ModelId -> ModelSnapshotId 映射表回填 ModelSnapshotId

### 6.2 校验

必须满足：

- 回填后，所有原本 ModelId 非空的记录，其 ModelSnapshotId 也非空
- 回填条数与基线统计一致
- 不允许出现 ModelId 非空但找不到 ModelSnapshotId 的漏数

## 阶段 7：建立约束前核对

在不删除旧列的前提下，执行一轮全量一致性检查：

- ModelKey -> CurrentSnapshotId -> ModelKeySnapshot
- Model -> CurrentSnapshotId -> ModelSnapshot -> ModelKey -> CurrentSnapshotId -> ModelKeySnapshot
- ChatTurn.ChatConfigSnapshotId -> ChatConfigSnapshot -> ModelSnapshot -> ModelKeySnapshot
- ChatTurn.ChatConfigId -> ChatConfig -> Model
- UsageTransaction.ModelId -> ModelSnapshot (通过临时映射)
- UserModelUsage.ModelId -> ModelSnapshot (通过临时映射)

核对维度建议包括：

- SystemPrompt 是否一致
- Temperature 是否一致
- WebSearchEnabled 是否一致
- CodeExecutionEnabled 是否一致
- MaxOutputTokens 是否一致
- ReasoningEffortId 是否一致
- ImageSize 是否一致
- ThinkingBudget 是否一致
- EnabledMcpNames 是否与原启用 MCP 名称集合一致

说明：

- 这里允许 Model live 字段与 ModelSnapshot 字段在未来发生偏离。
- 这里也允许某个 live Model 当前经由 ModelKey.CurrentSnapshotId 读取到的密钥版本，在未来晚于该 ModelSnapshot 内固化保存的 ModelKeySnapshotId。
- 但在首次迁移回填时，两边应尽量一致。
- 由于不保留 custom headers，核对范围只包括 MCP 名称集合，不包括 headers 内容。
- 实际执行脚本至少应校验所有新外键列都能解析到合法快照；若脚本未覆盖上述全部字段级内容比对，则仍应在迁移验收时补做人工抽样核对。

## 阶段 8：迁移收口

本阶段完成最终数据结构收口。

应完成：

- 为 ModelKey.CurrentSnapshotId 建外键
- 为 Model.CurrentSnapshotId 建外键
- 为 ModelKey.CurrentSnapshotId 建非空约束
- 为 Model.CurrentSnapshotId 建非空约束
- 为 ChatTurn.ChatConfigSnapshotId 建外键
- 为 UsageTransaction.ModelSnapshotId 建外键回 ModelSnapshot（无 FK 回 head）
- 为 UsageTransaction.ModelSnapshotId 建非空约束
- 为 UserModelUsage.ModelSnapshotId 建外键回 ModelSnapshot（无 FK 回 head）
- 为 UserModelUsage.ModelSnapshotId 建非空约束
- 删除 ModelKey 上已迁出的重复配置列
- 为 Model.Enabled 回填数据，规则为 IsDeleted = 0 -> Enabled = 1，IsDeleted = 1 -> Enabled = 0
- 删除 Model 上已迁出的重复配置列
- 删除 Model.IsDeleted
- 删除 UsageTransaction.ModelId 及其外键
- 删除 UserModelUsage.ModelId 及其外键
- 删除 ChatTurn.ChatConfigId
- 删除 ChatConfigArchived

不应在本阶段删除：

- 删除 ChatConfig -> Model 的外键

说明：

- ChatSpan 与 ChatPresetSpan 仍然需要通过 ChatConfig 关联 live Model，因此 ChatConfig -> Model 的外键仍应保留。
- 本次迁移收口的核心是让所有事实表脱离 live head 表，并让 ChatConfigArchived 退出数据模型。
- ChatTurn.ChatConfigSnapshotId 在最终结构中仍允许为 null，用于承接原本 ChatConfigId 就为空的历史消息。

## 建议的数据迁移顺序

推荐拆成 4 次独立发布：

1. 扩表发布
2. ModelKeySnapshot 与 ModelSnapshot 回填发布
3. ChatConfigSnapshot、ChatTurn、UsageTransaction、UserModelUsage 回填发布
4. 一致性校验与收口发布

这样做的好处是：

- 每一步都可独立回滚
- 异常定位范围更小
- 对线上锁表时间更友好

## 回滚策略

### 1. 阶段 1 回滚

如果只是扩表失败，直接回滚 schema migration。

### 2. 阶段 2-6 回滚

建议所有回填脚本满足幂等：

- 重复执行不会产生重复快照
- 重复执行不会覆盖已确认正确的数据

若回填结果异常，可采用以下方式回滚：

- 清空新表中的本次批次数据
- 清空 ChatTurn.ChatConfigSnapshotId
- 保留旧表和旧外键不变

前提是：

- 尚未对新列加非空约束
- 尚未删除旧的 ModelKey / Model 重复配置列
- 尚未删除旧的 ChatTurn.ChatConfigId 与 ChatConfigArchived
- 尚未删除旧的 UsageTransaction.ModelId 与 UserModelUsage.ModelId

## 数据校验清单

### 1. 收口前校验

在删除旧列之前，至少执行以下校验：

- ModelKeySnapshot 总数等于迁移时参与回填的 ModelKey 记录数
- 每个 ModelKey 都存在合法 CurrentSnapshotId
- ModelSnapshot 总数等于迁移时参与回填的 Model 记录数
- 每个 Model 都存在合法 CurrentSnapshotId
- ChatConfigSnapshot 总数等于迁移时参与回填的 ChatConfig 记录数
- ChatTurn 中 ChatConfigId 非空数等于 ChatConfigSnapshotId 非空数
- 每个 ModelSnapshot 都存在合法 ModelKeyId
- 每个 ModelSnapshot 都存在合法 ModelKeySnapshotId
- 每个 ModelSnapshot 都存在合法 ModelId
- 每个 ChatConfigSnapshot 都存在合法 ModelSnapshotId
- 迁移批次插入的 ChatConfigSnapshot，其 HashCode 都为 null
- 每条 ChatConfigSnapshot 的 EnabledMcpNames 都满足 null 或逗号分隔字符串格式
- UsageTransaction 中 ModelId 非空数等于 ModelSnapshotId 非空数
- UserModelUsage 中 ModelId 非空数等于 ModelSnapshotId 非空数
- 任意抽样一批 ChatTurn，快照内容与原 ChatConfig 内容一致

补充说明：

- 若执行脚本只实现了引用完整性与数量级校验，而没有完整覆盖字段级内容比对，上述最后一条应作为迁移验收时的人工抽样检查项保留。

### 2. 收口后校验

在阶段 8 删除旧列之后，至少执行以下校验：

- 每个 ModelKey 都存在合法 CurrentSnapshotId
- 每个 Model 都存在合法 CurrentSnapshotId
- 每个 Model.CurrentSnapshotId 都能解析到合法的 ModelSnapshot
- 每个 live Model 当前版本都能继续经由 ModelSnapshot.ModelKeyId -> ModelKey.CurrentSnapshotId 解析到合法的 ModelKeySnapshot
- 每个 ModelSnapshot 都存在合法 ModelKeyId
- 每个 ModelSnapshot 都存在合法 ModelKeySnapshotId
- 每个 ModelSnapshot 都存在合法 ModelId
- 每个 ChatConfigSnapshot 都存在合法 ModelSnapshotId
- 每个 ChatTurn 在 ChatConfigSnapshotId 非 null 时都存在合法的 ChatConfigSnapshotId
- 每个 UsageTransaction 都存在合法的 ModelSnapshotId
- 每个 UserModelUsage 都存在合法的 ModelSnapshotId
- 任意抽样一批 ChatTurn，快照内容与对应 ChatConfigSnapshot 一致

## 风险与注意事项

### 1. 最大风险：历史事实只能从当前值补建

由于缺少既往版本历史，迁移生成的第一批快照本质上是“迁移时刻快照”，不是“消息发送时刻快照”。这是本方案最大的历史精度损失。

### 2. Secret 全量历史存储风险

由于 ModelKeySnapshot 直接保存完整 Secret 历史，数据库中的敏感信息暴露面会扩大。这会降低建模复杂度，但必须通过更严格的访问控制、审计、备份保护和静态加密来对冲风险。

### 3. HashCode 语义变化风险

由于 ChatConfigSnapshot 不再保留 MCP 明细和 CustomHeaders，需要确保：

- 旧 ChatConfigArchived.HashCode 不再参与新快照的唯一性或查重逻辑
- 新哈希算法只覆盖最终实际落表字段
- 迁移阶段允许 HashCode 为 null，运行期索引与唯一性约束应只针对非 null 数据生效
- 后续如果补算历史 HashCode，必须复用与运行期完全一致的算法

### 4. MCP 名称归一化风险

ChatConfigSnapshot 去重时，EnabledMcpNames 必须先按稳定顺序归一化，否则相同名称集合可能被误判为不同快照。

### 5. 历史精度下降风险

由于不保留 custom headers，迁移后历史事实只保留“启用了哪些 MCP”，不再保留“以什么 headers 调用 MCP”。这属于有意接受的信息损失，需要在方案评审中明确。

### 6. 大表回填锁风险

如果 ChatTurn 数据量较大，回填 ChatConfigSnapshotId 应采用分批更新，避免长事务和锁升级。

## 后续建议

在完成本次数据迁移后，下一阶段应优先评估以下事项：

1. 将 ChatTurn 业务读写切换到 ChatConfigSnapshot。
