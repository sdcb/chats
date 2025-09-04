-- 将UserModelUsage表的UserModelId字段改成UserId和ModelId，ModelId不再引用UserModel表——而引用Model表，UserId引用User表

-- 1. 首先添加新字段
ALTER TABLE [dbo].[UserModelUsage] ADD
    [UserId] [int] NULL,
    [ModelId] [smallint] NULL
GO

-- 2. 通过UserModel表更新新字段的值
UPDATE u
SET u.UserId = m.UserId,
    u.ModelId = m.ModelId
FROM [dbo].[UserModelUsage] u
INNER JOIN [dbo].[UserModel] m ON u.UserModelId = m.Id
GO

-- 3. 删除原有的外键约束/原有的索引
ALTER TABLE [dbo].[UserModelUsage] DROP CONSTRAINT [FK_ModelUsage_UserModel2]
DROP INDEX [IX_ModelUsage_UserModelId] ON [dbo].[UserModelUsage]
GO

-- 4. 添加新的外键约束
ALTER TABLE [dbo].[UserModelUsage] 
ADD CONSTRAINT [FK_UserModelUsage_User] FOREIGN KEY([UserId])
REFERENCES [dbo].[User] ([Id])
GO

ALTER TABLE [dbo].[UserModelUsage] 
ADD CONSTRAINT [FK_UserModelUsage_Model] FOREIGN KEY([ModelId])
REFERENCES [dbo].[Model] ([Id])
GO

-- 5. 将新字段设置为非空
ALTER TABLE [dbo].[UserModelUsage] ALTER COLUMN [UserId] [int] NOT NULL
ALTER TABLE [dbo].[UserModelUsage] ALTER COLUMN [ModelId] [smallint] NOT NULL
GO

-- 6. 删除旧的UserModelId字段
ALTER TABLE [dbo].[UserModelUsage] DROP COLUMN [UserModelId]
GO

