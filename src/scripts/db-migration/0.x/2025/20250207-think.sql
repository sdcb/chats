INSERT INTO [MessageContentType] VALUES(3, 'reasoning');

-- Region Parameters
DECLARE @p0 SmallInt = 16
DECLARE @p1 VarChar(1000) = 'Doubao'
DECLARE @p2 VarChar(1000) = null
DECLARE @p3 VarChar(1000) = 'your-key'
DECLARE @p4 Bit = 0
-- EndRegion
INSERT INTO [ModelProvider]([Id], [Name], [InitialHost], [InitialSecret], [RequireDeploymentName])
VALUES (@p0, @p1, @p2, @p3, @p4)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 17
DECLARE @p1 VarChar(1000) = 'SiliconFlow'
DECLARE @p2 VarChar(1000) = null
DECLARE @p3 VarChar(1000) = 'sk-yourkey'
DECLARE @p4 Bit = 0
-- EndRegion
INSERT INTO [ModelProvider]([Id], [Name], [InitialHost], [InitialSecret], [RequireDeploymentName])
VALUES (@p0, @p1, @p2, @p3, @p4)
GO

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
CREATE TABLE dbo.ReasoningResponseKind
	(
	Id tinyint NOT NULL,
	Name varchar(50) NOT NULL
	)  ON [PRIMARY]
