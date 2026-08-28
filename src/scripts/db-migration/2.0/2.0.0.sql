/*
    Chats 2.0 - persistent containers and resource governance (SQL Server)

    This script is intentionally idempotent.  It only changes the database;
    administrators must remove 1.x containers from the runtime before running it.
    Connection credentials are stored as plain text for now and are expected to
    move to a secret/configuration provider in a later migration.
*/

SET NOCOUNT ON;

PRINT N'[第一步] 开始创建持久化 Docker 与资源治理基础结构';

    IF OBJECT_ID(N'dbo.[User]', N'U') IS NULL
        THROW 52000, N'dbo.[User] is required by the first-step migration.', 1;
    IF OBJECT_ID(N'dbo.Chat', N'U') IS NULL
        THROW 52001, N'dbo.Chat is required by the first-step migration.', 1;

    /* Step 1.1: remove the 1.x temporary-session model during the outage. */
    PRINT N'[Step 1.1] 删除 dbo.ChatDockerSession（若存在）';
    IF OBJECT_ID(N'dbo.ChatDockerSession', N'U') IS NOT NULL
    BEGIN
        DECLARE @dropSql nvarchar(max) = N'';
        SELECT @dropSql = @dropSql +
            N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(fk.parent_object_id)) + N'.' +
            QUOTENAME(OBJECT_NAME(fk.parent_object_id)) + N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(13)
        FROM sys.foreign_keys AS fk
        WHERE fk.parent_object_id = OBJECT_ID(N'dbo.ChatDockerSession')
           OR fk.referenced_object_id = OBJECT_ID(N'dbo.ChatDockerSession');
        IF @dropSql <> N'' EXEC sys.sp_executesql @dropSql;
        DROP TABLE dbo.ChatDockerSession;
    END;

    /* Step 1.2: Docker daemon, Windows Docker, Kubernetes, or another backend. */
    PRINT N'[Step 1.2] 创建 dbo.ContainerRuntimeNode（若不存在）';
    IF OBJECT_ID(N'dbo.ContainerRuntimeNode', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ContainerRuntimeNode
        (
            Id                  INT NOT NULL IDENTITY(1,1),
            Name                NVARCHAR(128) NOT NULL,
            -- 1=Docker, 2=Windows Docker, 3=Kubernetes, 4=Other
            BackendType         TINYINT NOT NULL,
            Endpoint            NVARCHAR(2048) NOT NULL,
            Credential           NVARCHAR(4000) NULL,
            IsEnabled            BIT NOT NULL CONSTRAINT DF_ContainerRuntimeNode_IsEnabled DEFAULT (1),
            SupportsDynamicResources BIT NOT NULL CONSTRAINT DF_ContainerRuntimeNode_Dynamic DEFAULT (0),
            SupportsNetworkPolicy BIT NOT NULL CONSTRAINT DF_ContainerRuntimeNode_Network DEFAULT (0),
            SupportsManagedVolumes BIT NOT NULL CONSTRAINT DF_ContainerRuntimeNode_Volumes DEFAULT (0),
            PhysicalCpuCores     REAL NULL,
            PhysicalMemoryBytes  BIGINT NULL,
            MaxContainerCount    INT NULL,
            CreatedAt            DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerRuntimeNode_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt            DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerRuntimeNode_UpdatedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_ContainerRuntimeNode PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT UQ_ContainerRuntimeNode_Name UNIQUE (Name),
            CONSTRAINT CK_ContainerRuntimeNode_BackendType CHECK (BackendType IN (1, 2, 3, 4)),
            CONSTRAINT CK_ContainerRuntimeNode_Capacity CHECK
                ((PhysicalCpuCores IS NULL OR PhysicalCpuCores >= 0) AND
                 (PhysicalMemoryBytes IS NULL OR PhysicalMemoryBytes >= 0) AND
                 (MaxContainerCount IS NULL OR MaxContainerCount >= 0))
        );
    END;

    /* Step 1.3: common resource record for permanent and temporary containers. */
    PRINT N'[Step 1.3] 创建 dbo.ContainerResource 及索引（若不存在）';
    IF OBJECT_ID(N'dbo.ContainerResource', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ContainerResource
        (
            Id                  BIGINT NOT NULL IDENTITY(1,1),
            OwnerUserId         INT NOT NULL,
            OwnerChatId         INT NULL,
            RuntimeNodeId       INT NOT NULL,
            -- 0=temporary, 1=permanent
            IsPermanent         BIT NOT NULL CONSTRAINT DF_ContainerResource_IsPermanent DEFAULT (0),
            BackendResourceId   NVARCHAR(256) NULL,
            Name                NVARCHAR(128) NOT NULL,
            Image               NVARCHAR(512) NOT NULL,
            ShellPrefix         NVARCHAR(128) NULL,
            CpuCores            REAL NULL,
            MemoryBytes         BIGINT NULL,
            MaxProcesses        INT NULL,
            -- NetworkPolicy: 0=None, 1=Egress, 2=Public
            NetworkPolicy       TINYINT NOT NULL,
            -- Status: 1=Running, 2=Stopped, 3=Pending, 4=Deleted
            Status              TINYINT NOT NULL,
            CreatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerResource_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerResource_UpdatedAt DEFAULT (SYSUTCDATETIME()),
            LastActiveAt        DATETIME2(7) NULL,
            StoppedAt           DATETIME2(7) NULL,
            DeletedAt           DATETIME2(7) NULL,
            CleanupAt           DATETIME2(7) NULL,
            LastError           NVARCHAR(4000) NULL,
            LastErrorAt         DATETIME2(7) NULL,
            CONSTRAINT PK_ContainerResource PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT FK_ContainerResource_User FOREIGN KEY (OwnerUserId) REFERENCES dbo.[User](Id),
            CONSTRAINT FK_ContainerResource_Chat FOREIGN KEY (OwnerChatId) REFERENCES dbo.Chat(Id),
            CONSTRAINT FK_ContainerResource_RuntimeNode FOREIGN KEY (RuntimeNodeId) REFERENCES dbo.ContainerRuntimeNode(Id),
            CONSTRAINT CK_ContainerResource_NetworkPolicy CHECK (NetworkPolicy IN (0, 1, 2)),
            CONSTRAINT CK_ContainerResource_Status CHECK (Status IN (1, 2, 3, 4)),
            CONSTRAINT CK_ContainerResource_Limits CHECK
                ((CpuCores IS NULL OR CpuCores >= 0) AND
                 (MemoryBytes IS NULL OR MemoryBytes >= 0) AND
                 (MaxProcesses IS NULL OR MaxProcesses >= 0)),
            CONSTRAINT CK_ContainerResource_Cleanup CHECK
                (IsPermanent = 1 OR CleanupAt IS NOT NULL OR Status = 4),
            CONSTRAINT CK_ContainerResource_DeletedState CHECK
                ((Status = 4 AND DeletedAt IS NOT NULL) OR (Status <> 4 AND DeletedAt IS NULL))
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerResource') AND name = N'IX_ContainerResource_OwnerUser_Status')
        CREATE INDEX IX_ContainerResource_OwnerUser_Status ON dbo.ContainerResource (OwnerUserId, Status, IsPermanent);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerResource') AND name = N'IX_ContainerResource_RuntimeNode_Status')
        CREATE INDEX IX_ContainerResource_RuntimeNode_Status ON dbo.ContainerResource (RuntimeNodeId, Status);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerResource') AND name = N'IX_ContainerResource_CleanupAt')
        CREATE INDEX IX_ContainerResource_CleanupAt ON dbo.ContainerResource (CleanupAt) WHERE CleanupAt IS NOT NULL AND Status <> 4;

    /* Step 1.4: first-class volumes. */
    PRINT N'[Step 1.4] 创建 dbo.ContainerVolume 及索引（若不存在）';
    IF OBJECT_ID(N'dbo.ContainerVolume', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ContainerVolume
        (
            Id                  BIGINT NOT NULL IDENTITY(1,1),
            OwnerUserId         INT NOT NULL,
            RuntimeNodeId       INT NOT NULL,
            ContainerResourceId  BIGINT NULL,
            -- 0=internal (owned by a container), 1=standalone
            IsStandalone         BIT NOT NULL CONSTRAINT DF_ContainerVolume_IsStandalone DEFAULT (0),
            BackendVolumeId      NVARCHAR(256) NULL,
            Name                NVARCHAR(128) NOT NULL,
            DeclaredBytes       BIGINT NULL,
            UsedBytes           BIGINT NULL,
            IsActive             BIT NOT NULL CONSTRAINT DF_ContainerVolume_IsActive DEFAULT (1),
            CreatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerVolume_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerVolume_UpdatedAt DEFAULT (SYSUTCDATETIME()),
            DeletedAt           DATETIME2(7) NULL,
            CONSTRAINT PK_ContainerVolume PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT FK_ContainerVolume_User FOREIGN KEY (OwnerUserId) REFERENCES dbo.[User](Id),
            CONSTRAINT FK_ContainerVolume_RuntimeNode FOREIGN KEY (RuntimeNodeId) REFERENCES dbo.ContainerRuntimeNode(Id),
            CONSTRAINT FK_ContainerVolume_Container FOREIGN KEY (ContainerResourceId) REFERENCES dbo.ContainerResource(Id),
            CONSTRAINT CK_ContainerVolume_Size CHECK
                ((DeclaredBytes IS NULL OR DeclaredBytes >= 0) AND (UsedBytes IS NULL OR UsedBytes >= 0)),
            CONSTRAINT CK_ContainerVolume_Ownership CHECK
                ((IsStandalone = 0 AND ContainerResourceId IS NOT NULL) OR
                 (IsStandalone = 1 AND ContainerResourceId IS NULL)),
            CONSTRAINT CK_ContainerVolume_ActiveState CHECK
                ((IsActive = 1 AND DeletedAt IS NULL) OR (IsActive = 0 AND DeletedAt IS NOT NULL))
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerVolume') AND name = N'UX_ContainerVolume_InternalContainer')
        CREATE UNIQUE INDEX UX_ContainerVolume_InternalContainer ON dbo.ContainerVolume (ContainerResourceId) WHERE IsStandalone = 0 AND ContainerResourceId IS NOT NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerVolume') AND name = N'IX_ContainerVolume_OwnerUser_Active')
        CREATE INDEX IX_ContainerVolume_OwnerUser_Active ON dbo.ContainerVolume (OwnerUserId, IsActive, IsStandalone);

    PRINT N'[Step 1.5] 创建 dbo.ContainerVolumeMount 及索引（若不存在）';
    IF OBJECT_ID(N'dbo.ContainerVolumeMount', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ContainerVolumeMount
        (
            Id                  BIGINT NOT NULL IDENTITY(1,1),
            VolumeId            BIGINT NOT NULL,
            ContainerResourceId BIGINT NOT NULL,
            -- Unicode mount path, limited to 512 characters so the active-path
            -- unique index remains below SQL Server's 1700-byte key limit.
            ContainerPath       NVARCHAR(512) NOT NULL,
            IsReadOnly          BIT NOT NULL CONSTRAINT DF_ContainerVolumeMount_ReadOnly DEFAULT (0),
            IsActive            BIT NOT NULL CONSTRAINT DF_ContainerVolumeMount_Active DEFAULT (1),
            MountedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerVolumeMount_MountedAt DEFAULT (SYSUTCDATETIME()),
            UnmountedAt         DATETIME2(7) NULL,
            CONSTRAINT PK_ContainerVolumeMount PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT FK_ContainerVolumeMount_Volume FOREIGN KEY (VolumeId) REFERENCES dbo.ContainerVolume(Id),
            CONSTRAINT FK_ContainerVolumeMount_Container FOREIGN KEY (ContainerResourceId) REFERENCES dbo.ContainerResource(Id),
            CONSTRAINT CK_ContainerVolumeMount_State CHECK ((IsActive = 1 AND UnmountedAt IS NULL) OR (IsActive = 0 AND UnmountedAt IS NOT NULL))
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerVolumeMount') AND name = N'UX_ContainerVolumeMount_ActivePath')
        CREATE UNIQUE INDEX UX_ContainerVolumeMount_ActivePath ON dbo.ContainerVolumeMount (VolumeId, ContainerResourceId, ContainerPath) WHERE IsActive = 1;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerVolumeMount') AND name = N'IX_ContainerVolumeMount_Container_Active')
        CREATE INDEX IX_ContainerVolumeMount_Container_Active ON dbo.ContainerVolumeMount (ContainerResourceId, IsActive);

    PRINT N'[Step 1.6] 创建 dbo.ChatContainerResourceAccess 及索引（若不存在）';
    IF OBJECT_ID(N'dbo.ChatContainerResourceAccess', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ChatContainerResourceAccess
        (
            Id                  BIGINT NOT NULL IDENTITY(1,1),
            ChatId              INT NOT NULL,
            ContainerResourceId BIGINT NOT NULL,
            -- NULL means chat-wide access; otherwise access starts at this turn's branch.
            GrantedFromTurnId   BIGINT NULL,
            GrantedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ChatContainerAccess_GrantedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_ChatContainerResourceAccess PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT FK_ChatContainerAccess_Chat FOREIGN KEY (ChatId) REFERENCES dbo.Chat(Id),
            CONSTRAINT FK_ChatContainerAccess_Container FOREIGN KEY (ContainerResourceId) REFERENCES dbo.ContainerResource(Id),
            CONSTRAINT FK_ChatContainerAccess_GrantedFromTurn FOREIGN KEY (GrantedFromTurnId) REFERENCES dbo.ChatTurn(Id),
            CONSTRAINT UQ_ChatContainerAccess_ChatContainer UNIQUE (ChatId, ContainerResourceId)
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ChatContainerResourceAccess') AND name = N'IX_ChatContainerAccess_GrantedFromTurn')
        CREATE INDEX IX_ChatContainerAccess_GrantedFromTurn ON dbo.ChatContainerResourceAccess (GrantedFromTurnId) WHERE GrantedFromTurnId IS NOT NULL;

    PRINT N'[Step 1.7] 创建 dbo.UserContainerQuota（若不存在）';
    IF OBJECT_ID(N'dbo.UserContainerQuota', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.UserContainerQuota
        (
            Id                  INT NOT NULL IDENTITY(1,1),
            -- NULL identifies the single global fallback quota; a value identifies a user quota.
            UserId              INT NULL,
            MaxContainerCount   INT NULL,
            MaxCpuCores         REAL NULL,
            MaxMemoryBytes      BIGINT NULL,
            MaxContainerProcesses INT NULL,
            MaxVolumeBytes      BIGINT NULL,
            MaxContainerCpuCores REAL NULL,
            MaxContainerMemoryBytes BIGINT NULL,
            MaxVolumeBytesPerVolume BIGINT NULL,
            UpdatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_UserContainerQuota_UpdatedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_UserContainerQuota PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT FK_UserContainerQuota_User FOREIGN KEY (UserId) REFERENCES dbo.[User](Id),
            CONSTRAINT CK_UserContainerQuota_Values CHECK
                ((MaxContainerCount IS NULL OR MaxContainerCount >= 0) AND
                 (MaxCpuCores IS NULL OR MaxCpuCores >= 0) AND
                 (MaxMemoryBytes IS NULL OR MaxMemoryBytes >= 0) AND
                 (MaxContainerProcesses IS NULL OR MaxContainerProcesses >= 0) AND
                 (MaxVolumeBytes IS NULL OR MaxVolumeBytes >= 0) AND
                 (MaxContainerCpuCores IS NULL OR MaxContainerCpuCores >= 0) AND
                 (MaxContainerMemoryBytes IS NULL OR MaxContainerMemoryBytes >= 0) AND
                 (MaxVolumeBytesPerVolume IS NULL OR MaxVolumeBytesPerVolume >= 0))
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.UserContainerQuota') AND name = N'UX_UserContainerQuota_User')
        CREATE UNIQUE INDEX UX_UserContainerQuota_User ON dbo.UserContainerQuota (UserId) WHERE UserId IS NOT NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.UserContainerQuota') AND name = N'UX_UserContainerQuota_Default')
        CREATE UNIQUE INDEX UX_UserContainerQuota_Default ON dbo.UserContainerQuota (UserId) WHERE UserId IS NULL;

    PRINT N'[Step 1.8] 创建 dbo.ContainerResourceTemplate 及索引（若不存在）';
    IF OBJECT_ID(N'dbo.ContainerResourceTemplate', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ContainerResourceTemplate
        (
            Id                  INT NOT NULL IDENTITY(1,1),
            Name                NVARCHAR(128) NOT NULL,
            Image               NVARCHAR(512) NOT NULL,
            CpuCores            REAL NOT NULL,
            MemoryBytes         BIGINT NOT NULL,
            MaxProcesses        INT NOT NULL,
            -- NetworkPolicy: 0=None, 1=Egress, 2=Public
            NetworkPolicy       TINYINT NOT NULL,
            DefaultVolumeBytes  BIGINT NULL,
            IsEnabled           BIT NOT NULL CONSTRAINT DF_ContainerTemplate_Enabled DEFAULT (1),
            IsDefault           BIT NOT NULL CONSTRAINT DF_ContainerTemplate_Default DEFAULT (0),
            CreatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerTemplate_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerTemplate_UpdatedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_ContainerResourceTemplate PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT UQ_ContainerResourceTemplate_Name UNIQUE (Name),
            CONSTRAINT CK_ContainerTemplate_NetworkPolicy CHECK (NetworkPolicy IN (0, 1, 2)),
            CONSTRAINT CK_ContainerTemplate_Values CHECK
                (CpuCores >= 0 AND MemoryBytes >= 0 AND MaxProcesses >= 0 AND (DefaultVolumeBytes IS NULL OR DefaultVolumeBytes >= 0))
        );
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerResourceTemplate') AND name = N'UX_ContainerResourceTemplate_Default')
        CREATE UNIQUE INDEX UX_ContainerResourceTemplate_Default ON dbo.ContainerResourceTemplate (IsDefault) WHERE IsDefault = 1;

/* Step 1.9: idempotent post-migration verification. */
PRINT N'[Step 1.9] 执行第一步结构校验';
IF OBJECT_ID(N'dbo.ChatDockerSession', N'U') IS NOT NULL
    THROW 52010, N'ChatDockerSession still exists after the first-step migration.', 1;

SELECT name AS TableName
FROM sys.tables
WHERE name IN
(
    'ContainerRuntimeNode', 'ContainerResource', 'ContainerVolume',
    'ContainerVolumeMount', 'ChatContainerResourceAccess',
    'UserContainerQuota', 'ContainerResourceTemplate'
)
ORDER BY name;

PRINT N'[第一步] 持久化 Docker 与资源治理基础结构创建完成';
GO
