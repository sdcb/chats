-- 创建新的 User2 表
CREATE TABLE [dbo].[User2](
    [Id] INT IDENTITY(1,1) NOT NULL,
    [OldId] UNIQUEIDENTIFIER NOT NULL,
    [Avatar] NVARCHAR(1000) NULL,
    [Account] NVARCHAR(1000) NOT NULL,
    [Username] NVARCHAR(1000) NOT NULL,
    [Password] NVARCHAR(1000) NULL,
    [Email] NVARCHAR(1000) NULL,
    [Phone] NVARCHAR(1000) NULL,
    [Role] NVARCHAR(1000) NOT NULL CONSTRAINT [Users2_role_df] DEFAULT('-'),
    [Enabled] BIT NOT NULL CONSTRAINT [Users2_enabled_df] DEFAULT((1)),
    [Provider] NVARCHAR(1000) NULL,
    [Sub] NVARCHAR(1000) NULL,
    [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [Users2_createdAt_df] DEFAULT(GETDATE()),
    [UpdatedAt] DATETIME2(7) NOT NULL,
    CONSTRAINT [Users2_pkey] PRIMARY KEY CLUSTERED ([Id] ASC)
) ON [PRIMARY];

-- 将数据从旧的 User 表复制到新的 User2 表
INSERT INTO [dbo].[User2] (
    [OldId], [Avatar], [Account], [Username], [Password], [Email], [Phone], 
    [Role], [Enabled], [Provider], [Sub], [CreatedAt], [UpdatedAt]
)
SELECT
    [Id] AS OldId, [Avatar], [Account], [Username], [Password], [Email], [Phone], 
    [Role], [Enabled], [Provider], [Sub], [CreatedAt], [UpdatedAt]
FROM [dbo].[User];



-- ======================== 表：BalanceTransaction ========================
-- 1. 添加新的可空列 UserId2 和 CreditUserId2
ALTER TABLE [dbo].[BalanceTransaction]
ADD [UserId2] INT NULL,
    [CreditUserId2] INT NULL;

-- 2. 将旧的 UserId 和 CreditUserId 对应新的 User2.Id
UPDATE bt
SET bt.UserId2 = u2.Id
FROM [dbo].[BalanceTransaction] bt
JOIN [dbo].[User2] u2 ON bt.UserId = u2.OldId;

UPDATE bt
SET bt.CreditUserId2 = u2.Id
FROM [dbo].[BalanceTransaction] bt
JOIN [dbo].[User2] u2 ON bt.CreditUserId = u2.OldId;

-- 3. 删除外键约束
ALTER TABLE [dbo].[BalanceTransaction] DROP CONSTRAINT [FK_BalanceLog2_Users];
ALTER TABLE [dbo].[BalanceTransaction] DROP CONSTRAINT [FK_BalanceLog2_CreditUser];

-- 4. 删除索引（如果存在）
IF EXISTS (SELECT name FROM sys.indexes WHERE name = 'IX_BalanceLog2_User')
    DROP INDEX [IX_BalanceLog2_User] ON [dbo].[BalanceTransaction];

IF EXISTS (SELECT name FROM sys.indexes WHERE name = 'IX_BalanceLog2_CreditUser')
    DROP INDEX [IX_BalanceLog2_CreditUser] ON [dbo].[BalanceTransaction];

-- 5. 删除旧的 UserId 和 CreditUserId 列
ALTER TABLE [dbo].[BalanceTransaction]
DROP COLUMN [UserId],
             [CreditUserId];

-- 6. 重命名新列为 UserId 和 CreditUserId
EXEC sp_rename '[dbo].[BalanceTransaction].[UserId2]', 'UserId', 'COLUMN';
EXEC sp_rename '[dbo].[BalanceTransaction].[CreditUserId2]', 'CreditUserId', 'COLUMN';

-- 7. 将新列设为 NOT NULL
ALTER TABLE [dbo].[BalanceTransaction]
ALTER COLUMN [UserId] INT NOT NULL;

ALTER TABLE [dbo].[BalanceTransaction]
ALTER COLUMN [CreditUserId] INT NOT NULL;

-- 8. 添加新的外键约束
ALTER TABLE [dbo].[BalanceTransaction]
ADD CONSTRAINT [FK_BalanceTransaction_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User2]([Id]);

ALTER TABLE [dbo].[BalanceTransaction]
ADD CONSTRAINT [FK_BalanceTransaction_CreditUserId] FOREIGN KEY ([CreditUserId]) REFERENCES [dbo].[User2]([Id]);

-- 9. 重新创建索引（如果需要）
CREATE INDEX [IX_BalanceTransaction_UserId] ON [dbo].[BalanceTransaction] ([UserId]);
CREATE INDEX [IX_BalanceTransaction_CreditUserId] ON [dbo].[BalanceTransaction] ([CreditUserId]);

-- ======================== 表：Chat ========================
-- 1. 添加新的可空列 UserId2
ALTER TABLE [dbo].[Chat]
ADD [UserId2] INT NULL;

-- 2. 将旧的 UserId 对应新的 User2.Id
UPDATE c
SET c.UserId2 = u2.Id
FROM [dbo].[Chat] c
JOIN [dbo].[User2] u2 ON c.UserId = u2.OldId;

-- 3. 删除外键约束
ALTER TABLE [dbo].[Chat] DROP CONSTRAINT [FK_Conversation2_Users];

-- 4. 删除索引（如果存在）
DROP INDEX IX_Conversation2_User ON [dbo].[Chat];

-- 5. 删除旧的 UserId 列
ALTER TABLE [dbo].[Chat] DROP COLUMN [UserId];

-- 6. 重命名新列为 UserId
EXEC sp_rename '[dbo].[Chat].[UserId2]', 'UserId', 'COLUMN';

-- 7. 将新列设为 NOT NULL
ALTER TABLE [dbo].[Chat] ALTER COLUMN [UserId] INT NOT NULL;

-- 8. 添加新的外键约束
ALTER TABLE [dbo].[Chat]
ADD CONSTRAINT [FK_Chat_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User2]([Id]);

-- 9. 重新创建索引（如果需要）
CREATE INDEX [IX_Chat_UserId] ON [dbo].[Chat] ([UserId]);

-- ======================== 表：InvitationCode ========================
-- 1. 添加新的可空列 CreateUserId2
ALTER TABLE [dbo].[InvitationCode]
ADD [CreateUserId2] INT NULL;

-- 2. 将旧的 CreateUserId 对应新的 User2.Id
UPDATE ic
SET ic.CreateUserId2 = u2.Id
FROM [dbo].[InvitationCode] ic
JOIN [dbo].[User2] u2 ON ic.CreateUserId = u2.OldId;

-- 5. 删除旧的 CreateUserId 列
ALTER TABLE [dbo].[InvitationCode] DROP COLUMN [CreateUserId];

-- 6. 重命名新列为 CreateUserId
EXEC sp_rename '[dbo].[InvitationCode].[CreateUserId2]', 'CreateUserId', 'COLUMN';

-- 7. 将新列设为 NOT NULL
ALTER TABLE [dbo].[InvitationCode] ALTER COLUMN [CreateUserId] INT NOT NULL;

-- 8. 添加新的外键约束
ALTER TABLE [dbo].[InvitationCode]
ADD CONSTRAINT [FK_InvitationCode_CreateUserId] FOREIGN KEY ([CreateUserId]) REFERENCES [dbo].[User2]([Id]);

-- 9. 重新创建索引（如果需要）
CREATE INDEX [IX_InvitationCode_CreateUserId] ON [dbo].[InvitationCode] ([CreateUserId]);

-- ======================== 表：Prompt ========================
-- 1. 添加新的可空列 CreateUserId2
ALTER TABLE [dbo].[Prompt]
ADD [CreateUserId2] INT NULL;

-- 2. 将旧的 CreateUserId 对应新的 User2.Id
UPDATE p
SET p.CreateUserId2 = u2.Id
FROM [dbo].[Prompt] p
JOIN [dbo].[User2] u2 ON p.CreateUserId = u2.OldId;

-- 3. 删除外键约束
ALTER TABLE [dbo].[Prompt] DROP CONSTRAINT [FK_Prompt2_User];

-- 4. 删除索引（如果存在）
DROP INDEX IX_Prompt2_CreateUserId ON [dbo].[Prompt];

-- 5. 删除旧的 CreateUserId 列
ALTER TABLE [dbo].[Prompt] DROP COLUMN [CreateUserId];

-- 6. 重命名新列为 CreateUserId
EXEC sp_rename '[dbo].[Prompt].[CreateUserId2]', 'CreateUserId', 'COLUMN';

-- 7. 将新列设为 NOT NULL
ALTER TABLE [dbo].[Prompt] ALTER COLUMN [CreateUserId] INT NOT NULL;

-- 8. 添加新的外键约束
ALTER TABLE [dbo].[Prompt]
ADD CONSTRAINT [FK_Prompt_CreateUserId] FOREIGN KEY ([CreateUserId]) REFERENCES [dbo].[User2]([Id]);

-- 9. 重新创建索引（如果需要）
CREATE INDEX [IX_Prompt_CreateUserId] ON [dbo].[Prompt] ([CreateUserId]);

-- ======================== 表：Session ========================
-- 1. 添加新的可空列 UserId2
ALTER TABLE [dbo].[Session]
ADD [UserId2] INT NULL;

-- 2. 将旧的 UserId 对应新的 User2.Id
UPDATE s
SET s.UserId2 = u2.Id
FROM [dbo].[Session] s
JOIN [dbo].[User2] u2 ON s.UserId = u2.OldId;

-- 3. 删除外键约束
ALTER TABLE [dbo].[Session] DROP CONSTRAINT [FK_Sessions_userId];

-- 4. 删除索引（如果存在）
-- 假设索引名为 IX_Session_UserId
DROP INDEX ID_Sessions_userId ON [dbo].[Session];

-- 5. 删除旧的 UserId 列
ALTER TABLE [dbo].[Session] DROP COLUMN [UserId];

-- 6. 重命名新列为 UserId
EXEC sp_rename '[dbo].[Session].[UserId2]', 'UserId', 'COLUMN';

-- 7. 将新列设为 NOT NULL
ALTER TABLE [dbo].[Session] ALTER COLUMN [UserId] INT NOT NULL;

-- 8. 添加新的外键约束
ALTER TABLE [dbo].[Session]
ADD CONSTRAINT [FK_Session_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User2]([Id]);

-- 9. 重新创建索引（如果需要）
CREATE INDEX [IX_Session_UserId] ON [dbo].[Session] ([UserId]);

-- ======================== 表：SmsRecord ========================
-- 1. 添加新的可空列 UserId2
ALTER TABLE [dbo].[SmsRecord]
ADD [UserId2] INT NULL;

-- 2. 将旧的 UserId 对应新的 User2.Id
UPDATE sr
SET sr.UserId2 = u2.Id
FROM [dbo].[SmsRecord] sr
JOIN [dbo].[User2] u2 ON sr.UserId = u2.OldId;

-- 4. 删除索引（如果存在）
DROP INDEX IX_SmsHistory_UserId ON [dbo].[SmsRecord];

-- 5. 删除旧的 UserId 列
ALTER TABLE [dbo].[SmsRecord] DROP COLUMN [UserId];

-- 6. 重命名新列为 UserId
EXEC sp_rename '[dbo].[SmsRecord].[UserId2]', 'UserId', 'COLUMN';

-- 8. 添加新的外键约束
ALTER TABLE [dbo].[SmsRecord]
ADD CONSTRAINT [FK_SmsRecord_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User2]([Id]);

-- 9. 重新创建索引（如果需要）
CREATE INDEX [IX_SmsRecord_UserId] ON [dbo].[SmsRecord] ([UserId]);


-- ======================== 表：UserApiKey ========================
-- 1. 添加新的可空列 UserId2
ALTER TABLE [dbo].[UserApiKey]
ADD [UserId2] INT NULL;

-- 2. 将旧的 UserId 对应新的 User2.Id
UPDATE uak
SET uak.UserId2 = u2.Id
FROM [dbo].[UserApiKey] uak
JOIN [dbo].[User2] u2 ON uak.UserId = u2.OldId;

-- 3. 删除外键约束
ALTER TABLE [dbo].[UserApiKey] DROP CONSTRAINT [FK_UserApiKey_Users];

-- 4. 删除索引（如果存在）
DROP INDEX IX_UserApiKey_User ON [dbo].[UserApiKey];

-- 5. 删除旧的 UserId 列
ALTER TABLE [dbo].[UserApiKey] DROP COLUMN [UserId];

-- 6. 重命名新列为 UserId
EXEC sp_rename '[dbo].[UserApiKey].[UserId2]', 'UserId', 'COLUMN';

-- 7. 将新列设为 NOT NULL
ALTER TABLE [dbo].[UserApiKey] ALTER COLUMN [UserId] INT NOT NULL;

-- 8. 添加新的外键约束
ALTER TABLE [dbo].[UserApiKey]
ADD CONSTRAINT [FK_UserApiKey_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User2]([Id]);

-- 9. 重新创建索引（如果需要）
CREATE INDEX [IX_UserApiKey_UserId] ON [dbo].[UserApiKey] ([UserId]);

-- ======================== 表：UserBalance ========================
-- 1. 添加新的可空列 UserId2
ALTER TABLE [dbo].[UserBalance]
ADD [UserId2] INT NULL;

-- 2. 将旧的 UserId 对应新的 User2.Id
UPDATE ub
SET ub.UserId2 = u2.Id
FROM [dbo].[UserBalance] ub
JOIN [dbo].[User2] u2 ON ub.UserId = u2.OldId;

-- 3. 删除外键约束
ALTER TABLE [dbo].[UserBalance] DROP CONSTRAINT UserBalances_userId_key;
ALTER TABLE [dbo].[UserBalance] DROP CONSTRAINT UserBalances_userId_fkey;

-- 4. 删除索引（如果存在）
DROP INDEX IDX_UserBalances_userId ON [dbo].[UserBalance];

-- 5. 删除旧的 UserId 列
ALTER TABLE [dbo].[UserBalance] DROP COLUMN [UserId];

-- 6. 重命名新列为 UserId
EXEC sp_rename '[dbo].[UserBalance].[UserId2]', 'UserId', 'COLUMN';

-- 7. 将新列设为 NOT NULL
ALTER TABLE [dbo].[UserBalance] ALTER COLUMN [UserId] INT NOT NULL;

-- 8. 添加新的外键约束
ALTER TABLE [dbo].[UserBalance]
ADD CONSTRAINT [FK_UserBalance_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User2]([Id]);

-- 9. 重新创建索引（如果需要）
CREATE UNIQUE INDEX [UserBalances_userId_key] ON [dbo].[UserBalance] ([UserId]);


-- ======================== 表：UserModel ========================
-- 1. 添加新的可空列 UserId2
ALTER TABLE [dbo].[UserModel]
ADD [UserId2] INT NULL;

-- 2. 将旧的 UserId 对应新的 User2.Id
UPDATE um
SET um.UserId2 = u2.Id
FROM [dbo].[UserModel] um
JOIN [dbo].[User2] u2 ON um.UserId = u2.OldId;

-- 3. 删除外键约束
ALTER TABLE [dbo].[UserModel] DROP CONSTRAINT [FK_UserModel2_User];

-- 4. 删除索引（如果存在）
DROP INDEX IX_UserModel2_UserId ON [dbo].[UserModel];

-- 5. 删除旧的 UserId 列
ALTER TABLE [dbo].[UserModel] DROP COLUMN [UserId];

-- 6. 重命名新列为 UserId
EXEC sp_rename '[dbo].[UserModel].[UserId2]', 'UserId', 'COLUMN';

-- 7. 将新列设为 NOT NULL
ALTER TABLE [dbo].[UserModel] ALTER COLUMN [UserId] INT NOT NULL;

-- 8. 添加新的外键约束
ALTER TABLE [dbo].[UserModel]
ADD CONSTRAINT [FK_UserModel_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User2]([Id]);

-- 9. 重新创建索引（如果需要）
CREATE INDEX [IX_UserModel_UserId] ON [dbo].[UserModel] ([UserId]);

-- ======================== 表：UserInvitation（特殊处理） ========================
-- 1. 添加新的可空列 UserId2
ALTER TABLE [dbo].[UserInvitation]
ADD [UserId2] INT NULL;

-- 2. 将旧的 UserId 对应新的 User2.Id
UPDATE ui
SET ui.UserId2 = u2.Id
FROM [dbo].[UserInvitation] ui
JOIN [dbo].[User2] u2 ON ui.UserId = u2.OldId;

-- 3. 删除外键和主键约束
ALTER TABLE [dbo].[UserInvitation] DROP CONSTRAINT [FK_UserInvitation_Users];
ALTER TABLE [dbo].[UserInvitation] DROP CONSTRAINT [PK_UserInvitation_1];

-- 4. 删除索引（如果存在）
DROP INDEX IX_UserInvitation_User ON [dbo].[UserInvitation]

-- 5. 删除旧的 UserId 列
ALTER TABLE [dbo].[UserInvitation] DROP COLUMN [UserId];

-- 6. 重命名新列为 UserId
EXEC sp_rename '[dbo].[UserInvitation].[UserId2]', 'UserId', 'COLUMN';

-- 7. 将新列设为 NOT NULL
ALTER TABLE [dbo].[UserInvitation] ALTER COLUMN [UserId] INT NOT NULL;

-- 8. 重新创建主键约束
ALTER TABLE [dbo].[UserInvitation]
ADD CONSTRAINT [PK_UserInvitation_1] PRIMARY KEY CLUSTERED ([UserId], [InvitationCodeId]);

-- 9. 添加新的外键约束
ALTER TABLE [dbo].[UserInvitation]
ADD CONSTRAINT [FK_UserInvitation_Users] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User2]([Id]);

