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
    [ToolCallId] [varchar](100) NULL,
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
    [ToolCallId] [varchar](100) NULL,
    [Response] [nvarchar](max) NOT NULL,
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
    [RequireApproval] BIT            CONSTRAINT [DEFAULT_Mcp_RequireApproval] DEFAULT 0 NOT NULL,
    [Headers]         NVARCHAR (MAX) NULL,
    [CreatedAt]       DATETIME2 (7)  CONSTRAINT [DEFAULT_Mcp_CreatedAt] DEFAULT SYSUTCDATETIME() NOT NULL,
    [IsPublic]        BIT        CONSTRAINT [DEFAULT_Mcp_Public] DEFAULT 0 NOT NULL,
    CONSTRAINT [PK_McpServer] PRIMARY KEY CLUSTERED ([Id] ASC)
);

CREATE TABLE [UserMcp] (
    [Id]              INT            IDENTITY (1, 1) NOT NULL,
    [McpServerId]     INT            NOT NULL,
    [UserId]          INT            NOT NULL,
    CONSTRAINT [PK_UserMcp] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserMcp_McpServer] FOREIGN KEY ([McpServerId]) REFERENCES [McpServer] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserMcp_User] FOREIGN KEY ([UserId]) REFERENCES [User] ([Id]),
    INDEX [IX_UserMcp_McpServerId] ([McpServerId]),
    INDEX [IX_UserMcp_UserId] ([UserId])
);

CREATE TABLE [ChatConfigMcp] (
    [Id]              INT            IDENTITY (1, 1) NOT NULL,
    [ChatConfigId]    INT            NOT NULL,
    [McpServerId]     INT            NOT NULL,
    [Headers]         NVARCHAR (MAX) NULL,
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