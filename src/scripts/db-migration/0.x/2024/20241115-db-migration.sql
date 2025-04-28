/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
BEGIN TRANSACTION
SET QUOTED_IDENTIFIER ON
SET ARITHABORT ON
SET NUMERIC_ROUNDABORT OFF
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_ClientInfo
GO
ALTER TABLE dbo.ClientInfo SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_UsageTransactionLog
GO
ALTER TABLE dbo.UsageTransaction SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_TransactionLog
GO
ALTER TABLE dbo.BalanceTransaction SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_UserModel2
GO
ALTER TABLE dbo.UserModel SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
CREATE TABLE dbo.Tmp_UserModelUsage
	(
	Id bigint NOT NULL IDENTITY (1, 1),
	UserModelId int NOT NULL,
	SegmentCount smallint NOT NULL,
	InputTokens int NOT NULL,
	OutputTokens int NOT NULL,
	ReasoningTokens int NOT NULL,
	FirstResponseDurationMs int NOT NULL,
	DurationMs int NOT NULL,
	InputCost decimal(14, 8) NOT NULL,
	OutputCost decimal(14, 8) NOT NULL,
	BalanceTransactionId bigint NULL,
	UsageTransactionId bigint NULL,
	ClientInfoId int NOT NULL,
	CreatedAt datetime2(7) NOT NULL
	)  ON [PRIMARY]
GO
ALTER TABLE dbo.Tmp_UserModelUsage SET (LOCK_ESCALATION = TABLE)
GO
ALTER TABLE dbo.Tmp_UserModelUsage ADD CONSTRAINT
	DF_UserModelUsage_SegmentCount DEFAULT 0 FOR SegmentCount
GO
ALTER TABLE dbo.Tmp_UserModelUsage ADD CONSTRAINT
	DF_UserModelUsage_ReasoningTokens DEFAULT 0 FOR ReasoningTokens
GO
ALTER TABLE dbo.Tmp_UserModelUsage ADD CONSTRAINT
	DF_UserModelUsage_TTFBDurationMs DEFAULT 0 FOR FirstResponseDurationMs
