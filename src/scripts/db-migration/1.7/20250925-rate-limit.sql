-- 登录相关速率限制表结构与补充索引

PRINT N'1) 创建 PasswordAttempt 表用于记录密码登录尝试';
IF OBJECT_ID(N'[dbo].[PasswordAttempt]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[PasswordAttempt]
	(
		[Id]            INT            IDENTITY(1, 1) NOT NULL,
		[UserName]      NVARCHAR(1000) NOT NULL,
		[ClientInfoId]  INT            NOT NULL,
		[UserId]        INT            NULL,
		[IsSuccessful]  BIT            NOT NULL,
		[FailureReason] VARCHAR(1000)  NULL,
		[CreatedAt]     DATETIME2(7)   NOT NULL,
		CONSTRAINT [PK_PasswordAttempt] PRIMARY KEY CLUSTERED ([Id] ASC),
		CONSTRAINT [FK_PasswordAttempt_ClientInfo] FOREIGN KEY ([ClientInfoId]) REFERENCES [dbo].[ClientInfo]([Id]),
		CONSTRAINT [FK_PasswordAttempt_User]       FOREIGN KEY ([UserId])       REFERENCES [dbo].[User]([Id]),
		INDEX [IX_PasswordAttempt_ClientInfo] ([ClientInfoId]),
		INDEX [IX_PasswordAttempt_UserId] ([UserId]),
		INDEX [IX_PasswordAttempt_CreatedAt] ([CreatedAt])
	);
END
GO


PRINT N'2) 创建 KeycloakAttempt 表用于记录 SSO 登录尝试';
IF OBJECT_ID(N'[dbo].[KeycloakAttempt]', N'U') IS NULL
BEGIN
	CREATE TABLE [dbo].[KeycloakAttempt]
	(
		[Id]            INT             IDENTITY(1, 1) NOT NULL,
		[ClientInfoId]  INT             NOT NULL,
		[UserId]        INT             NULL,
		[Provider]      VARCHAR(200)    NOT NULL,
		[Sub]           NVARCHAR(1000)  NULL,
		[Email]         NVARCHAR(1000)  NULL,
		[IsSuccessful]  BIT             NOT NULL,
		[FailureReason] VARCHAR(1000)   NULL,
		[CreatedAt]     DATETIME2(7)    NOT NULL,
		CONSTRAINT [PK_KeycloakAttempt] PRIMARY KEY CLUSTERED ([Id] ASC),
		CONSTRAINT [FK_KeycloakAttempt_ClientInfo] FOREIGN KEY ([ClientInfoId]) REFERENCES [dbo].[ClientInfo]([Id]),
		CONSTRAINT [FK_KeycloakAttempt_User]       FOREIGN KEY ([UserId])       REFERENCES [dbo].[User]([Id]),
		INDEX [IX_KeycloakAttempt_ClientInfo] ([ClientInfoId]),
		INDEX [IX_KeycloakAttempt_UserId] ([UserId]),
		INDEX [IX_KeycloakAttempt_CreatedAt] ([CreatedAt])
	);
END
GO


PRINT N'3) SmsAttempt 表补充索引';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SmsAttempt_CreatedAt' AND object_id = OBJECT_ID(N'[dbo].[SmsAttempt]', N'U'))
BEGIN
	CREATE INDEX [IX_SmsAttempt_CreatedAt] ON [dbo].[SmsAttempt]([CreatedAt]);
END
GO
