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
ALTER TABLE dbo.ChatShare
	DROP CONSTRAINT FK_ChatShare_Chat
GO
ALTER TABLE dbo.Chat SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.ChatShare ADD CONSTRAINT
	FK_ChatShare_Chat FOREIGN KEY
	(
	ChatId
	) REFERENCES dbo.Chat
	(
	Id
	) ON UPDATE  CASCADE 
	 ON DELETE  CASCADE 
	
GO
ALTER TABLE dbo.ChatShare SET (LOCK_ESCALATION = TABLE)
GO
COMMIT

-- Region Parameters
DECLARE @p0 SmallInt = 14
DECLARE @p1 VarChar(1000) = 'Ollama'
DECLARE @p2 VarChar(1000) = 'http://localhost:11434/v1'
DECLARE @p3 VarChar(1000) = 'ollama'
-- EndRegion
INSERT INTO [ModelProvider]([Id], [Name], [InitialHost], [InitialSecret])
VALUES (@p0, @p1, @p2, @p3)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 15
DECLARE @p1 VarChar(1000) = 'MiniMax'
DECLARE @p2 VarChar(1000) = null
DECLARE @p3 VarChar(1000) = 'your-key'
-- EndRegion
INSERT INTO [ModelProvider]([Id], [Name], [InitialHost], [InitialSecret])
VALUES (@p0, @p1, @p2, @p3)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1400
DECLARE @p1 SmallInt = 14
DECLARE @p2 NVarChar(1000) = 'general'
DECLARE @p3 NVarChar(1000) = null
DECLARE @p4 Bit = 0
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 Int = 128000
DECLARE @p12 Int = 8000
DECLARE @p13 SmallInt = null
DECLARE @p14 Decimal(6,5) = 0
DECLARE @p15 Decimal(6,5) = 0
DECLARE @p16 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [ShortName], [IsLegacy], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1401
DECLARE @p1 SmallInt = 14
DECLARE @p2 NVarChar(1000) = 'general-vision'
DECLARE @p3 NVarChar(1000) = null
DECLARE @p4 Bit = 0
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 1
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 Int = 128000
DECLARE @p12 Int = 8000
DECLARE @p13 SmallInt = null
DECLARE @p14 Decimal(6,5) = 0
DECLARE @p15 Decimal(6,5) = 0
DECLARE @p16 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [ShortName], [IsLegacy], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1500
DECLARE @p1 SmallInt = 15
DECLARE @p2 NVarChar(1000) = 'MiniMax-Text-01'
DECLARE @p3 NVarChar(1000) = null
DECLARE @p4 Bit = 0
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 1
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 Int = 1000000
DECLARE @p12 Int = 8000
DECLARE @p13 SmallInt = null
DECLARE @p14 Decimal(6,5) = 1
DECLARE @p15 Decimal(6,5) = 8
DECLARE @p16 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [ShortName], [IsLegacy], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16)
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
ALTER TABLE dbo.ModelKey
	DROP CONSTRAINT FK_ModelKey2_ModelProvider
GO
ALTER TABLE dbo.ModelProvider SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
CREATE TABLE dbo.Tmp_ModelKey
	(
	Id smallint NOT NULL IDENTITY (1, 1),
	ModelProviderId smallint NOT NULL,
	Name nvarchar(50) NOT NULL,
	Host varchar(500) NULL,
	Secret varchar(1000) NULL,
	CreatedAt datetime2(7) NOT NULL,
	UpdatedAt datetime2(7) NOT NULL
	)  ON [PRIMARY]
GO
ALTER TABLE dbo.Tmp_ModelKey SET (LOCK_ESCALATION = TABLE)
GO
SET IDENTITY_INSERT dbo.Tmp_ModelKey ON
GO
IF EXISTS(SELECT * FROM dbo.ModelKey)
	 EXEC('INSERT INTO dbo.Tmp_ModelKey (Id, ModelProviderId, Name, Host, Secret, CreatedAt, UpdatedAt)
		SELECT Id, ModelProviderId, Name, Host, Secret, CreatedAt, UpdatedAt FROM dbo.ModelKey WITH (HOLDLOCK TABLOCKX)')
GO
SET IDENTITY_INSERT dbo.Tmp_ModelKey OFF
GO
ALTER TABLE dbo.Model
	DROP CONSTRAINT FK_Model_ModelKey2
GO
DROP TABLE dbo.ModelKey
GO
EXECUTE sp_rename N'dbo.Tmp_ModelKey', N'ModelKey', 'OBJECT' 
GO
ALTER TABLE dbo.ModelKey ADD CONSTRAINT
	PK_ModelKey2 PRIMARY KEY CLUSTERED 
	(
	Id
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

GO
CREATE NONCLUSTERED INDEX IX_ModelKey2_ModelProviderId ON dbo.ModelKey
	(
	ModelProviderId
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
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
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.Model ADD CONSTRAINT
	FK_Model_ModelKey2 FOREIGN KEY
	(
	ModelKeyId
	) REFERENCES dbo.ModelKey
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.Model SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
