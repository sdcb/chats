ALTER TABLE [dbo].[ModelReference] DROP CONSTRAINT [FK_ModelReference_ReasoningResponseKind];
ALTER TABLE [dbo].[ModelReference] DROP CONSTRAINT [FK_ModelReference_Tokenizer];
ALTER TABLE [dbo].[ModelReference] DROP CONSTRAINT [DF_DefaultModelSetting_AllowVision];
ALTER TABLE [dbo].[ModelReference] DROP CONSTRAINT [DF_ModelReference_AllowSystemPrompt];
ALTER TABLE [dbo].[ModelReference] DROP CONSTRAINT [DF_ModelReference_AllowStreaming];
ALTER TABLE [dbo].[ModelReference] DROP CONSTRAINT [DF_ModelReference_ReasoningResponseKindId];
ALTER TABLE [dbo].[ModelReference] DROP CONSTRAINT [DF_ModelDefaults_ContextWindow];
ALTER TABLE [dbo].[ModelReference] DROP CONSTRAINT [DF_ModelDefaults_MaxResponseTokens];

CREATE TABLE [dbo].[ChatModelReference]
(
    [Id]                  SMALLINT       NOT NULL,
    [MinTemperature]      DECIMAL(3, 2)  NOT NULL,
    [MaxTemperature]      DECIMAL(3, 2)  NOT NULL,
    [AllowSearch]         BIT            NOT NULL,
    [AllowVision]         BIT            NOT NULL,
    [AllowSystemPrompt]   BIT            NOT NULL,
    [AllowStreaming]      BIT            NOT NULL,
    [ReasoningResponseKindId] TINYINT    NOT NULL,
    [ContextWindow]       INT            NOT NULL,
    [MaxResponseTokens]   INT            NOT NULL,
    [TokenizerId]         SMALLINT       NULL,
    [InputTokenPrice1M]   DECIMAL(9, 5)  NOT NULL,
    [OutputTokenPrice1M]  DECIMAL(9, 5)  NOT NULL,

    CONSTRAINT [PK_ChatModelReference] PRIMARY KEY CLUSTERED([Id] ASC),

    CONSTRAINT [FK_ChatModelReference_ModelReference]
        FOREIGN KEY([Id]) REFERENCES [dbo].[ModelReference]([Id]) 
        ON DELETE CASCADE
);
GO

INSERT INTO [dbo].[ChatModelReference] (
      [Id]
    , [MinTemperature]
    , [MaxTemperature]
    , [AllowSearch]
    , [AllowVision]
    , [AllowSystemPrompt]
    , [AllowStreaming]
    , [ReasoningResponseKindId]
    , [ContextWindow]
    , [MaxResponseTokens]
    , [TokenizerId]
    , [InputTokenPrice1M]
    , [OutputTokenPrice1M]
)
SELECT 
      [Id]
    , [MinTemperature]
    , [MaxTemperature]
    , [AllowSearch]
    , [AllowVision]
    , [AllowSystemPrompt]
    , [AllowStreaming]
    , [ReasoningResponseKindId]
    , [ContextWindow]
    , [MaxResponseTokens]
    , [TokenizerId]
    , [InputTokenPrice1M]
    , [OutputTokenPrice1M]
FROM [dbo].[ModelReference];
GO

ALTER TABLE [dbo].[ChatModelReference]  WITH CHECK 
    ADD CONSTRAINT [FK_ChatModelReference_ReasoningResponseKind]
    FOREIGN KEY([ReasoningResponseKindId]) REFERENCES [dbo].[ReasoningResponseKind]([Id]);
ALTER TABLE [dbo].[ChatModelReference] CHECK CONSTRAINT [FK_ChatModelReference_ReasoningResponseKind];

ALTER TABLE [dbo].[ChatModelReference]  WITH CHECK 
    ADD CONSTRAINT [FK_ChatModelReference_Tokenizer]
    FOREIGN KEY([TokenizerId]) REFERENCES [dbo].[Tokenizer]([Id]);
ALTER TABLE [dbo].[ChatModelReference] CHECK CONSTRAINT [FK_ChatModelReference_Tokenizer];

ALTER TABLE [dbo].[ChatModelReference]
    ADD CONSTRAINT [DF_ChatModelReference_AllowVision] 
    DEFAULT ((0)) FOR [AllowVision];

ALTER TABLE [dbo].[ChatModelReference]
    ADD CONSTRAINT [DF_ChatModelReference_AllowSystemPrompt]
    DEFAULT ((1)) FOR [AllowSystemPrompt];

ALTER TABLE [dbo].[ChatModelReference]
    ADD CONSTRAINT [DF_ChatModelReference_AllowStreaming]
    DEFAULT ((1)) FOR [AllowStreaming];

ALTER TABLE [dbo].[ModelReference]
    DROP COLUMN 
         [MinTemperature]
       , [MaxTemperature]
       , [AllowSearch]
       , [AllowVision]
       , [AllowSystemPrompt]
       , [AllowStreaming]
       , [ReasoningResponseKindId]
       , [ContextWindow]
       , [MaxResponseTokens]
       , [TokenizerId]
       , [InputTokenPrice1M]
       , [OutputTokenPrice1M];
GO