GO
ALTER TABLE dbo.ReasoningResponseKind ADD CONSTRAINT
	PK_ReasoningResponseKind PRIMARY KEY CLUSTERED 
	(
	Id
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

GO
ALTER TABLE dbo.ReasoningResponseKind SET (LOCK_ESCALATION = TABLE)
GO
COMMIT


-- Region Parameters
DECLARE @p0 TinyInt = 0
DECLARE @p1 VarChar(1000) = 'NoReasoningOutput'
-- EndRegion
INSERT INTO [ReasoningResponseKind]([Id], [Name])
VALUES (@p0, @p1)
GO

-- Region Parameters
DECLARE @p0 TinyInt = 1
DECLARE @p1 VarChar(1000) = 'reasoning_content'
-- EndRegion
INSERT INTO [ReasoningResponseKind]([Id], [Name])
VALUES (@p0, @p1)
GO

-- Region Parameters
DECLARE @p0 TinyInt = 2
DECLARE @p1 VarChar(1000) = '<think>'
-- EndRegion
INSERT INTO [ReasoningResponseKind]([Id], [Name])
VALUES (@p0, @p1)
GO


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
ALTER TABLE dbo.ReasoningResponseKind SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.ModelReference
	DROP CONSTRAINT FK_ModelSetting_ModelProvider
GO
ALTER TABLE dbo.ModelProvider SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.ModelReference
	DROP CONSTRAINT FK_ModelReference_Tokenizer
GO
ALTER TABLE dbo.Tokenizer SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.ModelReference
	DROP CONSTRAINT FK_ModelReference_CurrencyRate
GO
ALTER TABLE dbo.CurrencyRate SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.ModelReference
	DROP CONSTRAINT DF_ModelSetting_ProviderId
GO
ALTER TABLE dbo.ModelReference
	DROP CONSTRAINT DF_DefaultModelSetting_AllowVision
GO
ALTER TABLE dbo.ModelReference
	DROP CONSTRAINT DF_ModelReference_AllowSystemPrompt
GO
ALTER TABLE dbo.ModelReference
	DROP CONSTRAINT DF_ModelReference_AllowStreaming
GO
ALTER TABLE dbo.ModelReference
	DROP CONSTRAINT DF_ModelDefaults_ContextWindow
GO
ALTER TABLE dbo.ModelReference
	DROP CONSTRAINT DF_ModelDefaults_MaxResponseTokens
GO
ALTER TABLE dbo.ModelReference
	DROP CONSTRAINT DF_ModelReference_CurrencyCode
GO
CREATE TABLE dbo.Tmp_ModelReference
	(
	Id smallint NOT NULL,
	ProviderId smallint NOT NULL,
	Name nvarchar(50) NOT NULL,
	DisplayName nvarchar(50) NULL,
	PublishDate date NULL,
	MinTemperature decimal(3, 2) NOT NULL,
	MaxTemperature decimal(3, 2) NOT NULL,
	AllowSearch bit NOT NULL,
	AllowVision bit NOT NULL,
	AllowSystemPrompt bit NOT NULL,
	AllowStreaming bit NOT NULL,
	ReasoningResponseKindId tinyint NOT NULL,
	ContextWindow int NOT NULL,
	MaxResponseTokens int NOT NULL,
	TokenizerId smallint NULL,
	InputTokenPrice1M decimal(9, 5) NOT NULL,
	OutputTokenPrice1M decimal(9, 5) NOT NULL,
	CurrencyCode char(3) NOT NULL
	)  ON [PRIMARY]
GO
ALTER TABLE dbo.Tmp_ModelReference SET (LOCK_ESCALATION = TABLE)
GO
ALTER TABLE dbo.Tmp_ModelReference ADD CONSTRAINT
	DF_ModelSetting_ProviderId DEFAULT ((1)) FOR ProviderId
GO
ALTER TABLE dbo.Tmp_ModelReference ADD CONSTRAINT
	DF_DefaultModelSetting_AllowVision DEFAULT ((0)) FOR AllowVision
GO
ALTER TABLE dbo.Tmp_ModelReference ADD CONSTRAINT
	DF_ModelReference_AllowSystemPrompt DEFAULT ((1)) FOR AllowSystemPrompt
GO
ALTER TABLE dbo.Tmp_ModelReference ADD CONSTRAINT
	DF_ModelReference_AllowStreaming DEFAULT ((1)) FOR AllowStreaming
GO
ALTER TABLE dbo.Tmp_ModelReference ADD CONSTRAINT
	DF_ModelReference_ReasoningResponseKindId DEFAULT 0 FOR ReasoningResponseKindId
GO
ALTER TABLE dbo.Tmp_ModelReference ADD CONSTRAINT
	DF_ModelDefaults_ContextWindow DEFAULT ((4096)) FOR ContextWindow
GO
ALTER TABLE dbo.Tmp_ModelReference ADD CONSTRAINT
	DF_ModelDefaults_MaxResponseTokens DEFAULT ((4096)) FOR MaxResponseTokens
GO
ALTER TABLE dbo.Tmp_ModelReference ADD CONSTRAINT
	DF_ModelReference_CurrencyCode DEFAULT ('RMB') FOR CurrencyCode
GO
IF EXISTS(SELECT * FROM dbo.ModelReference)
	 EXEC('INSERT INTO dbo.Tmp_ModelReference (Id, ProviderId, Name, DisplayName, PublishDate, MinTemperature, MaxTemperature, AllowSearch, AllowVision, AllowSystemPrompt, AllowStreaming, ContextWindow, MaxResponseTokens, TokenizerId, InputTokenPrice1M, OutputTokenPrice1M, CurrencyCode)
		SELECT Id, ProviderId, Name, DisplayName, PublishDate, MinTemperature, MaxTemperature, AllowSearch, AllowVision, AllowSystemPrompt, AllowStreaming, ContextWindow, MaxResponseTokens, TokenizerId, InputTokenPrice1M, OutputTokenPrice1M, CurrencyCode FROM dbo.ModelReference WITH (HOLDLOCK TABLOCKX)')
GO
ALTER TABLE dbo.Model
	DROP CONSTRAINT FK_Model_ModelReference
GO
DROP TABLE dbo.ModelReference
GO
EXECUTE sp_rename N'dbo.Tmp_ModelReference', N'ModelReference', 'OBJECT' 
GO
ALTER TABLE dbo.ModelReference ADD CONSTRAINT
	PK_ModelSetting PRIMARY KEY CLUSTERED 
	(
	Id
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

GO
CREATE NONCLUSTERED INDEX [IX_ModelSetting_ProviderId+Type] ON dbo.ModelReference
	(
	ProviderId,
	Name
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE NONCLUSTERED INDEX IX_ModelReference_Name ON dbo.ModelReference
	(
	Name
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE dbo.ModelReference ADD CONSTRAINT
	FK_ModelReference_CurrencyRate FOREIGN KEY
	(
	CurrencyCode
	) REFERENCES dbo.CurrencyRate
	(
	Code
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.ModelReference ADD CONSTRAINT
	FK_ModelReference_Tokenizer FOREIGN KEY
	(
	TokenizerId
	) REFERENCES dbo.Tokenizer
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
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
ALTER TABLE dbo.ModelReference ADD CONSTRAINT
	FK_ModelReference_ReasoningResponseKind FOREIGN KEY
	(
	ReasoningResponseKindId
	) REFERENCES dbo.ReasoningResponseKind
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.Model ADD CONSTRAINT
	FK_Model_ModelReference FOREIGN KEY
	(
	ModelReferenceId
	) REFERENCES dbo.ModelReference
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.Model SET (LOCK_ESCALATION = TABLE)
GO
COMMIT


-- Region Parameters
DECLARE @p0 SmallInt = 1001
DECLARE @p1 TinyInt = 1
-- EndRegion
UPDATE [ModelReference]
SET [ReasoningResponseKindId] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1222
DECLARE @p1 TinyInt = 2
-- EndRegion
UPDATE [ModelReference]
SET [ReasoningResponseKindId] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1000
DECLARE @p1 Decimal(6,5) = 2
DECLARE @p2 Decimal(6,5) = 8
-- EndRegion
UPDATE [ModelReference]
SET [InputTokenPrice1M] = @p1, [OutputTokenPrice1M] = @p2
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1700
DECLARE @p1 SmallInt = 17
DECLARE @p2 NVarChar(1000) = 'deepseek-ai/DeepSeek-R1'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = '2025-01-21'
DECLARE @p5 Decimal(3,2) = 1
DECLARE @p6 Decimal(3,2) = 1
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 64000
DECLARE @p13 Int = 8192
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(6,5) = 4
DECLARE @p16 Decimal(7,5) = 16
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1701
DECLARE @p1 SmallInt = 17
DECLARE @p2 NVarChar(1000) = 'Pro/deepseek-ai/DeepSeek-R1'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = '2025-01-21'
DECLARE @p5 Decimal(3,2) = 1
DECLARE @p6 Decimal(3,2) = 1
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 64000
DECLARE @p13 Int = 8192
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(6,5) = 4
DECLARE @p16 Decimal(7,5) = 16
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1702
DECLARE @p1 SmallInt = 17
DECLARE @p2 NVarChar(1000) = 'deepseek-ai/DeepSeek-V3'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-V3'
DECLARE @p4 Date = '2025-12-16'
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 0
DECLARE @p12 Int = 64000
DECLARE @p13 Int = 8192
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(6,5) = 2
DECLARE @p16 Decimal(6,5) = 8
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1703
DECLARE @p1 SmallInt = 17
DECLARE @p2 NVarChar(1000) = 'Pro/deepseek-ai/DeepSeek-V3'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-V3'
DECLARE @p4 Date = '2024-12-16'
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 0
DECLARE @p12 Int = 64000
DECLARE @p13 Int = 8192
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(6,5) = 2
DECLARE @p16 Decimal(6,5) = 8
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1704
DECLARE @p1 SmallInt = 17
DECLARE @p2 NVarChar(1000) = 'deepseek-ai/DeepSeek-R1-Distill-Llama-70B'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = '2025-01-20'
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 32000
DECLARE @p13 Int = 16000
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(9,5) = 4.13
DECLARE @p16 Decimal(9,5) = 4.13
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1705
DECLARE @p1 SmallInt = 17
DECLARE @p2 NVarChar(1000) = 'deepseek-ai/DeepSeek-R1-Distill-Qwen-32B'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = '2025-01-20'
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 32000
DECLARE @p13 Int = 16000
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(9,5) = 1.26
DECLARE @p16 Decimal(9,5) = 1.26
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1706
DECLARE @p1 SmallInt = 17
DECLARE @p2 NVarChar(1000) = 'deepseek-ai/DeepSeek-R1-Distill-Qwen-14B'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = '2025-01-20'
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 32000
DECLARE @p13 Int = 16000
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(8,5) = 0.7
DECLARE @p16 Decimal(8,5) = 0.7
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1707
DECLARE @p1 SmallInt = 17
DECLARE @p2 NVarChar(1000) = 'deepseek-ai/DeepSeek-R1-Distill-Llama-8B'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = '2025-01-20'
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 32000
DECLARE @p13 Int = 16000
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(6,5) = 0
DECLARE @p16 Decimal(6,5) = 0
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1708
DECLARE @p1 SmallInt = 17
DECLARE @p2 NVarChar(1000) = 'deepseek-ai/DeepSeek-R1-Distill-Qwen-7B'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = '2025-01-20'
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 32000
DECLARE @p13 Int = 16000
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(6,5) = 0
DECLARE @p16 Decimal(6,5) = 0
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1709
DECLARE @p1 SmallInt = 17
DECLARE @p2 NVarChar(1000) = 'deepseek-ai/DeepSeek-R1-Distill-Qwen-1.5B'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Date = '2025-01-20'
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 0
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 1
DECLARE @p12 Int = 32000
DECLARE @p13 Int = 16000
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(6,5) = 0
DECLARE @p16 Decimal(6,5) = 0
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1402
DECLARE @p1 SmallInt = 14
DECLARE @p2 NVarChar(1000) = 'deepseek-r1'
DECLARE @p3 NVarChar(1000) = null
DECLARE @p4 Date = null
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 TinyInt = 2
DECLARE @p12 Int = 128000
DECLARE @p13 Int = 8000
DECLARE @p14 SmallInt = null
DECLARE @p15 Decimal(6,5) = 0
DECLARE @p16 Decimal(6,5) = 0
DECLARE @p17 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16, @p17)
GO


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
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_ModelUsage_UsageTransactionLog
GO
ALTER TABLE dbo.UsageTransaction SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.UserModelUsage
	DROP CONSTRAINT FK_UserModelUsage_FinishReason
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
	ReasoningDurationMs int NOT NULL,
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
	DF_UserModelUsage_ReasoningDurationMs DEFAULT 0 FOR ReasoningDurationMs
GO
SET IDENTITY_INSERT dbo.Tmp_UserModelUsage ON
GO
IF EXISTS(SELECT * FROM dbo.UserModelUsage)
	 EXEC('INSERT INTO dbo.Tmp_UserModelUsage (Id, UserModelId, FinishReasonId, SegmentCount, InputTokens, OutputTokens, ReasoningTokens, IsUsageReliable, PreprocessDurationMs, FirstResponseDurationMs, PostprocessDurationMs, TotalDurationMs, InputCost, OutputCost, BalanceTransactionId, UsageTransactionId, ClientInfoId, CreatedAt)
		SELECT Id, UserModelId, FinishReasonId, SegmentCount, InputTokens, OutputTokens, ReasoningTokens, IsUsageReliable, PreprocessDurationMs, FirstResponseDurationMs, PostprocessDurationMs, TotalDurationMs, InputCost, OutputCost, BalanceTransactionId, UsageTransactionId, ClientInfoId, CreatedAt FROM dbo.UserModelUsage WITH (HOLDLOCK TABLOCKX)')
GO
SET IDENTITY_INSERT dbo.Tmp_UserModelUsage OFF
GO
ALTER TABLE dbo.UserApiUsage
	DROP CONSTRAINT FK_UserApiUsage_UserModelUsage
GO
ALTER TABLE dbo.Message
	DROP CONSTRAINT FK_Message_UserModelUsage
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
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.Message ADD CONSTRAINT
	FK_Message_UserModelUsage FOREIGN KEY
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