-- 第三步：恢复 User 表

-- 1. 删除旧的 User 表
DROP TABLE [dbo].[User];

-- 2. 将 User2 表重命名为 User 表
EXEC sp_rename '[dbo].[User2]', 'User';

-- 3. 删除新的 User 表中的 OldId 列
ALTER TABLE [dbo].[User] DROP COLUMN [OldId];












-- ========================================
-- Step 1: 修改 InvitationCode 表
-- ========================================

-- 1.1 创建新的 InvitationCode2 表
CREATE TABLE [dbo].[InvitationCode2](
    [Id] INT IDENTITY(1,1) NOT NULL,  -- 新的自增主键
    [OldId] UNIQUEIDENTIFIER NOT NULL, -- 保存旧的唯一标识符
    [Value] NVARCHAR(100) NOT NULL,
    [Count] SMALLINT NOT NULL,
    [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_InvitationCode_CreatedAt] DEFAULT (GETDATE()),
    [IsDeleted] BIT NOT NULL CONSTRAINT [DF_InvitationCode_isDeleted] DEFAULT ((0)),
    [CreateUserId] INT NOT NULL,
    CONSTRAINT [InvitationCode2_pkey] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [InvitationCode2_value_key] UNIQUE NONCLUSTERED ([Value] ASC)
);

