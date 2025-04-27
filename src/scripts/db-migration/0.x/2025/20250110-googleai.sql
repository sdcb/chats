INSERT INTO [ModelProvider]([Id], [Name], [InitialHost], [InitialSecret])
VALUES (13, 'Google AI', NULL, '');

-- Region Parameters
DECLARE @p0 SmallInt = 1300
DECLARE @p1 SmallInt = 13
DECLARE @p2 NVarChar(1000) = 'gemini-2.0-flash-thinking-exp'
DECLARE @p3 NVarChar(1000) = 'gemini'
DECLARE @p4 Bit = 0
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 1
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 Int = 40000
DECLARE @p12 Int = 8000
DECLARE @p13 SmallInt = null
DECLARE @p14 Decimal(6,5) = 0
DECLARE @p15 Decimal(6,5) = 0
DECLARE @p16 Char(3) = 'USD'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [ShortName], [IsLegacy], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1301
DECLARE @p1 SmallInt = 13
DECLARE @p2 NVarChar(1000) = 'gemini-2.0-flash-exp'
DECLARE @p3 NVarChar(1000) = 'gemini'
DECLARE @p4 Bit = 0
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 1
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 Int = 1048576
DECLARE @p12 Int = 8000
DECLARE @p13 SmallInt = null
DECLARE @p14 Decimal(6,5) = 0
DECLARE @p15 Decimal(6,5) = 0
DECLARE @p16 Char(3) = 'USD'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [ShortName], [IsLegacy], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1302
DECLARE @p1 SmallInt = 13
DECLARE @p2 NVarChar(1000) = 'gemini-exp-1206'
DECLARE @p3 NVarChar(1000) = 'gemini'
DECLARE @p4 Bit = 0
DECLARE @p5 Decimal(3,2) = 0
DECLARE @p6 Decimal(3,2) = 2
DECLARE @p7 Bit = 0
DECLARE @p8 Bit = 1
DECLARE @p9 Bit = 1
DECLARE @p10 Bit = 1
DECLARE @p11 Int = 2097152
DECLARE @p12 Int = 8000
DECLARE @p13 SmallInt = null
DECLARE @p14 Decimal(6,5) = 0
DECLARE @p15 Decimal(6,5) = 0
DECLARE @p16 Char(3) = 'USD'
-- EndRegion
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [ShortName], [IsLegacy], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16)