GO
SET IDENTITY_INSERT dbo.Tmp_UserModelUsage ON
GO
IF EXISTS(SELECT * FROM dbo.UserModelUsage)
	 EXEC('INSERT INTO dbo.Tmp_UserModelUsage (Id, UserModelId, InputTokens, OutputTokens, DurationMs, InputCost, OutputCost, BalanceTransactionId, UsageTransactionId, ClientInfoId, CreatedAt)
		SELECT Id, UserModelId, InputTokenCount, OutputTokenCount, DurationMs, InputCost, OutputCost, BalanceTransactionId, UsageTransactionId, ClientInfoId, CreatedAt FROM dbo.UserModelUsage WITH (HOLDLOCK TABLOCKX)')
GO
SET IDENTITY_INSERT dbo.Tmp_UserModelUsage OFF
GO
ALTER TABLE dbo.UserApiUsage
	DROP CONSTRAINT FK_UserApiUsage_UserModelUsage
GO
ALTER TABLE dbo.Message
	DROP CONSTRAINT FK_Message2_UserModelUsage
GO
DROP TABLE dbo.UserModelUsage
GO
EXECUTE sp_rename N'dbo.Tmp_UserModelUsage', N'UserModelUsage', 'OBJECT' 
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	PK_ModelUsage PRIMARY KEY CLUSTERED 
	(
	Id
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

GO
CREATE NONCLUSTERED INDEX IX_ModelUsage_UserModelId ON dbo.UserModelUsage
	(
	UserModelId
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX IX_ModelUsage_BalanceTransaction ON dbo.UserModelUsage
	(
	BalanceTransactionId
	) WHERE ([BalanceTransactionId] IS NOT NULL)
 WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX IX_ModelUsage_UsageTransaction ON dbo.UserModelUsage
	(
	UsageTransactionId
	) WHERE ([UsageTransactionId] IS NOT NULL)
 WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX IX_ModelUsage_CreatedAt ON dbo.UserModelUsage
	(
	CreatedAt
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_UserModel2 FOREIGN KEY
	(
	UserModelId
	) REFERENCES dbo.UserModel
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_TransactionLog FOREIGN KEY
	(
	BalanceTransactionId
	) REFERENCES dbo.BalanceTransaction
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_UsageTransactionLog FOREIGN KEY
	(
	UsageTransactionId
	) REFERENCES dbo.UsageTransaction
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_ClientInfo FOREIGN KEY
	(
	ClientInfoId
	) REFERENCES dbo.ClientInfo
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.Message ADD CONSTRAINT
	FK_Message2_UserModelUsage FOREIGN KEY
	(
	UsageId
	) REFERENCES dbo.UserModelUsage
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.Message SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserApiUsage ADD CONSTRAINT
	FK_UserApiUsage_UserModelUsage FOREIGN KEY
	(
	UsageId
	) REFERENCES dbo.UserModelUsage
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserApiUsage SET (LOCK_ESCALATION = TABLE)
GO
COMMIT


/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
BEGIN TRANSACTION
SET QUOTED_IDENTIFIER ON
SET ARITHABORT ON
SET NUMERIC_ROUNDABORT OFF
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT DF_UserModelUsage_SegmentCount
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT DF_UserModelUsage_ReasoningTokens
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT DF_UserModelUsage_TTFBDurationMs
GO
ALTER TABLE dbo.UserModelUsage SET (LOCK_ESCALATION = TABLE)
GO
COMMIT


update ModelProvider set Host = 'https://<resource-name>.openai.azure.com/', ApiKey = '' where id = 1
update ModelProvider set Host = 'hunyuan.tencentcloudapi.com', ApiKey = '{"secretId":"", "secretKey":""}' where id = 2
update ModelProvider set ApiKey = '' where id = 3
update ModelProvider set ApiKey = '' where id = 4
update ModelProvider set Host = 'https://api.openai.com/v1', ApiKey = '' where id = 5
update ModelProvider set ApiKey = '{"apiKey":"", "secret":""}' where id = 6
update ModelProvider set ApiKey = '' where id = 7
update ModelProvider set ApiKey = '{"appId": "", "apiKey":"", "secret":""}' where id = 8
update ModelProvider set ApiKey = '' where id = 9


/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
BEGIN TRANSACTION
SET QUOTED_IDENTIFIER ON
SET ARITHABORT ON
SET NUMERIC_ROUNDABORT OFF
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
COMMIT
BEGIN TRANSACTION
GO
EXECUTE sp_rename N'dbo.ModelProvider.Host', N'Tmp_InitialHost_2', 'COLUMN' 
GO
EXECUTE sp_rename N'dbo.ModelProvider.ApiKey', N'Tmp_InitialSecret_3', 'COLUMN' 
GO
EXECUTE sp_rename N'dbo.ModelProvider.Tmp_InitialHost_2', N'InitialHost', 'COLUMN' 
GO
EXECUTE sp_rename N'dbo.ModelProvider.Tmp_InitialSecret_3', N'InitialSecret', 'COLUMN' 
GO
ALTER TABLE dbo.ModelProvider
	DROP COLUMN DisplayName, Icon, InitialConfig
GO
ALTER TABLE dbo.ModelProvider SET (LOCK_ESCALATION = TABLE)
GO
COMMIT


/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
BEGIN TRANSACTION
SET QUOTED_IDENTIFIER ON
SET ARITHABORT ON
SET NUMERIC_ROUNDABORT OFF
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
COMMIT
BEGIN TRANSACTION
GO
CREATE TABLE dbo.Tmp_ModelProvider
	(
	Id smallint NOT NULL,
	Name varchar(50) NOT NULL,
	InitialHost varchar(500) NULL,
	InitialSecret varchar(500) NULL
	)  ON [PRIMARY]
GO
ALTER TABLE dbo.Tmp_ModelProvider SET (LOCK_ESCALATION = TABLE)
GO
DECLARE @v sql_variant 
SET @v = N'JSON'
EXECUTE sp_addextendedproperty N'MS_Description', @v, N'SCHEMA', N'dbo', N'TABLE', N'Tmp_ModelProvider', NULL, NULL
GO
IF EXISTS(SELECT * FROM dbo.ModelProvider)
	 EXEC('INSERT INTO dbo.Tmp_ModelProvider (Id, Name, InitialHost, InitialSecret)
		SELECT Id, Name, InitialHost, InitialSecret FROM dbo.ModelProvider WITH (HOLDLOCK TABLOCKX)')
GO
ALTER TABLE dbo.ModelKey
	DROP CONSTRAINT FK_ModelKey2_ModelProvider
GO
ALTER TABLE dbo.ModelReference
	DROP CONSTRAINT FK_ModelSetting_ModelProvider
GO
DROP TABLE dbo.ModelProvider
GO
EXECUTE sp_rename N'dbo.Tmp_ModelProvider', N'ModelProvider', 'OBJECT' 
GO
ALTER TABLE dbo.ModelProvider ADD CONSTRAINT
	PK_ModelProvider PRIMARY KEY CLUSTERED 
	(
	Id
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

GO
CREATE NONCLUSTERED INDEX IX_ModelProvider ON dbo.ModelProvider
	(
	Name
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.ModelReference ADD CONSTRAINT
	FK_ModelSetting_ModelProvider FOREIGN KEY
	(
	ProviderId
	) REFERENCES dbo.ModelProvider
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.ModelReference SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.ModelKey ADD CONSTRAINT
	FK_ModelKey2_ModelProvider FOREIGN KEY
	(
	ModelProviderId
	) REFERENCES dbo.ModelProvider
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.ModelKey SET (LOCK_ESCALATION = TABLE)
GO
COMMIT

INSERT INTO dbo.ModelProvider VALUES(0, 'Test', NULL, NULL)

INSERT INTO ModelReference 
    (Id, ProviderId, Name, MinTemperature, MaxTemperature, AllowSearch, AllowVision, AllowSystemPrompt, AllowStreaming, ContextWindow, MaxResponseTokens, TokenizerId, PromptTokenPrice1M, ResponseTokenPrice1M, CurrencyCode)
VALUES 
    (0, 0, 'Test', 0, 2, 1, 1, 1, 1, 2048, 2048, 1, 0, 0, 'RMB');

UPDATE [ModelKey]
SET Secret = REPLACE(
                REPLACE(Secret, '"apiKey":', '"secretId":'),
                '"secret":', '"secretKey":')
WHERE Id = 13;

update ModelKey set Host = NULL where Id IN (1, 2, 6, 10);

/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
BEGIN TRANSACTION
SET QUOTED_IDENTIFIER ON
SET ARITHABORT ON
SET NUMERIC_ROUNDABORT OFF
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_ClientInfo
GO
ALTER TABLE dbo.ClientInfo SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_UsageTransactionLog
GO
ALTER TABLE dbo.UsageTransaction SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_TransactionLog
GO
ALTER TABLE dbo.BalanceTransaction SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_UserModel2
GO
ALTER TABLE dbo.UserModel SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
CREATE TABLE dbo.Tmp_UserModelUsage
	(
	Id bigint NOT NULL IDENTITY (1, 1),
	UserModelId int NOT NULL,
	SegmentCount smallint NOT NULL,
	InputTokens int NOT NULL,
	OutputTokens int NOT NULL,
	ReasoningTokens int NOT NULL,
	PreprocessDurationMs int NOT NULL,
	FirstResponseDurationMs int NOT NULL,
	TotalDurationMs int NOT NULL,
	InputCost decimal(14, 8) NOT NULL,
	OutputCost decimal(14, 8) NOT NULL,
	BalanceTransactionId bigint NULL,
	UsageTransactionId bigint NULL,
	ClientInfoId int NOT NULL,
	CreatedAt datetime2(7) NOT NULL
	)  ON [PRIMARY]
GO
ALTER TABLE dbo.Tmp_UserModelUsage SET (LOCK_ESCALATION = TABLE)
GO
ALTER TABLE dbo.Tmp_UserModelUsage ADD CONSTRAINT
	DF_UserModelUsage_PreprocessDurationMs DEFAULT 0 FOR PreprocessDurationMs
GO
SET IDENTITY_INSERT dbo.Tmp_UserModelUsage ON
GO
IF EXISTS(SELECT * FROM dbo.UserModelUsage)
	 EXEC('INSERT INTO dbo.Tmp_UserModelUsage (Id, UserModelId, SegmentCount, InputTokens, OutputTokens, ReasoningTokens, FirstResponseDurationMs, TotalDurationMs, InputCost, OutputCost, BalanceTransactionId, UsageTransactionId, ClientInfoId, CreatedAt)
		SELECT Id, UserModelId, SegmentCount, InputTokens, OutputTokens, ReasoningTokens, FirstResponseDurationMs, DurationMs, InputCost, OutputCost, BalanceTransactionId, UsageTransactionId, ClientInfoId, CreatedAt FROM dbo.UserModelUsage WITH (HOLDLOCK TABLOCKX)')
GO
SET IDENTITY_INSERT dbo.Tmp_UserModelUsage OFF
GO
ALTER TABLE dbo.Message
	DROP CONSTRAINT FK_Message2_UserModelUsage
GO
ALTER TABLE dbo.UserApiUsage
	DROP CONSTRAINT FK_UserApiUsage_UserModelUsage
GO
DROP TABLE dbo.UserModelUsage
GO
EXECUTE sp_rename N'dbo.Tmp_UserModelUsage', N'UserModelUsage', 'OBJECT' 
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	PK_ModelUsage PRIMARY KEY CLUSTERED 
	(
	Id
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

GO
CREATE NONCLUSTERED INDEX IX_ModelUsage_UserModelId ON dbo.UserModelUsage
	(
	UserModelId
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX IX_ModelUsage_BalanceTransaction ON dbo.UserModelUsage
	(
	BalanceTransactionId
	) WHERE ([BalanceTransactionId] IS NOT NULL)
 WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX IX_ModelUsage_UsageTransaction ON dbo.UserModelUsage
	(
	UsageTransactionId
	) WHERE ([UsageTransactionId] IS NOT NULL)
 WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX IX_ModelUsage_CreatedAt ON dbo.UserModelUsage
	(
	CreatedAt
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_UserModel2 FOREIGN KEY
	(
	UserModelId
	) REFERENCES dbo.UserModel
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_TransactionLog FOREIGN KEY
	(
	BalanceTransactionId
	) REFERENCES dbo.BalanceTransaction
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_UsageTransactionLog FOREIGN KEY
	(
	UsageTransactionId
	) REFERENCES dbo.UsageTransaction
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_ClientInfo FOREIGN KEY
	(
	ClientInfoId
	) REFERENCES dbo.ClientInfo
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserApiUsage ADD CONSTRAINT
	FK_UserApiUsage_UserModelUsage FOREIGN KEY
	(
	UsageId
	) REFERENCES dbo.UserModelUsage
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserApiUsage SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.Message ADD CONSTRAINT
	FK_Message2_UserModelUsage FOREIGN KEY
	(
	UsageId
	) REFERENCES dbo.UserModelUsage
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.Message SET (LOCK_ESCALATION = TABLE)
GO
COMMIT

/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
BEGIN TRANSACTION
SET QUOTED_IDENTIFIER ON
SET ARITHABORT ON
SET NUMERIC_ROUNDABORT OFF
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT DF_UserModelUsage_PreprocessDurationMs
GO
ALTER TABLE dbo.UserModelUsage SET (LOCK_ESCALATION = TABLE)
GO
COMMIT

/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
BEGIN TRANSACTION
SET QUOTED_IDENTIFIER ON
SET ARITHABORT ON
SET NUMERIC_ROUNDABORT OFF
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_ClientInfo
GO
ALTER TABLE dbo.ClientInfo SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_UsageTransactionLog
GO
ALTER TABLE dbo.UsageTransaction SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_TransactionLog
GO
ALTER TABLE dbo.BalanceTransaction SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_UserModel2
GO
ALTER TABLE dbo.UserModel SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
CREATE TABLE dbo.Tmp_UserModelUsage
	(
	Id bigint NOT NULL IDENTITY (1, 1),
	UserModelId int NOT NULL,
	SegmentCount smallint NOT NULL,
	InputTokens int NOT NULL,
	OutputTokens int NOT NULL,
	ReasoningTokens int NOT NULL,
	IsUsageReliable bit NOT NULL,
	PreprocessDurationMs int NOT NULL,
	FirstResponseDurationMs int NOT NULL,
	TotalDurationMs int NOT NULL,
	InputCost decimal(14, 8) NOT NULL,
	OutputCost decimal(14, 8) NOT NULL,
	BalanceTransactionId bigint NULL,
	UsageTransactionId bigint NULL,
	ClientInfoId int NOT NULL,
	CreatedAt datetime2(7) NOT NULL
	)  ON [PRIMARY]
GO
ALTER TABLE dbo.Tmp_UserModelUsage SET (LOCK_ESCALATION = TABLE)
GO
ALTER TABLE dbo.Tmp_UserModelUsage ADD CONSTRAINT
	DF_UserModelUsage_IsUsageReliable DEFAULT 1 FOR IsUsageReliable
GO
SET IDENTITY_INSERT dbo.Tmp_UserModelUsage ON
GO
IF EXISTS(SELECT * FROM dbo.UserModelUsage)
	 EXEC('INSERT INTO dbo.Tmp_UserModelUsage (Id, UserModelId, SegmentCount, InputTokens, OutputTokens, ReasoningTokens, PreprocessDurationMs, FirstResponseDurationMs, TotalDurationMs, InputCost, OutputCost, BalanceTransactionId, UsageTransactionId, ClientInfoId, CreatedAt)
		SELECT Id, UserModelId, SegmentCount, InputTokens, OutputTokens, ReasoningTokens, PreprocessDurationMs, FirstResponseDurationMs, TotalDurationMs, InputCost, OutputCost, BalanceTransactionId, UsageTransactionId, ClientInfoId, CreatedAt FROM dbo.UserModelUsage WITH (HOLDLOCK TABLOCKX)')
GO
SET IDENTITY_INSERT dbo.Tmp_UserModelUsage OFF
GO
ALTER TABLE dbo.UserApiUsage
	DROP CONSTRAINT FK_UserApiUsage_UserModelUsage
GO
ALTER TABLE dbo.Message
	DROP CONSTRAINT FK_Message2_UserModelUsage
GO
DROP TABLE dbo.UserModelUsage
GO
EXECUTE sp_rename N'dbo.Tmp_UserModelUsage', N'UserModelUsage', 'OBJECT' 
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	PK_ModelUsage PRIMARY KEY CLUSTERED 
	(
	Id
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

GO
CREATE NONCLUSTERED INDEX IX_ModelUsage_UserModelId ON dbo.UserModelUsage
	(
	UserModelId
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX IX_ModelUsage_BalanceTransaction ON dbo.UserModelUsage
	(
	BalanceTransactionId
	) WHERE ([BalanceTransactionId] IS NOT NULL)
 WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX IX_ModelUsage_UsageTransaction ON dbo.UserModelUsage
	(
	UsageTransactionId
	) WHERE ([UsageTransactionId] IS NOT NULL)
 WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX IX_ModelUsage_CreatedAt ON dbo.UserModelUsage
	(
	CreatedAt
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_UserModel2 FOREIGN KEY
	(
	UserModelId
	) REFERENCES dbo.UserModel
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_TransactionLog FOREIGN KEY
	(
	BalanceTransactionId
	) REFERENCES dbo.BalanceTransaction
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_UsageTransactionLog FOREIGN KEY
	(
	UsageTransactionId
	) REFERENCES dbo.UsageTransaction
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_ClientInfo FOREIGN KEY
	(
	ClientInfoId
	) REFERENCES dbo.ClientInfo
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.Message ADD CONSTRAINT
	FK_Message2_UserModelUsage FOREIGN KEY
	(
	UsageId
	) REFERENCES dbo.UserModelUsage
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.Message SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserApiUsage ADD CONSTRAINT
	FK_UserApiUsage_UserModelUsage FOREIGN KEY
	(
	UsageId
	) REFERENCES dbo.UserModelUsage
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserApiUsage SET (LOCK_ESCALATION = TABLE)
GO
COMMIT


/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
BEGIN TRANSACTION
SET QUOTED_IDENTIFIER ON
SET ARITHABORT ON
SET NUMERIC_ROUNDABORT OFF
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT DF_UserModelUsage_IsUsageReliable
GO
ALTER TABLE dbo.UserModelUsage SET (LOCK_ESCALATION = TABLE)
GO
COMMIT


/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
BEGIN TRANSACTION
SET QUOTED_IDENTIFIER ON
SET ARITHABORT ON
SET NUMERIC_ROUNDABORT OFF
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_ClientInfo
GO
ALTER TABLE dbo.ClientInfo SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_UsageTransactionLog
GO
ALTER TABLE dbo.UsageTransaction SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_TransactionLog
GO
ALTER TABLE dbo.BalanceTransaction SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_UserModel2
GO
ALTER TABLE dbo.UserModel SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
CREATE TABLE dbo.Tmp_UserModelUsage
	(
	Id bigint NOT NULL IDENTITY (1, 1),
	UserModelId int NOT NULL,
	SegmentCount smallint NOT NULL,
	InputTokens int NOT NULL,
	OutputTokens int NOT NULL,
	ReasoningTokens int NOT NULL,
	IsUsageReliable bit NOT NULL,
	PreprocessDurationMs int NOT NULL,
	FirstResponseDurationMs int NOT NULL,
	PostprocessDurationMs int NOT NULL,
	TotalDurationMs int NOT NULL,
	InputCost decimal(14, 8) NOT NULL,
	OutputCost decimal(14, 8) NOT NULL,
	BalanceTransactionId bigint NULL,
	UsageTransactionId bigint NULL,
	ClientInfoId int NOT NULL,
	CreatedAt datetime2(7) NOT NULL
	)  ON [PRIMARY]
GO
ALTER TABLE dbo.Tmp_UserModelUsage SET (LOCK_ESCALATION = TABLE)
GO
ALTER TABLE dbo.Tmp_UserModelUsage ADD CONSTRAINT
	DF_UserModelUsage_PostprocessDurationMs DEFAULT 0 FOR PostprocessDurationMs
GO
SET IDENTITY_INSERT dbo.Tmp_UserModelUsage ON
GO
IF EXISTS(SELECT * FROM dbo.UserModelUsage)
	 EXEC('INSERT INTO dbo.Tmp_UserModelUsage (Id, UserModelId, SegmentCount, InputTokens, OutputTokens, ReasoningTokens, IsUsageReliable, PreprocessDurationMs, FirstResponseDurationMs, TotalDurationMs, InputCost, OutputCost, BalanceTransactionId, UsageTransactionId, ClientInfoId, CreatedAt)
		SELECT Id, UserModelId, SegmentCount, InputTokens, OutputTokens, ReasoningTokens, IsUsageReliable, PreprocessDurationMs, FirstResponseDurationMs, TotalDurationMs, InputCost, OutputCost, BalanceTransactionId, UsageTransactionId, ClientInfoId, CreatedAt FROM dbo.UserModelUsage WITH (HOLDLOCK TABLOCKX)')
GO
SET IDENTITY_INSERT dbo.Tmp_UserModelUsage OFF
GO
ALTER TABLE dbo.Message
	DROP CONSTRAINT FK_Message2_UserModelUsage
GO
ALTER TABLE dbo.UserApiUsage
	DROP CONSTRAINT FK_UserApiUsage_UserModelUsage
GO
DROP TABLE dbo.UserModelUsage
GO
EXECUTE sp_rename N'dbo.Tmp_UserModelUsage', N'UserModelUsage', 'OBJECT' 
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	PK_ModelUsage PRIMARY KEY CLUSTERED 
	(
	Id
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

GO
CREATE NONCLUSTERED INDEX IX_ModelUsage_UserModelId ON dbo.UserModelUsage
	(
	UserModelId
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX IX_ModelUsage_BalanceTransaction ON dbo.UserModelUsage
	(
	BalanceTransactionId
	) WHERE ([BalanceTransactionId] IS NOT NULL)
 WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX IX_ModelUsage_UsageTransaction ON dbo.UserModelUsage
	(
	UsageTransactionId
	) WHERE ([UsageTransactionId] IS NOT NULL)
 WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX IX_ModelUsage_CreatedAt ON dbo.UserModelUsage
	(
	CreatedAt
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_UserModel2 FOREIGN KEY
	(
	UserModelId
	) REFERENCES dbo.UserModel
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_TransactionLog FOREIGN KEY
	(
	BalanceTransactionId
	) REFERENCES dbo.BalanceTransaction
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_UsageTransactionLog FOREIGN KEY
	(
	UsageTransactionId
	) REFERENCES dbo.UsageTransaction
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_ClientInfo FOREIGN KEY
	(
	ClientInfoId
	) REFERENCES dbo.ClientInfo
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserApiUsage ADD CONSTRAINT
	FK_UserApiUsage_UserModelUsage FOREIGN KEY
	(
	UsageId
	) REFERENCES dbo.UserModelUsage
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserApiUsage SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.Message ADD CONSTRAINT
	FK_Message2_UserModelUsage FOREIGN KEY
	(
	UsageId
	) REFERENCES dbo.UserModelUsage
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.Message SET (LOCK_ESCALATION = TABLE)
GO
COMMIT


/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
BEGIN TRANSACTION
SET QUOTED_IDENTIFIER ON
SET ARITHABORT ON
SET NUMERIC_ROUNDABORT OFF
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT DF_UserModelUsage_PostprocessDurationMs
GO
ALTER TABLE dbo.UserModelUsage SET (LOCK_ESCALATION = TABLE)
GO
COMMIT

/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
BEGIN TRANSACTION
SET QUOTED_IDENTIFIER ON
SET ARITHABORT ON
SET NUMERIC_ROUNDABORT OFF
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
COMMIT
BEGIN TRANSACTION
GO
CREATE TABLE dbo.FinishReason
	(
	Id tinyint NOT NULL,
	Name varchar(50) NOT NULL
	)  ON [PRIMARY]
GO
ALTER TABLE dbo.FinishReason ADD CONSTRAINT
	PK_FinishReason PRIMARY KEY CLUSTERED 
	(
	Id
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

GO
ALTER TABLE dbo.FinishReason SET (LOCK_ESCALATION = TABLE)
GO
COMMIT

INSERT INTO [FinishReason] ([Id], [Name]) VALUES
(0, 'Success'),
(1, 'Stop'),
(2, 'Length'),
(3, 'ToolCalls'),
(4, 'ContentFilter'),
(5, 'FunctionCall'),
(100, 'UnknownError'),
(101, 'InsufficientBalance'),
(102, 'UpstreamError'),
(103, 'InvalidModel'),
(104, 'SubscriptionExpired'),
(105, 'BadParameter'),
(106, 'Cancelled');


/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
BEGIN TRANSACTION
SET QUOTED_IDENTIFIER ON
SET ARITHABORT ON
SET NUMERIC_ROUNDABORT OFF
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.FinishReason SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_ClientInfo
GO
ALTER TABLE dbo.ClientInfo SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_UsageTransactionLog
GO
ALTER TABLE dbo.UsageTransaction SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_TransactionLog
GO
ALTER TABLE dbo.BalanceTransaction SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_UserModel2
GO
ALTER TABLE dbo.UserModel SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
CREATE TABLE dbo.Tmp_UserModelUsage
	(
	Id bigint NOT NULL IDENTITY (1, 1),
	UserModelId int NOT NULL,
	FinishReasonId tinyint NOT NULL,
	SegmentCount smallint NOT NULL,
	InputTokens int NOT NULL,
	OutputTokens int NOT NULL,
	ReasoningTokens int NOT NULL,
	IsUsageReliable bit NOT NULL,
	PreprocessDurationMs int NOT NULL,
	FirstResponseDurationMs int NOT NULL,
	PostprocessDurationMs int NOT NULL,
	TotalDurationMs int NOT NULL,
	InputCost decimal(14, 8) NOT NULL,
	OutputCost decimal(14, 8) NOT NULL,
	BalanceTransactionId bigint NULL,
	UsageTransactionId bigint NULL,
	ClientInfoId int NOT NULL,
	CreatedAt datetime2(7) NOT NULL
	)  ON [PRIMARY]
GO
ALTER TABLE dbo.Tmp_UserModelUsage SET (LOCK_ESCALATION = TABLE)
GO
ALTER TABLE dbo.Tmp_UserModelUsage ADD CONSTRAINT
	DF_UserModelUsage_FinishReasonId DEFAULT 0 FOR FinishReasonId
GO
SET IDENTITY_INSERT dbo.Tmp_UserModelUsage ON
GO
IF EXISTS(SELECT * FROM dbo.UserModelUsage)
	 EXEC('INSERT INTO dbo.Tmp_UserModelUsage (Id, UserModelId, SegmentCount, InputTokens, OutputTokens, ReasoningTokens, IsUsageReliable, PreprocessDurationMs, FirstResponseDurationMs, PostprocessDurationMs, TotalDurationMs, InputCost, OutputCost, BalanceTransactionId, UsageTransactionId, ClientInfoId, CreatedAt)
		SELECT Id, UserModelId, SegmentCount, InputTokens, OutputTokens, ReasoningTokens, IsUsageReliable, PreprocessDurationMs, FirstResponseDurationMs, PostprocessDurationMs, TotalDurationMs, InputCost, OutputCost, BalanceTransactionId, UsageTransactionId, ClientInfoId, CreatedAt FROM dbo.UserModelUsage WITH (HOLDLOCK TABLOCKX)')
GO
SET IDENTITY_INSERT dbo.Tmp_UserModelUsage OFF
GO
ALTER TABLE dbo.UserApiUsage
	DROP CONSTRAINT FK_UserApiUsage_UserModelUsage
GO
ALTER TABLE dbo.Message
	DROP CONSTRAINT FK_Message2_UserModelUsage
GO
DROP TABLE dbo.UserModelUsage
GO
EXECUTE sp_rename N'dbo.Tmp_UserModelUsage', N'UserModelUsage', 'OBJECT' 
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	PK_ModelUsage PRIMARY KEY CLUSTERED 
	(
	Id
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

GO
CREATE NONCLUSTERED INDEX IX_ModelUsage_UserModelId ON dbo.UserModelUsage
	(
	UserModelId
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX IX_ModelUsage_BalanceTransaction ON dbo.UserModelUsage
	(
	BalanceTransactionId
	) WHERE ([BalanceTransactionId] IS NOT NULL)
 WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX IX_ModelUsage_UsageTransaction ON dbo.UserModelUsage
	(
	UsageTransactionId
	) WHERE ([UsageTransactionId] IS NOT NULL)
 WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX IX_ModelUsage_CreatedAt ON dbo.UserModelUsage
	(
	CreatedAt
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_UserModel2 FOREIGN KEY
	(
	UserModelId
	) REFERENCES dbo.UserModel
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_TransactionLog FOREIGN KEY
	(
	BalanceTransactionId
	) REFERENCES dbo.BalanceTransaction
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_UsageTransactionLog FOREIGN KEY
	(
	UsageTransactionId
	) REFERENCES dbo.UsageTransaction
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_ModelUsage_ClientInfo FOREIGN KEY
	(
	ClientInfoId
	) REFERENCES dbo.ClientInfo
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserModelUsage ADD CONSTRAINT
	FK_UserModelUsage_FinishReason FOREIGN KEY
	(
	FinishReasonId
	) REFERENCES dbo.FinishReason
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.Message ADD CONSTRAINT
	FK_Message2_UserModelUsage FOREIGN KEY
	(
	UsageId
	) REFERENCES dbo.UserModelUsage
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.Message SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserApiUsage ADD CONSTRAINT
	FK_UserApiUsage_UserModelUsage FOREIGN KEY
	(
	UsageId
	) REFERENCES dbo.UserModelUsage
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.UserApiUsage SET (LOCK_ESCALATION = TABLE)
GO
COMMIT

/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
BEGIN TRANSACTION
SET QUOTED_IDENTIFIER ON
SET ARITHABORT ON
SET NUMERIC_ROUNDABORT OFF
SET CONCAT_NULL_YIELDS_NULL ON
SET ANSI_NULLS ON
SET ANSI_PADDING ON
SET ANSI_WARNINGS ON
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT DF_UserModelUsage_FinishReasonId
GO
ALTER TABLE dbo.UserModelUsage SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