-- 1.2 将数据从旧的 InvitationCode 表复制到新的 InvitationCode2 表
INSERT INTO [dbo].[InvitationCode2] (
    [OldId], [Value], [Count], [CreatedAt], [IsDeleted], [CreateUserId]
)
SELECT
    [Id] AS OldId, [Value], [Count], [CreatedAt], [IsDeleted], [CreateUserId]
FROM [dbo].[InvitationCode];

-- 1.3 更新引用 InvitationCode 表的外键关系

-- 表：UserInvitation
ALTER TABLE [dbo].[UserInvitation]
ADD [InvitationCodeId2] INT NULL;

UPDATE ui
SET ui.InvitationCodeId2 = ic2.Id
FROM [dbo].[UserInvitation] ui
JOIN [dbo].[InvitationCode2] ic2 ON ui.InvitationCodeId = ic2.OldId;

ALTER TABLE [dbo].[UserInvitation] DROP CONSTRAINT [FK_UserInvitation_InvitationCode];
ALTER TABLE [dbo].[UserInvitation] DROP CONSTRAINT [PK_UserInvitation_1];
ALTER TABLE [dbo].[UserInvitation] DROP COLUMN [InvitationCodeId];

EXEC sp_rename '[dbo].[UserInvitation].[InvitationCodeId2]', 'InvitationCodeId', 'COLUMN';

