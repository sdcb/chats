CREATE TABLE [dbo].[UserApiCache] (
    [Id]              INT           IDENTITY (1, 1) NOT NULL,
    [UserApiKeyId]    INT           NOT NULL,
    [ModelId]         SMALLINT      NOT NULL,
    [RequestHashCode] BIGINT        NOT NULL,
    [Expires]         DATETIME2 (7) NOT NULL,
    [ClientInfoId]    INT           NOT NULL,
    [CreatedAt]       DATETIME2 (7) NOT NULL,
    CONSTRAINT [PK_UserApiCache] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserApiCache_ClientInfoId] FOREIGN KEY ([ClientInfoId]) REFERENCES [dbo].[ClientInfo] ([Id]),
    CONSTRAINT [FK_UserApiCache_ModelId] FOREIGN KEY ([ModelId]) REFERENCES [dbo].[Model] ([Id]),
    CONSTRAINT [FK_UserApiCache_UserApiKeyId] FOREIGN KEY ([UserApiKeyId]) REFERENCES [dbo].[UserApiKey] ([Id]) ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE NONCLUSTERED INDEX [IX_UserApiCache_CreatedAt]
    ON [dbo].[UserApiCache]([Expires] ASC);

CREATE NONCLUSTERED INDEX [IX_UserApiCache_RequestHashCode]
    ON [dbo].[UserApiCache]([RequestHashCode] ASC);

CREATE NONCLUSTERED INDEX [IX_UserApiCache_ClientInfoId]
    ON [dbo].[UserApiCache]([ClientInfoId] ASC);

CREATE NONCLUSTERED INDEX [IX_UserApiCache_ModelId]
    ON [dbo].[UserApiCache]([ModelId] ASC);

CREATE NONCLUSTERED INDEX [IX_UserApiCache_UserApiKeyId]
    ON [dbo].[UserApiCache]([UserApiKeyId] ASC);



CREATE TABLE [dbo].[UserApiCacheBody] (
    [UserApiCacheId] INT            NOT NULL,
    [Request]        NVARCHAR (MAX) NOT NULL,
    [Response]       NVARCHAR (MAX) NOT NULL,
    CONSTRAINT [PK_UserApiCacheBody] PRIMARY KEY CLUSTERED ([UserApiCacheId] ASC),
    CONSTRAINT [FK_UserApiCacheBody_Id] FOREIGN KEY ([UserApiCacheId]) REFERENCES [dbo].[UserApiCache] ([Id]) ON DELETE CASCADE ON UPDATE CASCADE
);

CREATE TABLE [dbo].[UserApiCacheUsage] (
    [Id]              BIGINT         IDENTITY (1, 1) NOT NULL,
    [UserApiCacheId]  INT            NOT NULL,
    [ClientInfoId]    INT            NOT NULL,
    [UsedAt]          DATETIME2(7)   NOT NULL,
    CONSTRAINT [PK_UserApiCacheUsage] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserApiCacheUsage_UserApiCacheId]   FOREIGN KEY ([UserApiCacheId])
        REFERENCES [dbo].[UserApiCache] ([Id]) ON DELETE CASCADE ON UPDATE CASCADE,
    CONSTRAINT [FK_UserApiCacheUsage_ClientInfoId]     FOREIGN KEY ([ClientInfoId])
        REFERENCES [dbo].[ClientInfo] ([Id])
            ON DELETE CASCADE ON UPDATE CASCADE
);