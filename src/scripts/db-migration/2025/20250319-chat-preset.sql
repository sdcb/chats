CREATE TABLE dbo.ChatPreset
(
	Id int NOT NULL IDENTITY (1, 1),
	Name nvarchar(50) NOT NULL,
	UserId int NOT NULL,
	UpdatedAt datetime2(7) NOT NULL
)
ALTER TABLE dbo.ChatPreset ADD CONSTRAINT PK_ChatPreset PRIMARY KEY CLUSTERED (Id)
CREATE NONCLUSTERED INDEX IX_ChatPreset_UserId ON dbo.ChatPreset(UserId)
CREATE NONCLUSTERED INDEX IX_ChatPreset_Name ON dbo.ChatPreset(Name)

ALTER TABLE dbo.ChatPreset ADD CONSTRAINT FK_ChatPreset_User FOREIGN KEY(UserId) REFERENCES dbo.[User](Id) 
	ON UPDATE  CASCADE
	ON DELETE  CASCADE

CREATE TABLE dbo.ChatPresetSpan
(
    ChatPresetId INT NOT NULL,
    SpanId TINYINT NOT NULL,
    ChatConfigId INT NOT NULL,
    CONSTRAINT PK_ChatPresetSpan PRIMARY KEY CLUSTERED (ChatPresetId, SpanId),
    CONSTRAINT FK_ChatPresetSpan_Preset FOREIGN KEY (ChatPresetId) REFERENCES dbo.ChatPreset(Id)
        ON UPDATE CASCADE
        ON DELETE CASCADE,
    CONSTRAINT FK_ChatPresetSpan_Config FOREIGN KEY (ChatConfigId) REFERENCES dbo.ChatConfig(Id)
        ON UPDATE CASCADE
        ON DELETE NO ACTION 
);
CREATE NONCLUSTERED INDEX IX_ChatPresetSpan_Config ON dbo.ChatPresetSpan(ChatConfigId);
