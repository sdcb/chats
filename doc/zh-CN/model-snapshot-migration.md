# ModelSnapshot 数据迁移计划

本文档只讨论数据迁移方案，不讨论接口、服务、前端或运行时业务代码调整。目标是为后续让 Model 表支持硬删除打好数据基础。

## 目标

- 将历史事实数据从 live Model 脱钩。
- 为 ChatTurn 路径建立不可变快照链路。
- 保留 ChatSpan、ChatPresetSpan 这类 live 配置对 Model 的直接引用。
- 为后续 Model 硬删除创造条件。

## 范围

本次迁移只覆盖以下数据对象：

- Model
- ChatConfig
- ChatTurn
- ChatConfigMcp

本次迁移暂不处理以下内容，但会在文档中标记后续建议：

- 业务代码读写切换
- UserModel / UserApiModel / UserApiKey 清理逻辑
- UsageTransaction / UserModelUsage 是否也改接 ModelSnapshot
- 前端灰显、管理端删除交互、接口返回结构

补充说明：

- 本文档以“数据库迁移与业务代码同版发布”为前提描述目标数据结构，不单独讨论兼容旧代码的过渡期方案。
- 在当前迁移范围内，真正需要从 ChatConfig 迁移到 ChatConfigSnapshot 的事实表只有 ChatTurn。
- ChatSpan 与 ChatPresetSpan 继续保留对 live ChatConfig 的引用，不需要迁移到 ChatConfigSnapshot。
- ChatConfigArchived 会被 ChatConfigSnapshot 替代，并在本次迁移收口阶段删除。

## 设计原则

### 1. live 数据与事实数据分离

- Model 继续承担“当前可选模型目录”的角色。
- ModelSnapshot 承担“历史事实模型快照”的角色。
- ChatConfig 继续承担“当前用户配置模板”的角色。
- ChatConfigSnapshot 承担“历史事实配置快照”的角色。

### 2. 历史记录不再依赖 live 表

迁移完成后，历史事实链路应为：

ChatTurn -> ChatConfigSnapshot -> ModelSnapshot

而不是：

ChatTurn -> ChatConfig -> Model

### 3. live 引用继续保留在原表

以下路径仍应保留对 live Model 的引用，不纳入历史快照链：

- Chat -> ChatSpan -> ChatConfig -> Model
- ChatPreset -> ChatPresetSpan -> ChatConfig -> Model

## 迁移前提与现实限制

### 1. 现有历史模型版本不可追溯

当前库中并没有 Model 的版本历史表，因此对历史 ChatTurn 来说，只能基于“迁移执行时刻的 Model 当前值”生成第一版 ModelSnapshot。

这意味着：

- 迁移后可以保证从那一刻开始历史数据不再受 live Model 变更影响。
- 迁移前已经发生过的历史模型变更无法被精确还原。

这个限制需要在迁移评审中明确接受。

### 2. ChatConfigArchived 现状不足以承载历史事实

当前 ChatConfigArchived 仅是 ChatConfig 的 1:1 扩展，只保存 HashCode，无法独立表达完整历史配置。因此不能继续沿用为最终事实表。

从目标数据模型上看：

- ChatConfigSnapshot 已经承担了它原本试图承载的“配置归档/快照”职责。
- ChatConfigArchived 不再参与新快照迁移逻辑。
- 在本次迁移收口阶段，ChatConfigArchived 应被删除。

## 目标表结构

## 1. 新增 ModelSnapshot 表

建议字段：

- Id
- Name
- DeploymentName
- ModelKeyId
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

- ModelSnapshot 的目标是承载历史事实，不要求长期保留来源 Model.Id。
- 如果迁移过程中需要从旧 Model.Id 找到新 ModelSnapshot.Id，建议使用临时映射表或中间结果，而不是将来源字段固化到最终表结构中。
- 若后续希望保留 provider 名称、key 名称等展示字段，也建议直接冗余存入，而不是依赖外键回查。

## 2. 新增 ChatConfigSnapshot 表

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

## 3. 将 ChatTurn 改为引用 ChatConfigSnapshot

建议目标结构：

- ChatTurn.ChatConfigSnapshotId

说明：

- ChatTurn 作为事实表，应从 ChatConfig 切换为引用 ChatConfigSnapshot。
- 迁移执行时可以先增加可空的 ChatConfigSnapshotId 用于回填。
- 在完成回填与校验后，应删除旧的 ChatTurn.ChatConfigId 及其外键。

## 迁移阶段

## 阶段 0：迁移前盘点

执行以下盘点查询，产出基线数据：