ALTER TABLE [dbo].[UserInvitation] ALTER COLUMN [InvitationCodeId] INT NOT NULL;

ALTER TABLE [dbo].[UserInvitation]
ADD CONSTRAINT [PK_UserInvitation_1] PRIMARY KEY CLUSTERED ([UserId], [InvitationCodeId]);

ALTER TABLE [dbo].[UserInvitation]
ADD CONSTRAINT [FK_UserInvitation_InvitationCode] FOREIGN KEY ([InvitationCodeId]) REFERENCES [dbo].[InvitationCode2]([Id]);

-- 表：UserInitialConfig
ALTER TABLE [dbo].[UserInitialConfig]
ADD [InvitationCodeId2] INT NULL;

UPDATE uic
SET uic.InvitationCodeId2 = ic2.Id
FROM [dbo].[UserInitialConfig] uic
JOIN [dbo].[InvitationCode2] ic2 ON uic.InvitationCodeId = ic2.OldId;

ALTER TABLE [dbo].[UserInitialConfig] DROP CONSTRAINT [FK_UserInitialConfig_InvitationCode];
DROP INDEX [IX_UserInitialConfig_InvitationCodeId] ON [dbo].[UserInitialConfig];

ALTER TABLE [dbo].[UserInitialConfig] DROP COLUMN [InvitationCodeId];

