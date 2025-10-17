PRINT N'给ChatConfig表增加CodeExecutionEnabled字段';
IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'CodeExecutionEnabled' AND Object_ID = Object_ID(N'dbo.ChatConfig'))
BEGIN
    ALTER TABLE dbo.ChatConfig ADD CodeExecutionEnabled BIT NOT NULL CONSTRAINT DF_ChatConfig_CodeExecutionEnabled DEFAULT 0;
    ALTER TABLE dbo.ChatConfig DROP CONSTRAINT DF_ChatConfig_CodeExecutionEnabled;
END

PRINT N'给ChatConfig所有非0的ReasoningEffort加1以适应ReasoningEffort=Minimal的新增选项(1/2/3->2/3/4),如果数据库中没有为4的值,则加一次';
IF NOT EXISTS(SELECT * FROM dbo.ChatConfig WHERE ReasoningEffort = 4)
BEGIN
    UPDATE dbo.ChatConfig SET ReasoningEffort = ReasoningEffort + 1 WHERE ReasoningEffort <> 0;
END