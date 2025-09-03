USE master;
BEGIN TRY
    -- 设置ChatsDEV为SINGLE_USER模式
    ALTER DATABASE [ChatsDEV] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

    -- 恢复Chats3到ChatsDEV
    RESTORE DATABASE [ChatsDEV]
    FROM URL = 'https://richsgp.blob.core.windows.net/backup/chats3/latest.bak'
    WITH REPLACE,
    MOVE 'Chats3' TO '/var/opt/mssql/data/ChatsDEV.mdf',
    MOVE 'Chats3_log' TO '/var/opt/mssql/data/ChatsDEV.ldf';
END TRY
BEGIN CATCH
    -- 捕获错误并显示错误信息
    PRINT 'An error occurred: ' + ERROR_MESSAGE();
END CATCH


-- 无论是否发生错误，将数据库模式设置回MULTI_USER
ALTER DATABASE [ChatsDEV] SET MULTI_USER;

-- 创建用户并赋予权限
USE [ChatsDEV];
CREATE USER [chats-dev] FOR LOGIN [chats-dev];
EXEC sp_addrolemember 'db_datareader', 'chats-dev';
EXEC sp_addrolemember 'db_datawriter', 'chats-dev';