-- 7. 创建新的索引
CREATE NONCLUSTERED INDEX [IX_UserModelUsage_UserId] ON [dbo].[UserModelUsage]
(
    [UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

CREATE NONCLUSTERED INDEX [IX_UserModelUsage_ModelId] ON [dbo].[UserModelUsage]
(
    [ModelId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO






-- 将UsageTransaction表的UserModelId字段改成ModelId（这个表已经有一个CreateUserId了），ModelId不再引用UserModel表——而引用Model表
-- 1. 添加新字段
ALTER TABLE [dbo].[UsageTransaction] ADD
    [ModelId] [smallint] NULL
GO

-- 2. 通过UserModel表更新新字段的值
UPDATE t
SET t.ModelId = m.ModelId
FROM [dbo].[UsageTransaction] t
INNER JOIN [dbo].[UserModel] m ON t.UserModelId = m.Id
GO

-- 3. 删除原有的外键约束和索引
ALTER TABLE [dbo].[UsageTransaction] DROP CONSTRAINT [FK_UsageTransaction_UserModel]
DROP INDEX [IX_UsageTransaction_UserModelId] ON [dbo].[UsageTransaction]
GO

-- 4. 添加新的外键约束
ALTER TABLE [dbo].[UsageTransaction] 
ADD CONSTRAINT [FK_UsageTransaction_Model] FOREIGN KEY([ModelId])
REFERENCES [dbo].[Model] ([Id])
GO

-- 5. 将新字段设置为非空
ALTER TABLE [dbo].[UsageTransaction] ALTER COLUMN [ModelId] [smallint] NOT NULL
GO

-- 6. 删除旧的UserModelId字段
ALTER TABLE [dbo].[UsageTransaction] DROP COLUMN [UserModelId]
GO

-- 7. 创建新的索引
CREATE NONCLUSTERED INDEX [IX_UsageTransaction_ModelId] ON [dbo].[UsageTransaction]
(
    [ModelId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO



-- 将UserModel表的IsDeleted字段删除（因为将UserModelUsage/UsageTransaction两个表外键引用去掉之后，UserModel表之后就可以安全删除了）
-- 1. 首先删除所有标记为已删除的记录
DELETE FROM [dbo].[UserModel]
WHERE [IsDeleted] = 1
GO

-- 2. 删除默认值约束（如果存在）
DECLARE @ConstraintName nvarchar(200)
SELECT @ConstraintName = dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE c.object_id = OBJECT_ID('UserModel')
AND c.name = 'IsDeleted'

IF @ConstraintName IS NOT NULL
    EXEC('ALTER TABLE [dbo].[UserModel] DROP CONSTRAINT ' + @ConstraintName)
GO

-- 3. 删除 IsDeleted 字段
ALTER TABLE [dbo].[UserModel] DROP COLUMN [IsDeleted]
GO




-- Tool Call table structure
INSERT INTO [dbo].[ChatRole] ([Id], [Name]) VALUES (4, N'tool');
GO

INSERT INTO [dbo].[MessageContentType] ([Id], [ContentType]) VALUES
(4, 'toolCall'),
(5, 'toolCallResponse');
GO


CREATE TABLE [dbo].[MessageContentToolCall](
    [Id] [bigint] NOT NULL,
    [ToolCallId] [varchar](100) NOT NULL,
    [Name] [nvarchar](200) NOT NULL,
    [Parameters] [nvarchar](max) NOT NULL,
 CONSTRAINT [PK_MessageContentToolCall] PRIMARY KEY CLUSTERED 
(
    [Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[MessageContentToolCall]  WITH CHECK ADD  CONSTRAINT [FK_MessageContentToolCall_MessageContent] FOREIGN KEY([Id])
REFERENCES [dbo].[MessageContent] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [dbo].[MessageContentToolCall] CHECK CONSTRAINT [FK_MessageContentToolCall_MessageContent]
GO


CREATE TABLE [dbo].[MessageContentToolCallResponse](
    [Id] [bigint] NOT NULL,
    [ToolCallId] [varchar](100) NOT NULL,
    [IsSuccess] [bit] NOT NULL,
    [Response] [nvarchar](max) NOT NULL,
    [DurationMs] [int] NOT NULL,
 CONSTRAINT [PK_MessageContentToolCallResponse] PRIMARY KEY CLUSTERED 
(
    [Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

ALTER TABLE [dbo].[MessageContentToolCallResponse]  WITH CHECK ADD  CONSTRAINT [FK_MessageContentToolCallResponse_MessageContent] FOREIGN KEY([Id])
REFERENCES [dbo].[MessageContent] ([Id])
ON DELETE CASCADE
GO

ALTER TABLE [dbo].[MessageContentToolCallResponse] CHECK CONSTRAINT [FK_MessageContentToolCallResponse_MessageContent]
GO





DROP TABLE IF EXISTS [UserMcp];
DROP TABLE IF EXISTS [ChatConfigMcp];
DROP TABLE IF EXISTS [McpServer];
ALTER TABLE [ChatConfig] DROP CONSTRAINT IF EXISTS [FK_ChatConfig_ImageSize];
ALTER TABLE [ChatConfig] DROP CONSTRAINT IF EXISTS [DF_ChatConfig_ImageSizeId];
ALTER TABLE [ChatConfig] DROP COLUMN IF EXISTS [ImageSizeId];
DROP TABLE IF EXISTS [KnownImageSize];

CREATE TABLE [McpServer] (
    [Id]              INT            IDENTITY (1, 1) NOT NULL,
    [Label]           NVARCHAR (50)  NOT NULL,
    [Url]             NVARCHAR (300) NOT NULL,
    [Headers]         NVARCHAR (MAX) NULL,
    [CreatedAt]       DATETIME2 (7)  CONSTRAINT [DEFAULT_Mcp_CreatedAt] DEFAULT SYSUTCDATETIME() NOT NULL,
    [OwnerUserId]     INT            NOT NULL,
    [IsSystem]        BIT            CONSTRAINT [DEFAULT_Mcp_IsSystem] DEFAULT 0 NOT NULL,
    [LastFetchAt]     DATETIME2 (7)  NULL,
    CONSTRAINT [PK_McpServer] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_McpServer_User] FOREIGN KEY ([OwnerUserId]) REFERENCES [User] ([Id]),
    INDEX [IX_McpServer_OwnerUserId] ([OwnerUserId]) WHERE [OwnerUserId] IS NOT NULL
);

CREATE TABLE [dbo].[McpTool] (
    [Id]              INT            IDENTITY (1, 1) NOT NULL,
    [McpServerId]     INT            NOT NULL,
    [ToolName]        NVARCHAR (100) NOT NULL,
    [Description]     NVARCHAR (MAX) NULL,
    [Parameters]      NVARCHAR (MAX) NULL, -- 存储参数的 JSON Schema
    [RequireApproval] BIT            CONSTRAINT [DEFAULT_Mcp_RequireApproval] DEFAULT 0 NOT NULL,

    CONSTRAINT [PK_McpTool] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_McpTool_McpServer] FOREIGN KEY ([McpServerId]) REFERENCES [dbo].[McpServer] ([Id]) ON DELETE CASCADE, -- 级联删除
    CONSTRAINT [UX_McpTool_Server_Name] UNIQUE ([McpServerId], [ToolName]) -- 同一个 Server 内工具名唯一
);

CREATE TABLE [UserMcp] (
    [Id]              INT            IDENTITY (1, 1) NOT NULL,
    [McpServerId]     INT            NOT NULL,
    [CustomHeaders]   NVARCHAR (MAX) NULL,
    [UserId]          INT            NOT NULL,
    CONSTRAINT [PK_UserMcp] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserMcp_McpServer] FOREIGN KEY ([McpServerId]) REFERENCES [McpServer] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserMcp_User] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]),
    CONSTRAINT [UX_UserMcp_User_Server] UNIQUE NONCLUSTERED ([UserId], [McpServerId]),
    INDEX [IX_UserMcp_McpServerId] ([McpServerId])
);

CREATE TABLE [ChatConfigMcp] (
    [Id]              INT            IDENTITY (1, 1) NOT NULL,
    [ChatConfigId]    INT            NOT NULL,
    [McpServerId]     INT            NOT NULL,
    [CustomHeaders]   NVARCHAR (MAX) NULL,
    CONSTRAINT [PK_ChatConfigMcp] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ChatConfigMcp_ChatConfig] FOREIGN KEY ([ChatConfigId]) REFERENCES [ChatConfig] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ChatConfigMcp_McpServer] FOREIGN KEY ([McpServerId]) REFERENCES [McpServer] ([Id]) ON DELETE CASCADE,
    INDEX [IX_ChatConfigMcp_ChatConfigId] ([ChatConfigId]),
    INDEX [IX_ChatConfigMcp_McpServerId] ([McpServerId])
);

CREATE TABLE [KnownImageSize] (
    [Id]              SMALLINT       NOT NULL,
    [Width]           SMALLINT       NOT NULL,
    [Height]          SMALLINT       NOT NULL,
    CONSTRAINT [PK_KnownImageSize] PRIMARY KEY CLUSTERED ([Id] ASC)
);
INSERT INTO [KnownImageSize] VALUES
(0, 0,    0),
(1, 1024, 1024),
(2, 1536, 1024),
(3, 1024, 1536);

ALTER TABLE [ChatConfig] ADD [ImageSizeId] SMALLINT NOT NULL CONSTRAINT [DF_ChatConfig_ImageSizeId] DEFAULT 0;
ALTER TABLE [ChatConfig] ADD CONSTRAINT [FK_ChatConfig_ImageSize] FOREIGN KEY ([ImageSizeId]) REFERENCES [KnownImageSize] ([Id]);







PRINT N'1) 重命名 Message -> ChatTurn 以及 MessageContent* -> Step* ...';
-- 表重命名（外键仍然有效，名称不改不影响引用）
EXEC sp_rename 'dbo.Message',                         'ChatTurn';
EXEC sp_rename 'dbo.MessageContent',                  'StepContent';
EXEC sp_rename 'dbo.MessageContentBlob',              'StepContentBlob';
EXEC sp_rename 'dbo.MessageContentFile',              'StepContentFile';
EXEC sp_rename 'dbo.MessageContentText',              'StepContentText';
EXEC sp_rename 'dbo.MessageContentToolCall',          'StepContentToolCall';
EXEC sp_rename 'dbo.MessageContentToolCallResponse',  'StepContentToolCallResponse';
EXEC sp_rename 'dbo.MessageContentType',              'StepContentType';

PRINT N'2) ChatTurn 增加列 IsUser、ReactionId、ChatConfigId，并填充 IsUser 与 Reaction/Config';
-- 新列（IsUser 默认0，非空；其他可空）
ALTER TABLE dbo.ChatTurn ADD
    IsUser       bit      NOT NULL CONSTRAINT DF_ChatTurn_IsUser DEFAULT(0),
    ReactionId   bit      NULL,
    ChatConfigId int      NULL;

-- 2=user，其余均视为非用户
UPDATE dbo.ChatTurn
    SET IsUser = CASE WHEN ChatRoleId = 2 THEN 1 ELSE 0 END;

-- ReactionId/ChatConfigId 迁移到 ChatTurn
UPDATE ct
    SET ct.ReactionId   = mr.ReactionId,
        ct.ChatConfigId = mr.ChatConfigId
    FROM dbo.ChatTurn ct
    LEFT JOIN dbo.MessageResponse mr
    ON mr.MessageId = ct.Id;

-- ChatTurn.ChatConfigId 外键（若不存在则创建）
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_ChatTurn_ChatConfig')
BEGIN
    ALTER TABLE dbo.ChatTurn
    WITH CHECK ADD CONSTRAINT FK_ChatTurn_ChatConfig
    FOREIGN KEY (ChatConfigId) REFERENCES dbo.ChatConfig(Id);
    ALTER TABLE dbo.ChatTurn CHECK CONSTRAINT FK_ChatTurn_ChatConfig;
END

PRINT N'3) 创建 Step 表（承载 ChatRoleId/Edited/CreatedAt/UsageId）并迁移数据';
-- 新建 Step（含默认与必要外键）
IF OBJECT_ID('dbo.Step','U') IS NULL
BEGIN
    CREATE TABLE dbo.Step
    (
        Id          bigint IDENTITY(1,1) NOT NULL,
        TurnId      bigint NOT NULL,
        ChatRoleId  tinyint NOT NULL,
        Edited      bit NOT NULL CONSTRAINT DF_Step_Edited DEFAULT(0),
        CreatedAt   datetime2(7) NOT NULL,
        UsageId     bigint NULL,
        CONSTRAINT PK_Step PRIMARY KEY CLUSTERED (Id ASC),
        CONSTRAINT FK_Step_Turn   FOREIGN KEY (TurnId)     REFERENCES dbo.ChatTurn(Id)      ON DELETE CASCADE,
        CONSTRAINT FK_Step_Usage  FOREIGN KEY (UsageId)    REFERENCES dbo.UserModelUsage(Id),
        CONSTRAINT FK_Step_ChatRole FOREIGN KEY (ChatRoleId) REFERENCES dbo.ChatRole(Id)
    );
END

-- 为每个 Turn 插入“初始 Step”，UsageId 来自 MessageResponse
INSERT INTO dbo.Step (TurnId, ChatRoleId, Edited, CreatedAt, UsageId)
SELECT
    ct.Id,
    ct.ChatRoleId,
    ct.Edited,
    ct.CreatedAt,
    mr.UsageId
FROM dbo.ChatTurn ct
LEFT JOIN dbo.MessageResponse mr
    ON mr.MessageId = ct.Id
ORDER BY ct.Id;

PRINT N'4) StepContent 挂载到 Step：新增 StepId、回填、建FK并删除旧列 MessageId';
-- 删除 StepContent -> ChatTurn 的旧FK（重命名后名字仍为 FK_MessageContent_Message）
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_MessageContent_Message')
BEGIN
    ALTER TABLE dbo.StepContent DROP CONSTRAINT FK_MessageContent_Message;
END

-- 新增 StepId（临时可空）
IF COL_LENGTH('dbo.StepContent', 'StepId') IS NULL
BEGIN
    ALTER TABLE dbo.StepContent ADD StepId bigint NULL;
END

-- 回填 StepId：以 Step.TurnId == StepContent.MessageId 关联
;WITH S AS
(
    SELECT s.Id AS StepId, s.TurnId
        FROM dbo.Step s
)
UPDATE sc
    SET sc.StepId = s.StepId
    FROM dbo.StepContent sc
    JOIN S
    ON s.TurnId = sc.MessageId;

-- 校验：不应存在未回填成功的数据
IF EXISTS (SELECT 1 FROM dbo.StepContent WHERE StepId IS NULL)
BEGIN
    RAISERROR(N'存在无法回填 StepId 的 StepContent 记录', 16, 1);
END

-- StepId 设为 NOT NULL，并建立新外键
ALTER TABLE dbo.StepContent ALTER COLUMN StepId bigint NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_StepContent_Step')
BEGIN
    ALTER TABLE dbo.StepContent
    WITH CHECK ADD CONSTRAINT FK_StepContent_Step
    FOREIGN KEY (StepId) REFERENCES dbo.Step(Id)
    ON UPDATE CASCADE
    ON DELETE CASCADE;
    ALTER TABLE dbo.StepContent CHECK CONSTRAINT FK_StepContent_Step;
END

-- 删除旧列 MessageId
IF COL_LENGTH('dbo.StepContent', 'MessageId') IS NOT NULL
BEGIN
    DROP INDEX [IX_MessageContent2_Message] ON [dbo].[StepContent];
    ALTER TABLE dbo.StepContent DROP COLUMN MessageId;
END

-- 性能：为 StepContent.StepId 建索引
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_StepContent_StepId' AND object_id = OBJECT_ID('dbo.StepContent'))
BEGIN
    CREATE INDEX IX_StepContent_StepId ON dbo.StepContent(StepId);
END

PRINT N'5) 从 ChatTurn 移除已下沉到 Step 的列（ChatRoleId/Edited/CreatedAt）';
-- 删除 ChatTurn 上可能残留的外键（原 FK_Message_ChatRole）
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Message_ChatRole')
BEGIN
    ALTER TABLE dbo.ChatTurn DROP CONSTRAINT FK_Message_ChatRole;
END

-- 删除 Edited 的默认约束
ALTER TABLE dbo.ChatTurn DROP CONSTRAINT DF_Message_Edited;

-- 正式移除三列
IF COL_LENGTH('dbo.ChatTurn', 'ChatRoleId') IS NOT NULL
    ALTER TABLE dbo.ChatTurn DROP COLUMN ChatRoleId;
IF COL_LENGTH('dbo.ChatTurn', 'Edited') IS NOT NULL
    ALTER TABLE dbo.ChatTurn DROP COLUMN Edited;
IF COL_LENGTH('dbo.ChatTurn', 'CreatedAt') IS NOT NULL
    ALTER TABLE dbo.ChatTurn DROP COLUMN CreatedAt;

PRINT N'6) 删除 MessageResponse 表（Reaction/Config/Usage 已迁移）';
IF OBJECT_ID('dbo.MessageResponse','U') IS NOT NULL
BEGIN
    DROP TABLE dbo.MessageResponse;
END

PRINT N'7) 辅助索引：为 Step(TurnId, Id) 建索引';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Step_TurnId' AND object_id = OBJECT_ID('dbo.Step'))
BEGIN
    CREATE INDEX IX_Step_TurnId ON dbo.Step(TurnId, Id);
END

PRINT N'8) 将Chat表的LeafMessageId改名为LeafTurnId';
IF COL_LENGTH('dbo.Chat', 'LeafMessageId') IS NOT NULL
BEGIN
    EXEC sp_rename 'dbo.Chat.LeafMessageId', 'LeafTurnId', 'COLUMN';
END