-- =============================================
-- 1.8.0 数据库迁移脚本
-- 将模型能力配置从 ModelReference 迁移到 Model 表
-- =============================================

-- =============================================
-- 第一步：给 Model 表添加新字段
-- =============================================

PRINT N'* 给 Model 表添加模型能力配置字段';

-- 从 ModelReference 迁移的字段
IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'AllowSearch' AND Object_ID = Object_ID(N'dbo.Model'))
BEGIN
    ALTER TABLE dbo.Model ADD AllowSearch BIT NOT NULL CONSTRAINT DF_Model_AllowSearch DEFAULT 0;
    ALTER TABLE dbo.Model DROP CONSTRAINT DF_Model_AllowSearch;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'AllowVision' AND Object_ID = Object_ID(N'dbo.Model'))
BEGIN
    ALTER TABLE dbo.Model ADD AllowVision BIT NOT NULL CONSTRAINT DF_Model_AllowVision DEFAULT 0;
    ALTER TABLE dbo.Model DROP CONSTRAINT DF_Model_AllowVision;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'AllowSystemPrompt' AND Object_ID = Object_ID(N'dbo.Model'))
BEGIN
    ALTER TABLE dbo.Model ADD AllowSystemPrompt BIT NOT NULL CONSTRAINT DF_Model_AllowSystemPrompt DEFAULT 1;
    ALTER TABLE dbo.Model DROP CONSTRAINT DF_Model_AllowSystemPrompt;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'AllowStreaming' AND Object_ID = Object_ID(N'dbo.Model'))
BEGIN
    ALTER TABLE dbo.Model ADD AllowStreaming BIT NOT NULL CONSTRAINT DF_Model_AllowStreaming DEFAULT 1;
    ALTER TABLE dbo.Model DROP CONSTRAINT DF_Model_AllowStreaming;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'ThinkTagParserEnabled' AND Object_ID = Object_ID(N'dbo.Model'))
BEGIN
    -- 0为默认，1为启用（对应之前的 ReasoningResponseKindId = 2:ThinkTag）
    ALTER TABLE dbo.Model ADD ThinkTagParserEnabled BIT NOT NULL CONSTRAINT DF_Model_ThinkTagParserEnabled DEFAULT 0;
    ALTER TABLE dbo.Model DROP CONSTRAINT DF_Model_ThinkTagParserEnabled;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'MinTemperature' AND Object_ID = Object_ID(N'dbo.Model'))
BEGIN
    ALTER TABLE dbo.Model ADD MinTemperature DECIMAL(3, 2) NOT NULL CONSTRAINT DF_Model_MinTemperature DEFAULT 0;
    ALTER TABLE dbo.Model DROP CONSTRAINT DF_Model_MinTemperature;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'MaxTemperature' AND Object_ID = Object_ID(N'dbo.Model'))
BEGIN
    ALTER TABLE dbo.Model ADD MaxTemperature DECIMAL(3, 2) NOT NULL CONSTRAINT DF_Model_MaxTemperature DEFAULT 2;
    ALTER TABLE dbo.Model DROP CONSTRAINT DF_Model_MaxTemperature;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'ContextWindow' AND Object_ID = Object_ID(N'dbo.Model'))
BEGIN
    ALTER TABLE dbo.Model ADD ContextWindow INT NOT NULL CONSTRAINT DF_Model_ContextWindow DEFAULT 0;
    ALTER TABLE dbo.Model DROP CONSTRAINT DF_Model_ContextWindow;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'MaxResponseTokens' AND Object_ID = Object_ID(N'dbo.Model'))
BEGIN
    ALTER TABLE dbo.Model ADD MaxResponseTokens INT NOT NULL CONSTRAINT DF_Model_MaxResponseTokens DEFAULT 0;
    ALTER TABLE dbo.Model DROP CONSTRAINT DF_Model_MaxResponseTokens;
END

-- 从代码硬编码改为数据库字段
IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'AllowCodeExecution' AND Object_ID = Object_ID(N'dbo.Model'))
BEGIN
    ALTER TABLE dbo.Model ADD AllowCodeExecution BIT NOT NULL CONSTRAINT DF_Model_AllowCodeExecution DEFAULT 0;
    ALTER TABLE dbo.Model DROP CONSTRAINT DF_Model_AllowCodeExecution;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'ReasoningEffortOptions' AND Object_ID = Object_ID(N'dbo.Model'))
BEGIN
    -- 存储支持的推理等级列表，如 "1,2,3" 或 "0,1,2,3"，空字符串表示不支持
    ALTER TABLE dbo.Model ADD ReasoningEffortOptions NVARCHAR(50) NOT NULL CONSTRAINT DF_Model_ReasoningEffortOptions DEFAULT '';
    ALTER TABLE dbo.Model DROP CONSTRAINT DF_Model_ReasoningEffortOptions;
END

-- 扩展性字段
IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'AllowToolCall' AND Object_ID = Object_ID(N'dbo.Model'))
BEGIN
    -- 默认支持工具调用，图片生成模型除外
    ALTER TABLE dbo.Model ADD AllowToolCall BIT NOT NULL CONSTRAINT DF_Model_AllowToolCall DEFAULT 1;
    ALTER TABLE dbo.Model DROP CONSTRAINT DF_Model_AllowToolCall;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'SupportedImageSizes' AND Object_ID = Object_ID(N'dbo.Model'))
BEGIN
    -- 存储支持的图片生成分辨率列表，如 "1024x1024,1792x1024,1024x1792"，空字符串表示不支持
    ALTER TABLE dbo.Model ADD SupportedImageSizes NVARCHAR(200) NOT NULL CONSTRAINT DF_Model_SupportedImageSizes DEFAULT '';
    ALTER TABLE dbo.Model DROP CONSTRAINT DF_Model_SupportedImageSizes;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'ApiType' AND Object_ID = Object_ID(N'dbo.Model'))
