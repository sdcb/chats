CREATE CREDENTIAL [s3://io.starworks.cc:88/cv-private]
WITH
  IDENTITY = 'S3 Access Key',
  SECRET = '***:****';
GO


use master
GO
ALTER DATABASE [Chats3] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

RESTORE DATABASE [Chats3] 
FROM URL = N's3://io.starworks.cc:88/cv-private/2025/chats/backup-latest.bak' WITH REPLACE,
	FILE = 1,
	MOVE N'Chats3' TO N'D:\mssql-data\Chats3.mdf',
	MOVE N'Chats3_log' TO N'D:\mssql-data\Chats3.ldf',  
	--MOVE N'Chats3' TO N'/var/opt/mssql/data/Chats3.mdf',
	--MOVE N'Chats3_log' TO N'/var/opt/mssql/data/Chats3.ldf',  
	NOUNLOAD, STATS = 1;

ALTER DATABASE [Chats3] SET MULTI_USER;

use [Chats3];
GO
ALTER USER [chats] WITH LOGIN = [chats];
