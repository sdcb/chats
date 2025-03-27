CREATE TABLE dbo.ChatPreset
(
    Id INT NOT NULL IDENTITY(1, 1),
    Name NVARCHAR(50) NOT NULL,
    UserId INT NOT NULL,
    UpdatedAt DATETIME2(7) NOT NULL,
    CONSTRAINT PK_ChatPreset PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_ChatPreset_User FOREIGN KEY (UserId) REFERENCES dbo.[User](Id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    INDEX IX_ChatPreset_UserId NONCLUSTERED (UserId),
    INDEX IX_ChatPreset_Name NONCLUSTERED (Name)
);

CREATE TABLE dbo.ChatPresetSpan
(
    ChatPresetId INT NOT NULL,
    SpanId TINYINT NOT NULL,
    ChatConfigId INT NOT NULL,
    Enabled BIT NOT NULL CONSTRAINT DF_ChatPresetSpan_Enabled DEFAULT 1,
    CONSTRAINT PK_ChatPresetSpan PRIMARY KEY CLUSTERED (ChatPresetId, SpanId),
    CONSTRAINT FK_ChatPresetSpan_Preset FOREIGN KEY (ChatPresetId) REFERENCES dbo.ChatPreset(Id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    CONSTRAINT FK_ChatPresetSpan_Config FOREIGN KEY (ChatConfigId) REFERENCES dbo.ChatConfig(Id)
        ON UPDATE CASCADE
        ON DELETE NO ACTION,
    INDEX IX_ChatPresetSpan_Config NONCLUSTERED (ChatConfigId)
);

INSERT INTO [ModelReference]([Id], [ProviderId], [Name], [DisplayName], [PublishDate], [MinTemperature], [MaxTemperature], [AllowSearch], [AllowVision], [AllowSystemPrompt], [AllowStreaming], [ReasoningResponseKindId], [ContextWindow], [MaxResponseTokens], [TokenizerId], [InputTokenPrice1M], [OutputTokenPrice1M], [CurrencyCode])
VALUES 
(120, 1, 'gpt-4.5-preview', 'gpt-4.5', '2025-02-27', 0, 2, 0, 1, 1, 1, 0, 128000, 16384, 2, 75, 150, 'USD'),
(520, 5, 'gpt-4.5-preview', 'gpt-4.5', '2025-02-27', 0, 2, 0, 1, 1, 1, 0, 128000, 16384, 2, 75, 150, 'USD');