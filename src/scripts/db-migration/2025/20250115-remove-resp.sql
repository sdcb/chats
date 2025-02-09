﻿/* 为了防止任何可能出现的数据丢失问题，您应该先仔细检查此脚本，然后再在数据库设计器的上下文之外运行此脚本。*/
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
ALTER TABLE dbo.UserModelUsage SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.Message
	DROP CONSTRAINT FK_Message_ChatRole
GO
ALTER TABLE dbo.ChatRole SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.Message
	DROP CONSTRAINT DF_Message_Edited
GO
CREATE TABLE dbo.Tmp_Message
	(
	Id bigint NOT NULL IDENTITY (1, 1),
	ChatId int NOT NULL,
	SpanId tinyint NULL,
	ParentId bigint NULL,
	ChatRoleId tinyint NOT NULL,
	Edited bit NOT NULL,
	UsageId bigint NULL,
	ReactionId bit NULL,
	CreatedAt datetime2(7) NOT NULL
	)  ON [PRIMARY]
GO
ALTER TABLE dbo.Tmp_Message SET (LOCK_ESCALATION = TABLE)
GO
ALTER TABLE dbo.Tmp_Message ADD CONSTRAINT
	DF_Message_Edited DEFAULT ((0)) FOR Edited
GO
SET IDENTITY_INSERT dbo.Tmp_Message ON
GO
IF EXISTS(SELECT * FROM dbo.Message)
	 EXEC('INSERT INTO dbo.Tmp_Message (Id, ChatId, SpanId, ParentId, ChatRoleId, Edited, CreatedAt)
		SELECT Id, ChatId, SpanId, ParentId, ChatRoleId, Edited, CreatedAt FROM dbo.Message WITH (HOLDLOCK TABLOCKX)')
GO
SET IDENTITY_INSERT dbo.Tmp_Message OFF
GO
ALTER TABLE dbo.Message
	DROP CONSTRAINT FK_Message_ParentMessage
GO
ALTER TABLE dbo.MessageResponse
	DROP CONSTRAINT FK_MessageResponse_Message
GO
ALTER TABLE dbo.MessageContent
	DROP CONSTRAINT FK_MessageContent_Message
GO
ALTER TABLE dbo.Chat
	DROP CONSTRAINT FK_Chat_Message
GO
ALTER TABLE dbo.Message
	DROP CONSTRAINT FK_Message_Chat
GO
DROP TABLE dbo.Message
GO
EXECUTE sp_rename N'dbo.Tmp_Message', N'Message', 'OBJECT' 
GO
ALTER TABLE dbo.Message ADD CONSTRAINT
	PK_Message PRIMARY KEY CLUSTERED 
	(
	Id
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

GO
CREATE NONCLUSTERED INDEX IX_Message_ChatSpan ON dbo.Message
	(
	ChatId,
	SpanId
	) WITH( STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
CREATE UNIQUE NONCLUSTERED INDEX IX_Message_UsageId 
ON dbo.Message (UsageId)
WHERE UsageId IS NOT NULL
WITH (
    STATISTICS_NORECOMPUTE = OFF, 
    IGNORE_DUP_KEY = OFF, 
    ALLOW_ROW_LOCKS = ON, 
    ALLOW_PAGE_LOCKS = ON
) 
ON [PRIMARY];
GO
ALTER TABLE dbo.Message ADD CONSTRAINT
	FK_Message_ChatRole FOREIGN KEY
	(
	ChatRoleId
	) REFERENCES dbo.ChatRole
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.Message ADD CONSTRAINT
	FK_Message_ParentMessage FOREIGN KEY
	(
	ParentId
	) REFERENCES dbo.Message
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
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
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.Chat ADD CONSTRAINT
	FK_Chat_Message FOREIGN KEY
	(
	LeafMessageId
	) REFERENCES dbo.Message
	(
	Id
	) ON UPDATE  NO ACTION 
	 ON DELETE  NO ACTION 
	
GO
ALTER TABLE dbo.Message ADD CONSTRAINT
	FK_Message_Chat FOREIGN KEY
	(
	ChatId
	) REFERENCES dbo.Chat
	(
	Id
	) ON UPDATE  CASCADE 
	 ON DELETE  CASCADE 
	
GO
ALTER TABLE dbo.Chat SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.MessageContent ADD CONSTRAINT
	FK_MessageContent_Message FOREIGN KEY
	(
	MessageId
	) REFERENCES dbo.Message
	(
	Id
	) ON UPDATE  CASCADE 
	 ON DELETE  CASCADE 
	
GO
ALTER TABLE dbo.MessageContent SET (LOCK_ESCALATION = TABLE)
GO
COMMIT
BEGIN TRANSACTION
GO
ALTER TABLE dbo.MessageResponse ADD CONSTRAINT
	FK_MessageResponse_Message FOREIGN KEY
	(
	MessageId
	) REFERENCES dbo.Message
	(
	Id
	) ON UPDATE  CASCADE 
	 ON DELETE  CASCADE 
	
GO
ALTER TABLE dbo.MessageResponse SET (LOCK_ESCALATION = TABLE)
GO
COMMIT



UPDATE M SET M.UsageId = MR.UsageId, M.ReactionId = MR.ReactionId 
FROM [dbo].[Message] M
INNER JOIN [dbo].[MessageResponse] MR ON M.Id = MR.MessageId;

DROP TABLE [dbo].[MessageResponse];