- Model 总数
- ChatConfig 总数
- ChatTurn 中 ChatConfigId 非空总数
- ChatConfigMcp 总数
- ChatSpan / ChatPresetSpan 对各 Model 的引用分布
- ChatTurn 对各 ChatConfig 的引用分布

产出物：

- 一份迁移基线报告
- 一份异常数据清单

重点检查：

- 是否存在 ChatTurn.ChatConfigId 指向不存在 ChatConfig 的脏数据
- 是否存在 ChatConfig.ModelId 指向不存在 Model 的脏数据

## 阶段 1：扩表，不改旧数据

先发布纯 schema migration：

- 创建 ModelSnapshot
- 创建 ChatConfigSnapshot
- 为 ChatTurn 增加 ChatConfigSnapshotId nullable
- 增加必要索引和约束

建议索引：

- ChatConfigSnapshot(HashCode) where HashCode is not null
- ChatConfigSnapshot(ModelSnapshotId, HashCode) where HashCode is not null
- ChatTurn(ChatConfigSnapshotId)

建议唯一约束：

- 唯一索引：ChatConfigSnapshot(ModelSnapshotId, HashCode) where HashCode is not null

说明：

- ModelSnapshot 作为事实表可以不做内容去重，迁移阶段只需保证同一条旧 Model 记录能稳定映射到一条新快照记录。
- 由于迁移阶段会存在大量 HashCode 为 null 的历史记录，索引和唯一性约束应采用 filtered index，而不是对全表生效的普通唯一约束。

- 这一阶段不移动任何旧数据，确保 schema 变更可独立发布。

## 阶段 2：回填 ModelSnapshot

### 2.1 生成模型快照源数据

从当前 Model 全量扫描，逐条生成 ModelSnapshot 候选记录。

每条 Model 记录生成一条候选快照记录，字段取值来自迁移时刻的 Model 当前值。

### 2.2 直接插入

不做内容去重，直接写入 ModelSnapshot。

建议保留一张临时映射表或中间结果：

- ModelId
- ModelSnapshotId

该映射在后续 ChatConfigSnapshot 回填阶段会被复用。

### 2.3 校验

校验项：

- 每个现存 Model 都能映射到一个 ModelSnapshot
- ModelSnapshot 记录数应等于迁移时参与回填的 Model 记录数
- 临时映射表中不应出现一条 Model 记录对应多个 ModelSnapshot 的异常

## 阶段 3：回填 ChatConfigSnapshot

### 3.1 构造候选快照

从 ChatConfig 全量扫描，关联：

- ChatConfigMcp 获取已启用 MCP 名称列表
- 阶段 2 生成的 Model -> ModelSnapshot 映射

每条 ChatConfig 生成一条候选 ChatConfigSnapshot：

- 普通字段直接复制
- ModelId 替换为 ModelSnapshotId
- 将启用的 MCP 名称按稳定顺序排序后拼成逗号分隔字符串，写入 EnabledMcpNames
- HashCode 统一填 null

### 3.2 哈希策略

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

### 3.3 直接插入

迁移阶段不依赖 HashCode 去重，直接插入 ChatConfigSnapshot。

建议保留第二张临时映射表：

- ChatConfigId
- ChatConfigSnapshotId

### 3.4 校验

校验项：

- 每个被 ChatTurn 引用到的 ChatConfig 都能映射到一个 ChatConfigSnapshot
- 每个 ChatConfigSnapshot 都能找到对应的 ModelSnapshot
- 迁移批次插入的 ChatConfigSnapshot，其 HashCode 应全部为 null
- EnabledMcpNames 为空的记录只应来自没有启用 MCP 的 ChatConfig
- EnabledMcpNames 非空的记录应能与原 ChatConfigMcp 按名称集合对应上

## 阶段 4：回填 ChatTurn.ChatConfigSnapshotId

### 4.1 回填规则

对所有 ChatTurn.ChatConfigId 非空的记录：

- 通过 ChatConfigId -> ChatConfigSnapshotId 临时映射表回填 ChatConfigSnapshotId

对 ChatTurn.ChatConfigId 为空的记录：

- 保持 ChatConfigSnapshotId 为空

### 4.2 校验

必须满足：

- 回填后，所有原本 ChatConfigId 非空的 ChatTurn，其 ChatConfigSnapshotId 也非空
- 回填条数与基线统计一致
- 不允许出现 ChatConfigId 非空但找不到 ChatConfigSnapshotId 的漏数

建议输出核对结果：

- 原始 ChatTurn 引用数
- 成功回填数
- 漏回填数
- 漏回填样本清单

## 阶段 5：建立约束前核对