EXEC sp_rename '[dbo].[UserInitialConfig].[InvitationCodeId2]', 'InvitationCodeId', 'COLUMN';

-- InvitationCodeId 在原表中是可空的，所以不需要设置为 NOT NULL

ALTER TABLE [dbo].[UserInitialConfig]
ADD CONSTRAINT [FK_UserInitialConfig_InvitationCode] FOREIGN KEY ([InvitationCodeId]) REFERENCES [dbo].[InvitationCode2]([Id]);

CREATE INDEX [IX_UserInitialConfig_InvitationCodeId] ON [dbo].[UserInitialConfig] ([InvitationCodeId]);

-- 1.4 删除旧的 InvitationCode 表，重命名新表，并删除 OldId 列
DROP TABLE [dbo].[InvitationCode];
EXEC sp_rename '[dbo].[InvitationCode2]', 'InvitationCode';
ALTER TABLE [dbo].[InvitationCode] DROP COLUMN [OldId];









-- ========================================
-- Step 2: 修改 UserInitialConfig 表
-- ========================================

-- 2.1 创建新的 UserInitialConfig2 表
CREATE TABLE [dbo].[UserInitialConfig2](
    [Id] INT IDENTITY(1,1) NOT NULL,  -- 新的自增主键
    [OldId] UNIQUEIDENTIFIER NOT NULL, -- 保存旧的唯一标识符
    [Name] NVARCHAR(50) NOT NULL,
    [LoginType] NVARCHAR(50) NULL,
    [Price] DECIMAL(32, 16) NOT NULL CONSTRAINT [DF_UserInitialConfig_price] DEFAULT ((0)),
    [Models] NVARCHAR(4000) NOT NULL CONSTRAINT [DF_UserInitialConfig_models] DEFAULT ('[]'),
    [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_UserInitialConfig_createdAt] DEFAULT (GETDATE()),
    [UpdatedAt] DATETIME2(7) NOT NULL,
    [InvitationCodeId] INT NULL,
    CONSTRAINT [PK_UserInitialConfig] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- 2.2 将数据从旧的 UserInitialConfig 表复制到新的 UserInitialConfig2 表
INSERT INTO [dbo].[UserInitialConfig2] (
    [OldId], [Name], [LoginType], [Price], [Models], [CreatedAt], [UpdatedAt], [InvitationCodeId]
)
SELECT
    [Id] AS OldId, [Name], [LoginType], [Price], [Models], [CreatedAt], [UpdatedAt], [InvitationCodeId]
FROM [dbo].[UserInitialConfig];

-- 2.3 删除旧的 UserInitialConfig 表，重命名新表，并删除 OldId 列
DROP TABLE [dbo].[UserInitialConfig];
EXEC sp_rename '[dbo].[UserInitialConfig2]', 'UserInitialConfig';
ALTER TABLE [dbo].[UserInitialConfig] DROP COLUMN [OldId];

-- 2.4 重建外键约束（已在步骤 1.3 中处理）





-- ========================================
-- Step 3: 修改 FileService 表
-- ========================================

-- 3.1 创建新的 FileService2 表
CREATE TABLE [dbo].[FileService2](
    [Id] INT IDENTITY(1,1) NOT NULL, -- 新的自增主键
    [OldId] UNIQUEIDENTIFIER NOT NULL, -- 保存旧的唯一标识符
    [Name] NVARCHAR(1000) NOT NULL,
    [Enabled] BIT NOT NULL CONSTRAINT [DF_FileServices_enabled] DEFAULT ((1)),
    [Type] NVARCHAR(1000) NOT NULL,
    [Configs] NVARCHAR(2048) NOT NULL CONSTRAINT [DF_FileServices_configs] DEFAULT ('{}'),
    [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_FileServices_createdAt] DEFAULT (GETDATE()),
    [UpdatedAt] DATETIME2(7) NOT NULL,
    CONSTRAINT [PK_FileServices2] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- 3.2 将数据从旧的 FileService 表复制到新的 FileService2 表
INSERT INTO [dbo].[FileService2] (
    [OldId], [Name], [Enabled], [Type], [Configs], [CreatedAt], [UpdatedAt]
)
SELECT
    [Id] AS OldId, [Name], [Enabled], [Type], [Configs], [CreatedAt], [UpdatedAt]
FROM [dbo].[FileService];

-- 3.3 更新引用 FileService 表的外键关系

-- 表：Model
ALTER TABLE [dbo].[Model]
ADD [FileServiceId2] INT NULL;

UPDATE m
SET m.FileServiceId2 = fs2.Id
FROM [dbo].[Model] m
JOIN [dbo].[FileService2] fs2 ON m.FileServiceId = fs2.OldId;

ALTER TABLE [dbo].[Model] DROP CONSTRAINT [FK_Model_FileService];
DROP INDEX [IX_Model_FileServiceId] ON [dbo].[Model];

ALTER TABLE [dbo].[Model] DROP COLUMN [FileServiceId];

EXEC sp_rename '[dbo].[Model].[FileServiceId2]', 'FileServiceId', 'COLUMN';

-- 原来的 FileServiceId 是可空的，因此无需设为 NOT NULL

ALTER TABLE [dbo].[Model]
ADD CONSTRAINT [FK_Model_FileServiceId] FOREIGN KEY ([FileServiceId]) REFERENCES [dbo].[FileService2]([Id]);

CREATE INDEX [IX_Model_FileServiceId] ON [dbo].[Model] ([FileServiceId]);

-- 3.4 删除旧的 FileService 表，重命名新表，并删除 OldId 列
DROP TABLE [dbo].[FileService];
EXEC sp_rename '[dbo].[FileService2]', 'FileService';
ALTER TABLE [dbo].[FileService] DROP COLUMN [OldId];




-- ========================================
-- Step 4: 修改 LoginService 表
-- ========================================

-- 4.1 创建新的 LoginService2 表
CREATE TABLE [dbo].[LoginService2](
    [Id] INT IDENTITY(1,1) NOT NULL, -- 新的自增主键
    [OldId] UNIQUEIDENTIFIER NOT NULL, -- 保存旧的唯一标识符
    [Type] NVARCHAR(1000) NOT NULL,
    [Enabled] BIT NOT NULL CONSTRAINT [DF_LoginServices_enabled] DEFAULT ((1)),
    [Configs] NVARCHAR(2048) NOT NULL CONSTRAINT [DF_LoginServices_configs] DEFAULT ('{}'),
    [CreatedAt] DATETIME2(7) NOT NULL CONSTRAINT [DF_LoginServices_createdAt] DEFAULT (GETDATE()),
    [UpdatedAt] DATETIME2(7) NOT NULL,
    CONSTRAINT [PK_LoginServices2] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- 4.2 将数据从旧的 LoginService 表复制到新的 LoginService2 表
INSERT INTO [dbo].[LoginService2] (
    [OldId], [Type], [Enabled], [Configs], [CreatedAt], [UpdatedAt]
)
SELECT
    [Id] AS OldId, [Type], [Enabled], [Configs], [CreatedAt], [UpdatedAt]
FROM [dbo].[LoginService];

-- 4.3 删除旧的 LoginService 表，重命名新表，并删除 OldId 列
DROP TABLE [dbo].[LoginService];
EXEC sp_rename '[dbo].[LoginService2]', 'LoginService';
ALTER TABLE [dbo].[LoginService] DROP COLUMN [OldId];







-- ========================================
-- Step 5: 修改 Session 表
-- ========================================

-- 5.1 创建新的 Session2 表
CREATE TABLE [dbo].[Session2](
    [Id] INT IDENTITY(1,1) NOT NULL, -- 新的自增主键
    [OldId] UNIQUEIDENTIFIER NOT NULL, -- 保存旧的唯一标识符
    [CreatedAt] DATETIME2(7) NOT NULL,
    [UpdatedAt] DATETIME2(7) NOT NULL,
    [UserId] INT NOT NULL,
    CONSTRAINT [PK_Sessions2] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- 5.2 将数据从旧的 Session 表复制到新的 Session2 表
INSERT INTO [dbo].[Session2] (
    [OldId], [CreatedAt], [UpdatedAt], [UserId]
)
SELECT
    [Id] AS OldId, [CreatedAt], [UpdatedAt], [UserId]
FROM [dbo].[Session];

-- 5.3 删除外键约束和索引
ALTER TABLE [dbo].[Session] DROP CONSTRAINT [FK_Session_UserId];
DROP INDEX [IX_Session_UserId] ON [dbo].[Session];

-- 5.4 删除旧的 Session 表，重命名新表，并删除 OldId 列
DROP TABLE [dbo].[Session];
EXEC sp_rename '[dbo].[Session2]', 'Session';
ALTER TABLE [dbo].[Session] DROP COLUMN [OldId];

-- 5.5 重新创建外键约束和索引
ALTER TABLE [dbo].[Session]
ADD CONSTRAINT [FK_Session_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User]([Id]);

CREATE INDEX [IX_Session_UserId] ON [dbo].[Session] ([UserId]);






-- ========================================
-- Step 6: 修改 UserBalance 表
-- ========================================

-- 6.1 创建新的 UserBalance2 表
CREATE TABLE [dbo].[UserBalance2](
    [Id] INT IDENTITY(1,1) NOT NULL, -- 新的自增主键
    [OldId] UNIQUEIDENTIFIER NOT NULL, -- 保存旧的唯一标识符
    [Balance] DECIMAL(32, 16) NOT NULL,
    [CreatedAt] DATETIME2(7) NOT NULL,
    [UpdatedAt] DATETIME2(7) NOT NULL,
    [UserId] INT NOT NULL,
    CONSTRAINT [PK_UserBalances2] PRIMARY KEY CLUSTERED ([Id] ASC)
);

-- 6.2 将数据从旧的 UserBalance 表复制到新的 UserBalance2 表
INSERT INTO [dbo].[UserBalance2] (
    [OldId], [Balance], [CreatedAt], [UpdatedAt], [UserId]
)
SELECT
    [Id] AS OldId, [Balance], [CreatedAt], [UpdatedAt], [UserId]
FROM [dbo].[UserBalance];

-- 6.3 删除外键约束和索引
ALTER TABLE [dbo].[UserBalance] DROP CONSTRAINT [FK_UserBalance_UserId];
DROP INDEX [UserBalances_userId_key] ON [dbo].[UserBalance];

-- 6.4 删除旧的 UserBalance 表，重命名新表，并删除 OldId 列
DROP TABLE [dbo].[UserBalance];
EXEC sp_rename '[dbo].[UserBalance2]', 'UserBalance';
ALTER TABLE [dbo].[UserBalance] DROP COLUMN [OldId];

-- 6.5 重新创建外键约束和索引
ALTER TABLE [dbo].[UserBalance]
ADD CONSTRAINT [FK_UserBalance_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User]([Id]);

CREATE UNIQUE INDEX [UserBalances_userId_key] ON [dbo].[UserBalance] ([UserId]);


DROP TABLE [Session]

ALTER TABLE dbo.UserInitialConfig ADD CONSTRAINT
	FK_UserInitialConfig_InvitationCode FOREIGN KEY
	(
	InvitationCodeId
	) REFERENCES dbo.InvitationCode
	(
	Id
	) ON UPDATE  SET NULL 
	 ON DELETE  SET NULL 