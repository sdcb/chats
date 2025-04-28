-- Region Parameters
DECLARE @p0 SmallInt = 1301
DECLARE @p1 NVarChar(1000) = 'gemini-2.0-flash'
DECLARE @p2 Date = '2025-02-06'
DECLARE @p3 Int = 8192
DECLARE @p4 Decimal(8,5) = 0.1
DECLARE @p5 Decimal(8,5) = 0.4
-- EndRegion
UPDATE [ModelReference]
SET [Name] = @p1, [PublishDate] = @p2, [MaxResponseTokens] = @p3, [InputTokenPrice1M] = @p4, [OutputTokenPrice1M] = @p5
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1302
DECLARE @p1 NVarChar(1000) = 'gemini-2.0-pro-exp'
-- EndRegion
UPDATE [ModelReference]
SET [Name] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1300
DECLARE @p1 Int = 1048576
-- EndRegion
UPDATE [ModelReference]
SET [ContextWindow] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 1303
DECLARE @p1 SmallInt = 13
DECLARE @p2 NVarChar(1000) = 'gemini-2.0-flash-lite-preview'
DECLARE @p3 NVarChar(1000) = 'gemini'
DECLARE @p4 Date = null
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
INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12, @p13, @p14, @p15, @p16)
GO

-- Region Parameters
DECLARE @p0 SmallInt = 901
DECLARE @p1 Date = '2024-05-20'
-- EndRegion
UPDATE [ModelReference]
SET [PublishDate] = @p1
WHERE [Id] = @p0
GO

-- Region Parameters
DECLARE @p0 SmallInt = 900
DECLARE @p1 Date = '2025-01-11'
-- EndRegion
UPDATE [ModelReference]
SET [PublishDate] = @p1
WHERE [Id] = @p0
GO