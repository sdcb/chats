use [master]
GO

ALTER DATABASE [Chats3] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;

RESTORE DATABASE [Chats3] 
FROM URL = N'https://richsgp.blob.core.windows.net/backup/chats3/latest.bak' WITH REPLACE,
	FILE = 1,
	MOVE N'Chats3' TO N'/var/opt/mssql/data/Chats3.mdf',
	MOVE N'Chats3_log' TO N'/var/opt/mssql/data/Chats3.ldf',  NOUNLOAD,
	STATS = 5;

ALTER DATABASE [Chats3] SET MULTI_USER;

use [Chats3];
GO
ALTER USER [chats] WITH LOGIN = [chats];
