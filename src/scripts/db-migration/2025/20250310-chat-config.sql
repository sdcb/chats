--DROP TABLE dbo.ChatConfig
CREATE TABLE dbo.ChatConfig
(
    Id             INT           NOT NULL IDENTITY (1, 1),
	HashCode       BIGINT        NOT NULL DEFAULT 0,
    ModelId        SMALLINT      NOT NULL,
    SystemPrompt   NVARCHAR(MAX) NULL,
    Temperature    REAL          NULL,
    WebSearchEnabled BIT           NOT NULL,
    MaxOutputTokens  INT           NULL,
    ReasoningEffort TINYINT      NULL,
);
GO

ALTER TABLE dbo.ChatConfig ADD CONSTRAINT PK_ChatConfig PRIMARY KEY CLUSTERED (Id);
CREATE NONCLUSTERED INDEX IX_ChatConfig_ModelId ON dbo.ChatConfig (ModelId);
GO

-- 为 HashCode 创建条件索引，仅当 HashCode 不等于 0 时才创建
CREATE NONCLUSTERED INDEX IX_ChatConfig_HashCode ON dbo.ChatConfig (HashCode) WHERE HashCode != 0;
GO

ALTER TABLE dbo.ChatConfig
    ADD CONSTRAINT FK_ChatConfig_Model FOREIGN KEY(ModelId)
	REFERENCES dbo.Model(Id)
		ON UPDATE NO ACTION
		ON DELETE NO ACTION;
GO

DROP TABLE IF EXISTS #TempChatSpan;

-- 创建临时表 #TempChatSpan，包含 RowNumber 字段
CREATE TABLE #TempChatSpan (
    RowNumber INT IDENTITY(1,1) PRIMARY KEY,  -- 自增列，从 1 开始
    ChatId INT,
    SpanId TINYINT,
    HashCode BIGINT,
    ModelId SMALLINT,
    Temperature REAL,
    WebSearchEnabled BIT,
    SystemPrompt NVARCHAR(MAX),
    MaxOutputTokens INT NULL,
    ReasoningEffort INT
);

-- 插入数据到临时表, RowNumber 会自动生成
INSERT INTO #TempChatSpan (ChatId, SpanId, HashCode, ModelId, Temperature, WebSearchEnabled, SystemPrompt, MaxOutputTokens, ReasoningEffort)
SELECT
    cs.ChatId,
    cs.SpanId,
    0 AS HashCode,
    cs.ModelId,
    cs.Temperature,
    cs.EnableSearch AS WebSearchEnabled,
    COALESCE(SystemPrompt.Content, DefaultPrompt.Content) AS SystemPrompt,
    NULL AS MaxOutputTokens,
    0 AS ReasoningEffort
FROM
    ChatSpan cs
INNER JOIN
    Chat c ON cs.ChatId = c.Id
OUTER APPLY (
    SELECT TOP 1 mct.Content
    FROM Message m
    INNER JOIN MessageContent mc ON m.Id = mc.MessageId
    INNER JOIN MessageContentText mct ON mc.Id = mct.Id
    WHERE m.ChatId = cs.ChatId AND (m.SpanId = cs.SpanId OR m.SpanId IS NULL) AND m.ChatRoleId = 1
    ORDER BY m.CreatedAt
) AS SystemPrompt
OUTER APPLY (
    SELECT p.Content
    FROM Prompt p
    WHERE p.IsSystem = 1 AND p.IsDefault = 1
) AS DefaultPrompt;

-- 验证插入的数据，包括 RowNumber
SELECT * FROM #TempChatSpan;

-- 开启 IDENTITY_INSERT，允许向自增列插入值
SET IDENTITY_INSERT dbo.ChatConfig ON;

-- 从临时表 #TempChatSpan 插入数据到 ChatConfig
TRUNCATE TABLE dbo.ChatConfig
INSERT INTO dbo.ChatConfig (Id, HashCode, ModelId, SystemPrompt, Temperature, WebSearchEnabled, MaxOutputTokens, ReasoningEffort)
SELECT
    RowNumber, -- 使用 RowNumber 作为 Id 的值
    t.HashCode,
    t.ModelId,
    t.SystemPrompt,
    t.Temperature,
    t.WebSearchEnabled,
    t.MaxOutputTokens,
    t.ReasoningEffort
FROM
    #TempChatSpan t;

-- 关闭 IDENTITY_INSERT
SET IDENTITY_INSERT dbo.ChatConfig OFF;

/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
BEGIN TRANSACTION
GO
ALTER TABLE dbo.ChatSpan DROP CONSTRAINT FK_ChatSpan_Chat
GO
ALTER TABLE dbo.Chat SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.ChatSpan DROP CONSTRAINT FK_ChatSpan_Model
GO
ALTER TABLE dbo.Model SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
CREATE TABLE dbo.Tmp_ChatSpan
	(
	ChatId int NOT NULL,
	SpanId tinyint NOT NULL,
	Enabled bit NOT NULL,
	ChatConfigId int NULL
	)  ON [PRIMARY]
GO
ALTER TABLE dbo.Tmp_ChatSpan SET (LOCK_ESCALATION = TABLE)
GO
ALTER TABLE dbo.Tmp_ChatSpan ADD CONSTRAINT
	DF_ChatSpan_Enabled DEFAULT 1 FOR Enabled