在不删除旧列的前提下，执行一轮全量一致性检查：

- ChatTurn.ChatConfigId -> ChatConfig -> Model
- ChatTurn.ChatConfigSnapshotId -> ChatConfigSnapshot -> ModelSnapshot

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
- 但在首次迁移回填时，两边应尽量一致。
- 由于不保留 custom headers，核对范围只包括 MCP 名称集合，不包括 headers 内容。

## 阶段 6：迁移收口

本阶段完成最终数据结构收口。

应完成：

- 为 ChatTurn.ChatConfigSnapshotId 建外键
- 为 ChatTurn.ChatConfigSnapshotId 建非空约束
- 删除 ChatTurn.ChatConfigId
- 删除 ChatConfigArchived

不应在本阶段删除：

- 删除 ChatConfig -> Model 的外键

说明：

- ChatSpan 与 ChatPresetSpan 仍然需要通过 ChatConfig 关联 live Model，因此 ChatConfig -> Model 的外键仍应保留。
- 本次迁移收口的核心是让 ChatTurn 完全脱离 ChatConfig，并让 ChatConfigArchived 退出数据模型。

## 建议的数据迁移顺序

推荐拆成 4 次独立发布：

1. 扩表发布
2. ModelSnapshot 回填发布
3. ChatConfigSnapshot 与 ChatTurn 回填发布
4. 一致性校验与收口发布

这样做的好处是：

- 每一步都可独立回滚
- 异常定位范围更小
- 对线上锁表时间更友好

## 回滚策略

### 1. 阶段 1 回滚

如果只是扩表失败，直接回滚 schema migration。

### 2. 阶段 2-4 回滚

建议所有回填脚本满足幂等：

- 重复执行不会产生重复快照
- 重复执行不会覆盖已确认正确的数据

若回填结果异常，可采用以下方式回滚：

- 清空新表中的本次批次数据
- 清空 ChatTurn.ChatConfigSnapshotId
- 保留旧表和旧外键不变

前提是：

- 尚未对新列加非空约束
- 尚未删除旧的 ChatTurn.ChatConfigId 与 ChatConfigArchived

## 数据校验清单

迁移完成后，至少执行以下校验：

- ModelSnapshot 总数等于迁移时参与回填的 Model 记录数
- ChatConfigSnapshot 总数等于迁移时参与回填的 ChatConfig 记录数
- ChatTurn 中 ChatConfigId 非空数等于 ChatConfigSnapshotId 非空数
- 每个 ChatConfigSnapshot 都存在合法 ModelSnapshotId
- 迁移批次插入的 ChatConfigSnapshot，其 HashCode 都为 null
- 每条 ChatConfigSnapshot 的 EnabledMcpNames 都满足 null 或逗号分隔字符串格式
- 任意抽样一批 ChatTurn，快照内容与原 ChatConfig 内容一致

## 风险与注意事项

### 1. 最大风险：历史事实只能从当前值补建

由于缺少既往版本历史，迁移生成的第一批快照本质上是“迁移时刻快照”，不是“消息发送时刻快照”。这是本方案最大的历史精度损失。

### 2. HashCode 语义变化风险

由于 ChatConfigSnapshot 不再保留 MCP 明细和 CustomHeaders，需要确保：

- 旧 ChatConfigArchived.HashCode 不再参与新快照的唯一性或查重逻辑
- 新哈希算法只覆盖最终实际落表字段
- 迁移阶段允许 HashCode 为 null，运行期索引与唯一性约束应只针对非 null 数据生效
- 后续如果补算历史 HashCode，必须复用与运行期完全一致的算法

### 3. MCP 名称归一化风险

ChatConfigSnapshot 去重时，EnabledMcpNames 必须先按稳定顺序归一化，否则相同名称集合可能被误判为不同快照。

### 4. 历史精度下降风险

由于不保留 custom headers，迁移后历史事实只保留“启用了哪些 MCP”，不再保留“以什么 headers 调用 MCP”。这属于有意接受的信息损失，需要在方案评审中明确。

### 5. 大表回填锁风险

如果 ChatTurn 数据量较大，回填 ChatConfigSnapshotId 应采用分批更新，避免长事务和锁升级。

## 后续建议

在完成本次数据迁移后，下一阶段应优先评估以下事项：

1. 将 ChatTurn 业务读写切换到 ChatConfigSnapshot。
2. 评估 UsageTransaction / UserModelUsage 是否也应改接 ModelSnapshot。
3. 在确认 live 引用仅剩 ChatSpan / ChatPresetSpan / 授权类表后，重新定义 Model 硬删除规则。
