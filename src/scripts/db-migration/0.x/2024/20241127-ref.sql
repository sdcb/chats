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
	ShortName nvarchar(50) NULL,
	MinTemperature decimal(3, 2) NOT NULL,
	MaxTemperature decimal(3, 2) NOT NULL,
	AllowSearch bit NOT NULL,
	AllowVision bit NOT NULL,
	AllowSystemPrompt bit NOT NULL,
	AllowStreaming bit NOT NULL,
	ContextWindow int NOT NULL,
	MaxResponseTokens int NOT NULL,
	TokenizerId smallint NULL,
	PromptTokenPrice1M decimal(9, 5) NOT NULL,
	ResponseTokenPrice1M decimal(9, 5) NOT NULL,
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
	DF_ModelDefaults_ContextWindow DEFAULT ((4096)) FOR ContextWindow
GO
ALTER TABLE dbo.Tmp_ModelReference ADD CONSTRAINT
	DF_ModelDefaults_MaxResponseTokens DEFAULT ((4096)) FOR MaxResponseTokens
GO
ALTER TABLE dbo.Tmp_ModelReference ADD CONSTRAINT
	DF_ModelReference_CurrencyCode DEFAULT ('RMB') FOR CurrencyCode
GO
IF EXISTS(SELECT * FROM dbo.ModelReference)
	 EXEC('INSERT INTO dbo.Tmp_ModelReference (Id, ProviderId, Name, MinTemperature, MaxTemperature, AllowSearch, AllowVision, AllowSystemPrompt, AllowStreaming, ContextWindow, MaxResponseTokens, TokenizerId, PromptTokenPrice1M, ResponseTokenPrice1M, CurrencyCode)
		SELECT Id, ProviderId, Name, MinTemperature, MaxTemperature, AllowSearch, AllowVision, AllowSystemPrompt, AllowStreaming, ContextWindow, MaxResponseTokens, TokenizerId, PromptTokenPrice1M, ResponseTokenPrice1M, CurrencyCode FROM dbo.ModelReference WITH (HOLDLOCK TABLOCKX)')
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


update ModelReference set shortname = 'gpt-3.5-turbo' where name like 'gpt-3.5-turbo%'
update ModelReference set shortname = 'gpt-4-vision' where name like 'gpt-4-vision%'
update ModelReference set shortname = 'gpt-4-vision' where name like 'gpt-4-vision%'
update ModelReference set shortname = 'gpt-4-turbo' where name = 'gpt-4-1106-preview'
update ModelReference set shortname = 'gpt-4-turbo' where name = 'gpt-4-0125-preview'
update ModelReference set shortname = 'gpt-4-32k' where name = 'gpt-4-32k'
update ModelReference set shortname = 'gpt-4' where name = 'gpt-4'
update ModelReference set shortname = 'gpt-4-turbo' where name = 'gpt-4-turbo-2024-04-09'
update ModelReference set shortname = 'o1-preview' where name like 'o1-preview%'
update ModelReference set shortname = 'o1-mini' where name like 'o1-mini%'
update ModelReference set shortname = 'gpt-4o-mini' where name like 'gpt-4o-mini-%'
update ModelReference set shortname = 'gpt-4o' where name like 'gpt-4o%'



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
	ShortName nvarchar(50) NULL,
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
	PromptTokenPrice1M decimal(9, 5) NOT NULL,
	ResponseTokenPrice1M decimal(9, 5) NOT NULL,
	CurrencyCode char(3) NOT NULL
	)  ON [PRIMARY]
GO
ALTER TABLE dbo.Tmp_ModelReference SET (LOCK_ESCALATION = TABLE)
GO
ALTER TABLE dbo.Tmp_ModelReference ADD CONSTRAINT
	DF_ModelSetting_ProviderId DEFAULT ((1)) FOR ProviderId
GO
ALTER TABLE dbo.Tmp_ModelReference ADD CONSTRAINT
	DF_ModelReference_IsLegacy DEFAULT 0 FOR IsLegacy
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
	 EXEC('INSERT INTO dbo.Tmp_ModelReference (Id, ProviderId, Name, ShortName, MinTemperature, MaxTemperature, AllowSearch, AllowVision, AllowSystemPrompt, AllowStreaming, ContextWindow, MaxResponseTokens, TokenizerId, PromptTokenPrice1M, ResponseTokenPrice1M, CurrencyCode)
		SELECT Id, ProviderId, Name, ShortName, MinTemperature, MaxTemperature, AllowSearch, AllowVision, AllowSystemPrompt, AllowStreaming, ContextWindow, MaxResponseTokens, TokenizerId, PromptTokenPrice1M, ResponseTokenPrice1M, CurrencyCode FROM dbo.ModelReference WITH (HOLDLOCK TABLOCKX)')
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


update ModelReference set IsLegacy = 1 where ShortName in ('gpt-3.5-turbo', 'gpt-4-vision', 'gpt-4-turbo', 'gpt-4-32k', 'gpt-4') and name != 'gpt-4-turbo-2024-04-09' or name = 'gpt-4o-2024-05-13'
insert into ModelReference values (117, 1, 'gpt-4o-2024-11-20', 'gpt-4o', 0, 0, 2, 0, 1, 1, 1, 128000, 16384, 2, 2.5, 10, 'USD')
insert into ModelReference values (517, 1, 'gpt-4o-2024-11-20', 'gpt-4o', 0, 0, 2, 0, 1, 1, 1, 128000, 16384, 2, 2.5, 10, 'USD')
update ModelReference set contextwindow = 128000 where contextwindow=131072 and providerid in (1, 5)
update ModelProvider set name = 'Azure OpenAI' where id = 1
update ModelProvider set name = 'Tencent Hunyuan' where id = 2
update ModelProvider set name = '01.ai' where id = 3
update ModelProvider set name = 'Wenxin Qianfan' where id = 6
update ModelProvider set name = 'DashScope' where id = 7
update ModelProvider set name = 'Xunfei SparkDesk' where id = 8
update ModelProvider set name = 'Zhipu AI' where id = 9
update ModelReference set name = replace(name, 'gpt-3.5', 'gpt-35'), ShortName = replace(shortname, 'gpt-3.5', 'gpt-35') where ProviderId = 1
update modelReference set AllowSearch=0, AllowVision=0 where id = 0