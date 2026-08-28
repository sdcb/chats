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
    IF OBJECT_ID(N'dbo.ChatTurn', N'U') IS NULL
        THROW 52002, N'dbo.ChatTurn is required by the first-step migration.', 1;

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
    ELSE
    BEGIN
        PRINT N'    -> ChatDockerSession 表不存在，跳过删除';
    END;

    /* Step 1.2: Docker daemon, Windows Docker, Kubernetes, or another backend. */
    PRINT N'[Step 1.2] 创建 dbo.ContainerRuntimeNode（若不存在）';
    IF OBJECT_ID(N'dbo.ContainerRuntimeNode', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ContainerRuntimeNode
        (
            Id                  INT NOT NULL IDENTITY(1,1),
            Name                NVARCHAR(128) NOT NULL,
            AIName              VARCHAR(128) NOT NULL,
            Description         NVARCHAR(1000) NULL,
            -- 1=Docker, 2=Windows Docker, 3=Kubernetes, 4=Other
            BackendType         TINYINT NOT NULL,
            Endpoint            VARCHAR(2048) NOT NULL,
            Credential          VARCHAR(4000) NULL,
            IsEnabled            BIT NOT NULL CONSTRAINT DF_ContainerRuntimeNode_IsEnabled DEFAULT (1),
            CreatedAt            DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerRuntimeNode_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt            DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerRuntimeNode_UpdatedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_ContainerRuntimeNode PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT UQ_ContainerRuntimeNode_Name UNIQUE (Name),
            CONSTRAINT UQ_ContainerRuntimeNode_AIName UNIQUE (AIName),
            CONSTRAINT CK_ContainerRuntimeNode_BackendType CHECK (BackendType IN (1, 2, 3, 4))
        );
    END;
    ELSE
    BEGIN
        PRINT N'    -> ContainerRuntimeNode 表已存在，跳过创建';
    END;

    PRINT N'[Step 1.2.1] 插入默认 Docker RuntimeNode（若不存在）';
    IF NOT EXISTS (SELECT 1 FROM dbo.ContainerRuntimeNode WHERE Name = N'default-docker')
    BEGIN
        INSERT INTO dbo.ContainerRuntimeNode
            (Name, AIName, Description, BackendType, Endpoint, Credential, IsEnabled)
        VALUES
            (N'default-docker', 'linux', N'Default Linux Docker runtime', 1, 'unix:///var/run/docker.sock', NULL, 1);
        PRINT N'    -> 已插入 default-docker RuntimeNode';
    END;
    ELSE
    BEGIN
        PRINT N'    -> default-docker RuntimeNode 已存在，跳过插入';
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
            OwnerTurnId         BIGINT NULL,
            RuntimeNodeId       INT NOT NULL,
            -- 0=temporary, 1=permanent
            IsPermanent         BIT NOT NULL CONSTRAINT DF_ContainerResource_IsPermanent DEFAULT (0),
            BackendResourceId   VARCHAR(256) NOT NULL,
            Ip                  VARCHAR(45) NULL,
            Name                NVARCHAR(128) NOT NULL,
            Image               VARCHAR(512) NOT NULL,
            ShellPrefix         VARCHAR(128) NULL,
            CpuCores            REAL NULL,
            MemoryBytes         BIGINT NULL,
            MaxProcesses        INT NULL,
            -- Backend-specific network name; NULL uses the backend default network.
            BackendNetworkName  VARCHAR(128) NULL,
            CreatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerResource_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerResource_UpdatedAt DEFAULT (SYSUTCDATETIME()),
            LastActiveAt        DATETIME2(7) NULL,
            StoppedAt           DATETIME2(7) NULL,
            DeletedAt           DATETIME2(7) NULL,
            CleanupAt           DATETIME2(7) NULL,
            CONSTRAINT PK_ContainerResource PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT FK_ContainerResource_User FOREIGN KEY (OwnerUserId) REFERENCES dbo.[User](Id),
            CONSTRAINT FK_ContainerResource_Chat FOREIGN KEY (OwnerChatId) REFERENCES dbo.Chat(Id),
            CONSTRAINT FK_ContainerResource_Turn FOREIGN KEY (OwnerTurnId) REFERENCES dbo.ChatTurn(Id) ON DELETE SET NULL,
            CONSTRAINT FK_ContainerResource_RuntimeNode FOREIGN KEY (RuntimeNodeId) REFERENCES dbo.ContainerRuntimeNode(Id),
            CONSTRAINT CK_ContainerResource_Limits CHECK
                ((CpuCores IS NULL OR CpuCores >= 0) AND
                 (MemoryBytes IS NULL OR MemoryBytes >= 0) AND
                 (MaxProcesses IS NULL OR MaxProcesses >= 0)),
            CONSTRAINT CK_ContainerResource_Cleanup CHECK
                (IsPermanent = 1 OR CleanupAt IS NOT NULL OR DeletedAt IS NOT NULL)
        );
    END;
    ELSE
    BEGIN
        PRINT N'    -> ContainerResource 表已存在，跳过创建';
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerResource') AND name = N'IX_ContainerResource_OwnerUser_Deleted_Stopped')
        CREATE INDEX IX_ContainerResource_OwnerUser_Deleted_Stopped ON dbo.ContainerResource (OwnerUserId, DeletedAt, StoppedAt, IsPermanent);
    ELSE
        PRINT N'    -> 索引 IX_ContainerResource_OwnerUser_Deleted_Stopped 已存在，跳过';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerResource') AND name = N'IX_ContainerResource_OwnerTurn_Name')
        CREATE INDEX IX_ContainerResource_OwnerTurn_Name ON dbo.ContainerResource (OwnerTurnId, Name) WHERE OwnerTurnId IS NOT NULL;
    ELSE
        PRINT N'    -> 索引 IX_ContainerResource_OwnerTurn_Name 已存在，跳过';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerResource') AND name = N'IX_ContainerResource_OwnerChat_Turn_Deleted_Stopped')
        CREATE INDEX IX_ContainerResource_OwnerChat_Turn_Deleted_Stopped ON dbo.ContainerResource (OwnerChatId, OwnerTurnId, DeletedAt, StoppedAt) WHERE OwnerChatId IS NOT NULL;
    ELSE
        PRINT N'    -> 索引 IX_ContainerResource_OwnerChat_Turn_Deleted_Stopped 已存在，跳过';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerResource') AND name = N'UX_ContainerResource_RuntimeNode_BackendResource')
        CREATE UNIQUE INDEX UX_ContainerResource_RuntimeNode_BackendResource ON dbo.ContainerResource (RuntimeNodeId, BackendResourceId);
    ELSE
        PRINT N'    -> 索引 UX_ContainerResource_RuntimeNode_BackendResource 已存在，跳过';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerResource') AND name = N'IX_ContainerResource_RuntimeNode_Deleted_Stopped')
        CREATE INDEX IX_ContainerResource_RuntimeNode_Deleted_Stopped ON dbo.ContainerResource (RuntimeNodeId, DeletedAt, StoppedAt);
    ELSE
        PRINT N'    -> 索引 IX_ContainerResource_RuntimeNode_Deleted_Stopped 已存在，跳过';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerResource') AND name = N'IX_ContainerResource_CleanupAt')
        CREATE INDEX IX_ContainerResource_CleanupAt ON dbo.ContainerResource (CleanupAt) WHERE CleanupAt IS NOT NULL AND DeletedAt IS NULL;
    ELSE
        PRINT N'    -> 索引 IX_ContainerResource_CleanupAt 已存在，跳过';

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
            BackendVolumeId      VARCHAR(256) NULL,
            Name                NVARCHAR(128) NOT NULL,
            DeclaredBytes       BIGINT NULL,
            IsActive             BIT NOT NULL CONSTRAINT DF_ContainerVolume_IsActive DEFAULT (1),
            CreatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerVolume_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerVolume_UpdatedAt DEFAULT (SYSUTCDATETIME()),
            DeletedAt           DATETIME2(7) NULL,
            CONSTRAINT PK_ContainerVolume PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT FK_ContainerVolume_User FOREIGN KEY (OwnerUserId) REFERENCES dbo.[User](Id),
            CONSTRAINT FK_ContainerVolume_RuntimeNode FOREIGN KEY (RuntimeNodeId) REFERENCES dbo.ContainerRuntimeNode(Id),
            CONSTRAINT FK_ContainerVolume_Container FOREIGN KEY (ContainerResourceId) REFERENCES dbo.ContainerResource(Id),
            CONSTRAINT CK_ContainerVolume_Size CHECK
                (DeclaredBytes IS NULL OR DeclaredBytes >= 0),
            CONSTRAINT CK_ContainerVolume_Ownership CHECK
                ((IsStandalone = 0 AND ContainerResourceId IS NOT NULL) OR
                 (IsStandalone = 1 AND ContainerResourceId IS NULL)),
            CONSTRAINT CK_ContainerVolume_ActiveState CHECK
                ((IsActive = 1 AND DeletedAt IS NULL) OR (IsActive = 0 AND DeletedAt IS NOT NULL))
        );
    END;
    ELSE
    BEGIN
        PRINT N'    -> ContainerVolume 表已存在，跳过创建';
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerVolume') AND name = N'UX_ContainerVolume_InternalContainer')
        CREATE UNIQUE INDEX UX_ContainerVolume_InternalContainer ON dbo.ContainerVolume (ContainerResourceId) WHERE IsStandalone = 0 AND ContainerResourceId IS NOT NULL;
    ELSE
        PRINT N'    -> 索引 UX_ContainerVolume_InternalContainer 已存在，跳过';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerVolume') AND name = N'IX_ContainerVolume_OwnerUser_Active')
        CREATE INDEX IX_ContainerVolume_OwnerUser_Active ON dbo.ContainerVolume (OwnerUserId, IsActive, IsStandalone);
    ELSE
        PRINT N'    -> 索引 IX_ContainerVolume_OwnerUser_Active 已存在，跳过';

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
    ELSE
    BEGIN
        PRINT N'    -> ContainerVolumeMount 表已存在，跳过创建';
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerVolumeMount') AND name = N'UX_ContainerVolumeMount_ActivePath')
        CREATE UNIQUE INDEX UX_ContainerVolumeMount_ActivePath ON dbo.ContainerVolumeMount (VolumeId, ContainerResourceId, ContainerPath) WHERE IsActive = 1;
    ELSE
        PRINT N'    -> 索引 UX_ContainerVolumeMount_ActivePath 已存在，跳过';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerVolumeMount') AND name = N'IX_ContainerVolumeMount_Container_Active')
        CREATE INDEX IX_ContainerVolumeMount_Container_Active ON dbo.ContainerVolumeMount (ContainerResourceId, IsActive);
    ELSE
        PRINT N'    -> 索引 IX_ContainerVolumeMount_Container_Active 已存在，跳过';

    PRINT N'[Step 1.6] 创建 dbo.ChatContainerResourceAccess 及索引（若不存在）';
    IF OBJECT_ID(N'dbo.ChatContainerResourceAccess', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ChatContainerResourceAccess
        (
            Id                  BIGINT NOT NULL IDENTITY(1,1),
            ChatId              INT NOT NULL,
            ContainerResourceId BIGINT NOT NULL,
            GrantedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ChatContainerAccess_GrantedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_ChatContainerResourceAccess PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT FK_ChatContainerAccess_Chat FOREIGN KEY (ChatId) REFERENCES dbo.Chat(Id),
            CONSTRAINT FK_ChatContainerAccess_Container FOREIGN KEY (ContainerResourceId) REFERENCES dbo.ContainerResource(Id),
            CONSTRAINT UQ_ChatContainerAccess_ChatContainer UNIQUE (ChatId, ContainerResourceId)
        );
    END;
    ELSE
    BEGIN
        PRINT N'    -> ChatContainerResourceAccess 表已存在，跳过创建';
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ChatContainerResourceAccess') AND name = N'IX_ChatContainerAccess_Container')
        CREATE INDEX IX_ChatContainerAccess_Container ON dbo.ChatContainerResourceAccess (ContainerResourceId);
    ELSE
        PRINT N'    -> 索引 IX_ChatContainerAccess_Container 已存在，跳过';

    PRINT N'[Step 1.7] 创建 dbo.UserContainerQuota（若不存在）';
    IF OBJECT_ID(N'dbo.UserContainerQuota', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.UserContainerQuota
        (
            Id                  INT NOT NULL IDENTITY(1,1),
            -- NULL identifies the single global fallback quota; a value identifies a user quota.
            UserId              INT NULL,
            AllowCustomImage    BIT NOT NULL CONSTRAINT DF_UserContainerQuota_AllowCustomImage DEFAULT (0),
            -- Comma-separated backend network modes/names; '*' means unrestricted.
            AllowedNetworkModes VARCHAR(1024) NOT NULL CONSTRAINT DF_UserContainerQuota_AllowedNetworkModes DEFAULT ('none,bridge'),
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
    ELSE
    BEGIN
        PRINT N'    -> UserContainerQuota 表已存在，跳过创建';
    END;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.UserContainerQuota') AND name = N'UX_UserContainerQuota_User')
        CREATE UNIQUE INDEX UX_UserContainerQuota_User ON dbo.UserContainerQuota (UserId) WHERE UserId IS NOT NULL;
    ELSE
        PRINT N'    -> 索引 UX_UserContainerQuota_User 已存在，跳过';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.UserContainerQuota') AND name = N'UX_UserContainerQuota_Default')
        CREATE UNIQUE INDEX UX_UserContainerQuota_Default ON dbo.UserContainerQuota (UserId) WHERE UserId IS NULL;
    ELSE
        PRINT N'    -> 索引 UX_UserContainerQuota_Default 已存在，跳过';

    PRINT N'[Step 1.7.1] 插入全局默认用户容器配额（若不存在）';
    IF NOT EXISTS (SELECT 1 FROM dbo.UserContainerQuota WHERE UserId IS NULL)
    BEGIN
        INSERT INTO dbo.UserContainerQuota
            (UserId, AllowCustomImage, AllowedNetworkModes)
        VALUES
            (NULL, 0, 'none,bridge');
        PRINT N'    -> 已插入全局默认用户容器配额';
    END;
    ELSE
    BEGIN
        PRINT N'    -> 全局默认用户容器配额已存在，跳过插入';
    END;

    PRINT N'[Step 1.8] 创建 dbo.ContainerImage（若不存在）';
    IF OBJECT_ID(N'dbo.ContainerImage', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ContainerImage
        (
            Image       VARCHAR(512) NOT NULL,
            Description NVARCHAR(1000) NULL,
            IsEnabled   BIT NOT NULL CONSTRAINT DF_ContainerImage_IsEnabled DEFAULT (1),
            CONSTRAINT PK_ContainerImage PRIMARY KEY CLUSTERED (Image),
            CONSTRAINT CK_ContainerImage_Image CHECK (LEN(Image) > 0)
        );
    END;
    ELSE
    BEGIN
        PRINT N'    -> ContainerImage 表已存在，跳过创建';
    END;

    PRINT N'[Step 1.8.1] 插入内置镜像 code-interpreter:latest（若不存在）';
    IF NOT EXISTS (SELECT 1 FROM dbo.ContainerImage WHERE Image = 'code-interpreter:latest')
    BEGIN
        INSERT INTO dbo.ContainerImage (Image, Description, IsEnabled)
        VALUES
            ('code-interpreter:latest', N'Pre-installed with common packages, suitable for most daily tasks', 1);
        PRINT N'    -> 已插入内置镜像 code-interpreter:latest';
    END;
    ELSE
    BEGIN
        PRINT N'    -> 内置镜像 code-interpreter:latest 已存在，跳过插入';
    END;

    PRINT N'[Step 1.8.2] 创建 dbo.ContainerResourceTemplate 及索引（若不存在）';
    IF OBJECT_ID(N'dbo.ContainerResourceTemplate', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ContainerResourceTemplate
        (
            Id                  INT NOT NULL IDENTITY(1,1),
            Name                NVARCHAR(128) NOT NULL,
            RuntimeNodeId       INT NOT NULL,
            Image               VARCHAR(512) NOT NULL,
            CpuCores            REAL NOT NULL,
            MemoryBytes         BIGINT NOT NULL,
            MaxProcesses        INT NOT NULL,
            -- Backend-specific network name; NULL uses the backend default network.
            BackendNetworkName  VARCHAR(128) NULL,
            DefaultVolumeBytes  BIGINT NULL,
            -- 0=disabled, 1=user-visible, 2=AI-visible, 3=both.
            Visibility          TINYINT NOT NULL CONSTRAINT DF_ContainerTemplate_Visibility DEFAULT (3),
            CreatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerTemplate_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerTemplate_UpdatedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_ContainerResourceTemplate PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT UQ_ContainerResourceTemplate_Name UNIQUE (Name),
            CONSTRAINT FK_ContainerResourceTemplate_RuntimeNode FOREIGN KEY (RuntimeNodeId) REFERENCES dbo.ContainerRuntimeNode(Id),
            CONSTRAINT CK_ContainerTemplate_Values CHECK
                (CpuCores >= 0 AND MemoryBytes >= 0 AND MaxProcesses >= 0 AND (DefaultVolumeBytes IS NULL OR DefaultVolumeBytes >= 0)),
            CONSTRAINT CK_ContainerTemplate_Visibility CHECK (Visibility IN (0, 1, 2, 3))
        );
    END;
    ELSE
    BEGIN
        PRINT N'    -> ContainerResourceTemplate 表已存在，跳过创建';
    END;
    PRINT N'[Step 1.8.3] 插入默认 ContainerResourceTemplate（若不存在）';
    IF NOT EXISTS (SELECT 1 FROM dbo.ContainerResourceTemplate WHERE Name = N'default-code-interpreter')
    BEGIN
        DECLARE @defaultRuntimeNodeId int = (SELECT Id FROM dbo.ContainerRuntimeNode WHERE Name = N'default-docker');
        IF @defaultRuntimeNodeId IS NULL
            THROW 52011, N'default-docker RuntimeNode is required by the default container template.', 1;

        INSERT INTO dbo.ContainerResourceTemplate
            (Name, RuntimeNodeId, Image, CpuCores, MemoryBytes, MaxProcesses, BackendNetworkName, DefaultVolumeBytes, Visibility)
        VALUES
            (N'default-code-interpreter', @defaultRuntimeNodeId, 'code-interpreter:latest', 2.0, 2147483648, 200, 'bridge', NULL, 3);
        PRINT N'    -> 已插入默认 ContainerResourceTemplate';
    END;
    ELSE
    BEGIN
        PRINT N'    -> 默认 ContainerResourceTemplate 已存在，跳过插入';
    END;

/* Step 1.9: idempotent post-migration verification. */
PRINT N'[Step 1.9] 执行第一步结构校验';
IF OBJECT_ID(N'dbo.ChatDockerSession', N'U') IS NOT NULL
    THROW 52010, N'ChatDockerSession still exists after the first-step migration.', 1;
PRINT N'    -> 结构校验通过，旧 ChatDockerSession 不存在';

PRINT N'[第一步] 持久化 Docker 与资源治理基础结构创建完成';
GO
