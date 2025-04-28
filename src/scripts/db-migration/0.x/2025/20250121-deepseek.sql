-- Region Parameters
DECLARE @p0 SmallInt = 1001
DECLARE @p1 SmallInt = 10
DECLARE @p2 NVarChar(1000) = 'deepseek-reasoner'
DECLARE @p3 NVarChar(1000) = 'DeepSeek-R1'
DECLARE @p4 Bit = 0
DECLARE @p5 Decimal(3,2) = 1
DECLARE @p6 Decimal(3,2) = 1
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 Int = 64000
DECLARE @p12 Int = 8000
DECLARE @p13 SmallInt = null
DECLARE @p14 Decimal(6,5) = 4
DECLARE @p15 Decimal(7,5) = 16
DECLARE @p16 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [ShortName], [IsLegacy], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1000
DECLARE @p1 NVarChar(1000) = 'DeepSeek-V3'
DECLARE @p2 Int = 8000
-- EndRegion
UPDATE [ModelReference]
SET [ShortName] = @p1, [MaxResponseTokens] = @p2
WHERE [Id] = @p0
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
	DROP CONSTRAINT DF_ModelReference_IsLegacy
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
	IsLegacy bit NOT NULL,
	MinTemperature decimal(3, 2) NOT NULL,
	MaxTemperature decimal(3, 2) NOT NULL,
	AllowSearch bit NOT NULL,
	AllowVision bit NOT NULL,
	AllowSystemPrompt bit NOT NULL,
	AllowStreaming bit NOT NULL,
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
	DF_ModelReference_IsLegacy DEFAULT ((0)) FOR IsLegacy
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
	DF_ModelDefaults_ContextWindow DEFAULT ((4096)) FOR ContextWindow
GO
ALTER TABLE dbo.Tmp_ModelReference ADD CONSTRAINT
	DF_ModelDefaults_MaxResponseTokens DEFAULT ((4096)) FOR MaxResponseTokens
GO
ALTER TABLE dbo.Tmp_ModelReference ADD CONSTRAINT
	DF_ModelReference_CurrencyCode DEFAULT ('RMB') FOR CurrencyCode
GO
IF EXISTS(SELECT * FROM dbo.ModelReference)
	 EXEC('INSERT INTO dbo.Tmp_ModelReference (Id, ProviderId, Name, DisplayName, IsLegacy, MinTemperature, MaxTemperature, AllowSearch, AllowVision, AllowSystemPrompt, AllowStreaming, ContextWindow, MaxResponseTokens, TokenizerId, InputTokenPrice1M, OutputTokenPrice1M, CurrencyCode)
		SELECT Id, ProviderId, Name, ShortName, IsLegacy, MinTemperature, MaxTemperature, AllowSearch, AllowVision, AllowSystemPrompt, AllowStreaming, ContextWindow, MaxResponseTokens, TokenizerId, InputTokenPrice1M, OutputTokenPrice1M, CurrencyCode FROM dbo.ModelReference WITH (HOLDLOCK TABLOCKX)')
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


UPDATE ModelReference SET PublishDate = '2024-01-01' WHERE IsLegacy = 1
UPDATE ModelReference SET PublishDate = '2025-01-21' WHERE Name = 'deepseek-reasoner'
UPDATE ModelReference SET PublishDate = '2024-12-16' WHERE Name = 'deepseek-chat'
UPDATE ModelReference SET PublishDate = '2024-11-20' WHERE Name = 'gpt-4o-2024-11-20'
UPDATE ModelReference SET PublishDate = '2024-08-06' WHERE Name = 'gpt-4o-2024-08-06'
UPDATE ModelReference SET PublishDate = '2024-05-13' WHERE Name = 'gpt-4o-2024-05-13'
UPDATE ModelReference SET PublishDate = '2024-12-17' WHERE Name = 'o1-2024-12-17'
UPDATE ModelReference SET PublishDate = '2024-09-12' WHERE Name = 'o1-mini-2024-09-12'
UPDATE ModelReference SET PublishDate = '2024-07-18' WHERE Name = 'gpt-4o-mini-2024-07-18'
UPDATE ModelReference SET PublishDate = '2024-04-09' WHERE Name = 'gpt-4-turbo-2024-04-09'


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
ALTER TABLE dbo.ModelReference
	DROP CONSTRAINT DF_ModelReference_IsLegacy
GO
ALTER TABLE dbo.ModelReference
	DROP COLUMN IsLegacy
GO
ALTER TABLE dbo.ModelReference SET (LOCK_ESCALATION = TABLE)
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
ALTER TABLE dbo.ModelProvider ADD
	RequireDeploymentName bit NOT NULL CONSTRAINT DF_ModelProvider_RequireDeploymentName DEFAULT 0
GO
ALTER TABLE dbo.ModelProvider SET (LOCK_ESCALATION = TABLE)
GO
COMMIT


UPDATE [ModelProvider] SET RequireDeploymentName = 1 WHERE Name IN('Azure OpenAI', 'Ollama')