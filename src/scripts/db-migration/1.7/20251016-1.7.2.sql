PRINT N'* 给ChatConfig表增加CodeExecutionEnabled字段';
IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'CodeExecutionEnabled' AND Object_ID = Object_ID(N'dbo.ChatConfig'))
BEGIN
    ALTER TABLE dbo.ChatConfig ADD CodeExecutionEnabled BIT NOT NULL CONSTRAINT DF_ChatConfig_CodeExecutionEnabled DEFAULT 0;
    ALTER TABLE dbo.ChatConfig DROP CONSTRAINT DF_ChatConfig_CodeExecutionEnabled;
END

PRINT N'* 调整ChatConfig的ReasoningEffort：所有非0值加1以适配新增的Minimal级别(1/2/3->2/3/4)，仅在当前不存在值4时执行';
IF NOT EXISTS(SELECT * FROM dbo.ChatConfig WHERE ReasoningEffort = 4)
BEGIN
    UPDATE dbo.ChatConfig SET ReasoningEffort = ReasoningEffort + 1 WHERE ReasoningEffort <> 0;
END