GO
IF EXISTS(SELECT * FROM dbo.ChatSpan)
	 EXEC('INSERT INTO dbo.Tmp_ChatSpan (ChatId, SpanId)
		SELECT ChatId, SpanId FROM dbo.ChatSpan WITH (HOLDLOCK TABLOCKX)')
GO
DROP TABLE dbo.ChatSpan
GO
EXECUTE sp_rename N'dbo.Tmp_ChatSpan', N'ChatSpan', 'OBJECT' 
GO
ALTER TABLE dbo.ChatSpan ADD CONSTRAINT
	PK_ChatSpan PRIMARY KEY CLUSTERED 
	(
	ChatId,
	SpanId
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE dbo.ChatSpan ADD CONSTRAINT FK_ChatSpan_ChatConfig FOREIGN KEY
	(
	ChatConfigId
	) REFERENCES dbo.ChatConfig
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION
GO
CREATE NONCLUSTERED INDEX IX_ChatSpan_ChatConfigId ON dbo.ChatSpan
	(
	ChatConfigId
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE dbo.ChatSpan ADD CONSTRAINT
	FK_ChatSpan_Chat FOREIGN KEY
	(
	ChatId
	) REFERENCES dbo.Chat
	(
	Id
	) ON UPDATE  CASCADE 
	 ON DELETE  CASCADE 
	
GO
ALTER TABLE dbo.ChatSpan ADD CONSTRAINT
	FK_ChatSpan_ChatSpan FOREIGN KEY
	(
	ChatId,
	SpanId
	) REFERENCES dbo.ChatSpan
	(
	ChatId,
	SpanId
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
COMMIT

-- 使用 CTE (Common Table Expression) 来更新 ChatSpan 表
WITH ChatSpanUpdate AS (
    SELECT 
        cs.ChatId,
        cs.SpanId,
        cs.ChatConfigId,
        t.RowNumber AS NewChatConfigId  -- 使用临时表中的 RowNumber 作为新的 ChatConfigId
    FROM 
        dbo.ChatSpan cs
    INNER JOIN 
        #TempChatSpan t ON cs.ChatId = t.ChatId AND cs.SpanId = t.SpanId
)
UPDATE 
    ChatSpanUpdate
SET 
    ChatConfigId = NewChatConfigId;

ALTER TABLE dbo.ChatSpan DROP CONSTRAINT FK_ChatSpan_ChatConfig;
DROP INDEX IX_ChatSpan_ChatConfigId ON dbo.ChatSpan;
ALTER TABLE dbo.ChatSpan ALTER COLUMN ChatConfigId INT NOT NULL;
ALTER TABLE dbo.ChatSpan ADD CONSTRAINT FK_ChatSpan_ChatConfig FOREIGN KEY (ChatConfigId) REFERENCES dbo.ChatConfig(Id) ON UPDATE NO ACTION ON DELETE NO ACTION;
CREATE NONCLUSTERED INDEX IX_ChatSpan_ChatConfigId ON dbo.ChatSpan (ChatConfigId);
GO

--DROP TABLE dbo.MessageResponse 
CREATE TABLE dbo.MessageResponse (
    MessageId BIGINT NOT NULL,
    UsageId BIGINT NOT NULL,
    ReactionId BIT NULL,  -- 假设 ReactionId 是 BIT 类型，如果不是，请调整
    ChatConfigId INT NOT NULL, -- 新增的 ChatConfigId 列
    CONSTRAINT PK_MessageResponse PRIMARY KEY CLUSTERED (MessageId),
    CONSTRAINT FK_MessageResponse_Message FOREIGN KEY (MessageId) REFERENCES dbo.Message(Id) ON DELETE CASCADE, -- 与 Message 表的一对一关系
    CONSTRAINT FK_MessageResponse_UserModelUsage FOREIGN KEY (UsageId) REFERENCES dbo.UserModelUsage(Id),
    CONSTRAINT FK_MessageResponse_ChatConfig FOREIGN KEY (ChatConfigId) REFERENCES dbo.ChatConfig(Id)
);

-- 添加索引以提高查询性能（可选，但建议）
CREATE NONCLUSTERED INDEX IX_MessageResponse_UsageId ON dbo.MessageResponse (UsageId);
CREATE NONCLUSTERED INDEX IX_MessageResponse_ChatConfigId ON dbo.MessageResponse (ChatConfigId);

INSERT INTO dbo.MessageResponse (MessageId, UsageId, ReactionId, ChatConfigId)
SELECT
    m.Id,
    m.UsageId,
    m.ReactionId,
    sub.ChatConfigId
FROM
    dbo.Message m
    CROSS APPLY (
        SELECT cc.Id AS ChatConfigId,
               ROW_NUMBER() OVER (ORDER BY
                                    CASE WHEN cs.SpanId = m.SpanId THEN 0 ELSE 1 END,
                                    cc.Id DESC
                                 ) as rn
        FROM dbo.ChatSpan cs
        INNER JOIN dbo.ChatConfig cc ON cs.ChatConfigId = cc.Id
        WHERE cs.ChatId = m.ChatId
    ) AS sub
WHERE m.ChatRoleId = 3
  AND sub.rn = 1;


ALTER TABLE dbo.Message DROP CONSTRAINT FK_Message_UserModelUsage;
DROP INDEX IX_Message_UsageId ON dbo.Message;
ALTER TABLE dbo.Message DROP COLUMN UsageId, ReactionId;
