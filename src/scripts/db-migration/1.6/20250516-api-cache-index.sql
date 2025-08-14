CREATE NONCLUSTERED INDEX [IX_UserApiCacheUsage_UserApiCacheId]
    ON [dbo].[UserApiCacheUsage]([UserApiCacheId] ASC);


CREATE NONCLUSTERED INDEX [IX_UserApiCacheUsage_ClientInfoId]
    ON [dbo].[UserApiCacheUsage]([ClientInfoId] ASC);