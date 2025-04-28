-- Region Parameters
DECLARE @p0 SmallInt = 119
DECLARE @p1 SmallInt = 1
DECLARE @p2 NVarChar(1000) = 'o3-mini-2025-01-31'
DECLARE @p3 NVarChar(1000) = 'o3-mini'
DECLARE @p4 Date = '2025-02-01'
DECLARE @p5 Decimal(3,2) = 1
DECLARE @p6 Decimal(3,2) = 1
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 Int = 200000
DECLARE @p12 Int = 100000
DECLARE @p13 SmallInt = 2
DECLARE @p14 Decimal(8,5) = 1.1
DECLARE @p15 Decimal(8,5) = 4.4
DECLARE @p16 Char(3) = 'USD'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 519
DECLARE @p1 SmallInt = 5
DECLARE @p2 NVarChar(1000) = 'o3-mini-2025-01-31'
DECLARE @p3 NVarChar(1000) = 'o3-mini'
DECLARE @p4 Date = '2025-02-01'
DECLARE @p5 Decimal(3,2) = 1
DECLARE @p6 Decimal(3,2) = 1
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 Int = 200000
DECLARE @p12 Int = 100000
DECLARE @p13 SmallInt = 2
DECLARE @p14 Decimal(8,5) = 1.1
DECLARE @p15 Decimal(8,5) = 4.4
DECLARE @p16 Char(3) = 'USD'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1220
DECLARE @p1 Bit = 1
-- EndRegion
UPDATE [ModelReference]
SET [AllowStreaming] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1221
DECLARE @p1 SmallInt = 12
DECLARE @p2 NVarChar(1000) = 'o3-mini-2025-01-31'
DECLARE @p3 NVarChar(1000) = 'o3-mini'
DECLARE @p4 Date = '2025-02-01'
DECLARE @p5 Decimal(3,2) = 1
DECLARE @p6 Decimal(3,2) = 1
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 Int = 200000
DECLARE @p12 Int = 100000
DECLARE @p13 SmallInt = 2
DECLARE @p14 Decimal(8,5) = 1.1
DECLARE @p15 Decimal(8,5) = 4.4
DECLARE @p16 Char(3) = 'USD'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16)
GO


-- Region Parameters
DECLARE @p0 SmallInt = 1222
DECLARE @p1 SmallInt = 12
DECLARE @p2 NVarChar(1000) = 'deepseek-r1'
DECLARE @p3 NVarChar(1000) = 'deepseek'
DECLARE @p4 Date = null
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 0
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 Int = 128000
DECLARE @p12 Int = 4000
DECLARE @p13 SmallInt = null
DECLARE @p14 Decimal(6,5) = 0
DECLARE @p15 Decimal(6,5) = 0
DECLARE @p16 Char(3) = 'RMB'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16)
GO