BEGIN
    -- 0: ChatCompletion API, 1: Response API
    ALTER TABLE dbo.Model ADD ApiType TINYINT NOT NULL CONSTRAINT DF_Model_ApiType DEFAULT 0;
    ALTER TABLE dbo.Model DROP CONSTRAINT DF_Model_ApiType;
END

GO

-- =============================================
-- 第二步：数据迁移
-- =============================================

PRINT N'* 从 ModelReference 迁移数据到 Model 表';

-- 迁移基础字段（从 ModelReference 复制）
UPDATE m
SET 
    m.AllowSearch = mr.AllowSearch,
    m.AllowVision = mr.AllowVision,
    m.AllowSystemPrompt = mr.AllowSystemPrompt,
    m.AllowStreaming = mr.AllowStreaming,
    m.ThinkTagParserEnabled = CASE WHEN mr.ReasoningResponseKindId = 2 THEN 1 ELSE 0 END,
    m.MinTemperature = mr.MinTemperature,
    m.MaxTemperature = mr.MaxTemperature,
    m.ContextWindow = mr.ContextWindow,
    m.MaxResponseTokens = mr.MaxResponseTokens
FROM dbo.Model m
INNER JOIN dbo.ModelReference mr ON m.ModelReferenceId = mr.Id
WHERE 
    m.AllowSearch = 0 AND m.AllowVision = 0 AND m.AllowSystemPrompt = 1 
    AND m.AllowStreaming = 1 AND m.ThinkTagParserEnabled = 0 
    AND m.MinTemperature = 0 AND m.MaxTemperature = 2
    AND m.ContextWindow = 0 AND m.MaxResponseTokens = 0;

GO

PRINT N'* 迁移 AllowCodeExecution 字段（基于模型名称判断）';

-- 迁移 AllowCodeExecution
-- gemini-2.0-flash-lite, gemini-2.0-flash-exp, gemini-2.0-flash-exp-image-generation => false
-- 其他以 "gemini-" 开头的模型 => true
UPDATE m
SET m.AllowCodeExecution = 0
FROM dbo.Model m
INNER JOIN dbo.ModelReference mr ON m.ModelReferenceId = mr.Id
WHERE mr.Name IN (
    'gemini-2.0-flash-lite', 
    'gemini-2.0-flash-exp', 
    'gemini-2.0-flash-exp-image-generation'
);

UPDATE m
SET m.AllowCodeExecution = 1
FROM dbo.Model m
INNER JOIN dbo.ModelReference mr ON m.ModelReferenceId = mr.Id
WHERE mr.Name LIKE 'gemini-%'
  AND mr.Name NOT IN (
    'gemini-2.0-flash-lite', 
    'gemini-2.0-flash-exp', 
    'gemini-2.0-flash-exp-image-generation'
  );

GO

PRINT N'* 迁移 ReasoningEffortOptions 字段（基于模型名称判断）';

-- 迁移 ReasoningEffortOptions
-- TranditionalReasoning (Low=2, Medium=3, High=4) => "2,3,4"
UPDATE m
SET m.ReasoningEffortOptions = '2,3,4'
FROM dbo.Model m
INNER JOIN dbo.ModelReference mr ON m.ModelReferenceId = mr.Id
WHERE mr.Name IN (
    'grok-3-mini', 'grok-3-mini-fast',
    'o1-2024-12-17', 'o3', 'o3-pro', 'o3-mini-2025-01-31', 'gpt-5-codex',
    'o4-mini', 'codex-mini',
    'gpt-image-1', 'gpt-image-1-mini'
);

-- Gpt5Reasoning (Minimal=1, Low=2, Medium=3, High=4) => "1,2,3,4"
UPDATE m
SET m.ReasoningEffortOptions = '1,2,3,4'
FROM dbo.Model m
INNER JOIN dbo.ModelReference mr ON m.ModelReferenceId = mr.Id
WHERE mr.Name IN (
    'gpt-5', 'gpt-5-mini', 'gpt-5-nano'
);

-- Compatible (Low=2) => "2"
UPDATE m
SET m.ReasoningEffortOptions = '2'
FROM dbo.Model m
INNER JOIN dbo.ModelReference mr ON m.ModelReferenceId = mr.Id
WHERE mr.Name IN (
    'gemini-2.5-pro', 'gemini-2.5-flash',
    'Qwen/Qwen3-235B-A22B', 'Qwen/Qwen3-30B-A3B', 'Qwen/Qwen3-32B', 'Qwen/Qwen3-14B', 'Qwen/Qwen3-8B',
    'qwen3-235b-a22b', 'qwen3-30b-a3b', 'qwen3-32b', 'qwen3-14b', 'qwen3-8b', 'qwen3-4b', 'qwen3-1.7b', 'qwen3-0.6b'
);

GO

PRINT N'* 迁移 SupportedImageSizes 并设置图片生成模型的 AllowToolCall';

-- 迁移 SupportedImageSizes：根据模型名称判断是否为图片生成模型
-- gpt-image-1 和 gpt-image-1-mini 支持特定的图片尺寸
UPDATE m
SET m.SupportedImageSizes = '1024x1024,1792x1024,1024x1792'
FROM dbo.Model m
INNER JOIN dbo.ModelReference mr ON m.ModelReferenceId = mr.Id
WHERE mr.Name IN ('gpt-image-1', 'gpt-image-1-mini');

-- 图片生成模型不支持工具调用
UPDATE m
SET m.AllowToolCall = 0
FROM dbo.Model m
WHERE m.SupportedImageSizes <> '';

GO

PRINT N'* 数据迁移完成';
