IF NOT EXISTS (SELECT 1 FROM sys.credentials WHERE name = 's3://io.starworks.cc:88/cv-private')
BEGIN
  CREATE CREDENTIAL [s3://io.starworks.cc:88/cv-private]
  WITH
    IDENTITY = 'S3 Access Key',
    SECRET = '***:****';
END;
GO


use master
GO
ALTER DATABASE [Chats3] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

DECLARE @dataFile NVARCHAR(260);
DECLARE @logFile NVARCHAR(260);

IF EXISTS (SELECT 1 FROM sys.dm_os_host_info WHERE host_platform = 'Linux')
BEGIN
  SET @dataFile = N'/var/opt/mssql/data/Chats3.mdf';
  SET @logFile = N'/var/opt/mssql/data/Chats3.ldf';
END
ELSE
BEGIN
  SET @dataFile = N'D:\mssql-data\Chats3.mdf';
  SET @logFile = N'D:\mssql-data\Chats3.ldf';
END;

RESTORE DATABASE [Chats3] 
FROM URL = N's3://io.starworks.cc:88/cv-private/2025/chats/backup-latest.bak' WITH REPLACE,
	FILE = 1,
	MOVE N'Chats3' TO @dataFile,
	MOVE N'Chats3_log' TO @logFile,  
	NOUNLOAD, STATS = 1;

ALTER DATABASE [Chats3] SET MULTI_USER;

use [Chats3];
GO
ALTER USER [chats] WITH LOGIN = [chats];
