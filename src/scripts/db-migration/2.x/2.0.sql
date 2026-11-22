/*
    Chats 2.0 - persistent containers and resource governance (SQL Server)

    This script is intentionally idempotent.  It only changes the database;
    administrators must remove 1.x containers from the runtime before running it.
    Connection credentials are stored as plain text for now and are expected to
    move to a secret/configuration provider in a later migration.
*/

IF DB_NAME() <> N'ChatsDEV'
BEGIN
    THROW 52012, N'This migration must be executed against the ChatsDEV database.', 1;
END;

SET NOCOUNT ON;

PRINT N'[第一步] 开始创建持久化 Docker 与资源治理基础结构';

    IF OBJECT_ID(N'dbo.[User]', N'U') IS NULL
        THROW 52000, N'dbo.[User] is required by the first-step migration.', 1;
    IF OBJECT_ID(N'dbo.Chat', N'U') IS NULL
        THROW 52001, N'dbo.Chat is required by the first-step migration.', 1;
    IF OBJECT_ID(N'dbo.ChatTurn', N'U') IS NULL
        THROW 52002, N'dbo.ChatTurn is required by the first-step migration.', 1;

    /* Step 1.1: remove the 1.x temporary-session model during the outage. */
    IF OBJECT_ID(N'dbo.ChatDockerSession', N'U') IS NOT NULL
    BEGIN
        PRINT N'[Step 1.1] 删除 dbo.ChatDockerSession';
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
    IF OBJECT_ID(N'dbo.ContainerRuntimeNode', N'U') IS NULL
    BEGIN
        PRINT N'[Step 1.2] 创建 dbo.ContainerRuntimeNode';
        CREATE TABLE dbo.ContainerRuntimeNode
        (
            Id                  INT NOT NULL IDENTITY(1,1),
            Name                NVARCHAR(128) NOT NULL,
            AiName              VARCHAR(128) NOT NULL,
            Description         NVARCHAR(1000) NULL,
            -- 1=Docker, 2=Windows Docker, 3=Kubernetes, 4=Other
            BackendType         TINYINT NOT NULL,
            Endpoint            VARCHAR(2048) NULL,
            Credential          VARCHAR(4000) NULL,
            IsEnabled            BIT NOT NULL CONSTRAINT DF_ContainerRuntimeNode_IsEnabled DEFAULT (1),
            CreatedAt            DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerRuntimeNode_CreatedAt DEFAULT (SYSUTCDATETIME()),
            UpdatedAt            DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerRuntimeNode_UpdatedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT PK_ContainerRuntimeNode PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT UQ_ContainerRuntimeNode_Name UNIQUE (Name),
            CONSTRAINT UQ_ContainerRuntimeNode_AiName UNIQUE (AiName),
            CONSTRAINT CK_ContainerRuntimeNode_BackendType CHECK (BackendType IN (1, 2, 3, 4))
        );
    END;
    ELSE
    BEGIN
        PRINT N'    -> ContainerRuntimeNode 表已存在，跳过创建';
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.ContainerRuntimeNode WHERE Name = N'default-docker')
    BEGIN
        PRINT N'[Step 1.2.1] 插入默认 Docker RuntimeNode';
        INSERT INTO dbo.ContainerRuntimeNode
            (Name, AiName, Description, BackendType, Endpoint, Credential, IsEnabled)
        VALUES
            (N'default-docker', 'linux', N'Default Linux Docker runtime', 1, NULL, NULL, 1);
        PRINT N'    -> 已插入 default-docker RuntimeNode';
    END;
    ELSE
    BEGIN
        PRINT N'    -> default-docker RuntimeNode 已存在，跳过插入';
    END;

    /* Step 1.3: common resource record for permanent and temporary containers. */
    IF OBJECT_ID(N'dbo.ContainerResource', N'U') IS NULL
    BEGIN
        PRINT N'[Step 1.3] 创建 dbo.ContainerResource 及索引';
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
    IF OBJECT_ID(N'dbo.ContainerVolume', N'U') IS NULL
    BEGIN
        PRINT N'[Step 1.4] 创建 dbo.ContainerVolume 及索引';
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

    IF OBJECT_ID(N'dbo.ContainerVolumeMount', N'U') IS NULL
    BEGIN
        PRINT N'[Step 1.5] 创建 dbo.ContainerVolumeMount 及索引';
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

    IF OBJECT_ID(N'dbo.ChatContainerResourceAccess', N'U') IS NULL
    BEGIN
        PRINT N'[Step 1.6] 创建 dbo.ChatContainerResourceAccess 及索引';
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

    IF OBJECT_ID(N'dbo.UserContainerQuota', N'U') IS NULL
    BEGIN
        PRINT N'[Step 1.7] 创建 dbo.UserContainerQuota';
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

    IF NOT EXISTS (SELECT 1 FROM dbo.UserContainerQuota WHERE UserId IS NULL)
    BEGIN
        PRINT N'[Step 1.7.1] 插入全局默认用户容器配额';
        INSERT INTO dbo.UserContainerQuota
            (UserId, AllowCustomImage, AllowedNetworkModes)
        VALUES
            (NULL, 1, 'none,bridge');
        PRINT N'    -> 已插入全局默认用户容器配额';
    END;
    ELSE
    BEGIN
        PRINT N'    -> 全局默认用户容器配额已存在，跳过插入';
    END;

    IF OBJECT_ID(N'dbo.ContainerImage', N'U') IS NULL
    BEGIN
        PRINT N'[Step 1.8] 创建 dbo.ContainerImage';
        CREATE TABLE dbo.ContainerImage
        (
            Id          INT NOT NULL IDENTITY(1,1),
            Image       VARCHAR(512) NOT NULL,
            Description NVARCHAR(1000) NULL,
            IsEnabled   BIT NOT NULL CONSTRAINT DF_ContainerImage_IsEnabled DEFAULT (1),
            CONSTRAINT PK_ContainerImage PRIMARY KEY CLUSTERED (Id),
            CONSTRAINT UQ_ContainerImage_Image UNIQUE (Image),
            CONSTRAINT CK_ContainerImage_Image CHECK (LEN(LTRIM(RTRIM(Image))) > 0)
        );
    END;
    ELSE
    BEGIN
        PRINT N'    -> ContainerImage 表已存在，跳过创建';
    END;

    IF NOT EXISTS (SELECT 1 FROM dbo.ContainerImage WHERE Image = 'code-interpreter:latest')
    BEGIN
        PRINT N'[Step 1.8.1] 插入内置镜像 code-interpreter:latest';
        INSERT INTO dbo.ContainerImage (Image, Description, IsEnabled)
        VALUES
            ('code-interpreter:latest', N'Pre-installed with common packages, suitable for most daily tasks', 1);
        PRINT N'    -> 已插入内置镜像 code-interpreter:latest';
    END;
    ELSE
    BEGIN
        PRINT N'    -> 内置镜像 code-interpreter:latest 已存在，跳过插入';
    END;

    IF OBJECT_ID(N'dbo.ContainerResourceTemplate', N'U') IS NULL
    BEGIN
        PRINT N'[Step 1.8.2] 创建 dbo.ContainerResourceTemplate 及索引';
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
    IF NOT EXISTS (SELECT 1 FROM dbo.ContainerResourceTemplate WHERE Name = N'default-code-interpreter')
    BEGIN
        PRINT N'[Step 1.8.3] 插入默认 ContainerResourceTemplate';
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

/* =====================================================================
   第二步：端口映射、Agent 运行态、网站托管与 Provider File ID
   ===================================================================== */
SET NOCOUNT ON;
PRINT N'[第二步] 开始创建 2.0 运行态与扩展结构';

/* Step 2.1: runtime-node addressing and host-port pool for port mappings. */
IF COL_LENGTH(N'dbo.ContainerRuntimeNode', N'PublicHost') IS NULL
BEGIN
    PRINT N'[Step 2.1] ContainerRuntimeNode 增加 PublicHost/PortRangeStart/PortRangeEnd';
    ALTER TABLE dbo.ContainerRuntimeNode ADD
        -- Host/IP that gateways use to reach published container ports on this node.
        PublicHost      VARCHAR(255) NULL,
        PortRangeStart  INT NULL,
        PortRangeEnd    INT NULL;
END;
ELSE
    PRINT N'    -> ContainerRuntimeNode 端口池列已存在，跳过';
GO
IF OBJECT_ID(N'CK_ContainerRuntimeNode_PortRange', N'C') IS NULL
    ALTER TABLE dbo.ContainerRuntimeNode ADD CONSTRAINT CK_ContainerRuntimeNode_PortRange CHECK
        ((PortRangeStart IS NULL AND PortRangeEnd IS NULL) OR
         (PortRangeStart BETWEEN 1 AND 65535 AND PortRangeEnd BETWEEN PortRangeStart AND 65535));
GO

IF COL_LENGTH(N'dbo.UserContainerQuota', N'MaxPublishedSites') IS NULL
BEGIN
    PRINT N'[Step 2.1.1] UserContainerQuota 增加 MaxPortMappings/MaxPublishedSites';
    ALTER TABLE dbo.UserContainerQuota ADD
        MaxPortMappings   INT NULL,
        MaxPublishedSites INT NULL;
END;
GO

/* Step 2.2: container port mappings (container port -> node host port). */
IF OBJECT_ID(N'dbo.ContainerPortMapping', N'U') IS NULL
BEGIN
    PRINT N'[Step 2.2] 创建 dbo.ContainerPortMapping';
    CREATE TABLE dbo.ContainerPortMapping
    (
        Id                  BIGINT NOT NULL IDENTITY(1,1),
        ContainerResourceId BIGINT NOT NULL,
        RuntimeNodeId       INT NOT NULL,
        ContainerPort       INT NOT NULL,
        HostPort            INT NOT NULL,
        -- tcp | udp
        Protocol            VARCHAR(8) NOT NULL CONSTRAINT DF_ContainerPortMapping_Protocol DEFAULT ('tcp'),
        IsActive            BIT NOT NULL CONSTRAINT DF_ContainerPortMapping_IsActive DEFAULT (1),
        CreatedAt           DATETIME2(7) NOT NULL CONSTRAINT DF_ContainerPortMapping_CreatedAt DEFAULT (SYSUTCDATETIME()),
        RemovedAt           DATETIME2(7) NULL,
        CONSTRAINT PK_ContainerPortMapping PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ContainerPortMapping_Container FOREIGN KEY (ContainerResourceId) REFERENCES dbo.ContainerResource(Id),
        CONSTRAINT FK_ContainerPortMapping_RuntimeNode FOREIGN KEY (RuntimeNodeId) REFERENCES dbo.ContainerRuntimeNode(Id),
        CONSTRAINT CK_ContainerPortMapping_Ports CHECK
            (ContainerPort BETWEEN 1 AND 65535 AND HostPort BETWEEN 1 AND 65535 AND Protocol IN ('tcp', 'udp')),
        CONSTRAINT CK_ContainerPortMapping_State CHECK
            ((IsActive = 1 AND RemovedAt IS NULL) OR (IsActive = 0 AND RemovedAt IS NOT NULL))
    );
END;
ELSE
    PRINT N'    -> ContainerPortMapping 表已存在，跳过创建';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerPortMapping') AND name = N'UX_ContainerPortMapping_NodeHostPort')
    CREATE UNIQUE INDEX UX_ContainerPortMapping_NodeHostPort ON dbo.ContainerPortMapping (RuntimeNodeId, HostPort, Protocol) WHERE IsActive = 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ContainerPortMapping') AND name = N'UX_ContainerPortMapping_ContainerPort')
    CREATE UNIQUE INDEX UX_ContainerPortMapping_ContainerPort ON dbo.ContainerPortMapping (ContainerResourceId, ContainerPort, Protocol) WHERE IsActive = 1;
GO

/* Step 2.3: persistent agent runs (SSE replay, stop, steering). */
IF OBJECT_ID(N'dbo.ChatRun', N'U') IS NULL
BEGIN
    PRINT N'[Step 2.3] 创建 dbo.ChatRun';
    CREATE TABLE dbo.ChatRun
    (
        Id              BIGINT NOT NULL IDENTITY(1,1),
        ChatId          INT NOT NULL,
        UserId          INT NOT NULL,
        -- 1=general, 2=regenerate-one, 3=regenerate-all, 4=continue (answer question)
        Kind            TINYINT NOT NULL,
        -- 1=running, 2=completed, 3=stopped, 4=failed
        Status          TINYINT NOT NULL CONSTRAINT DF_ChatRun_Status DEFAULT (1),
        -- JSON snapshot needed by clients to rebuild placeholders on resume.
        Metadata        NVARCHAR(MAX) NULL,
        LastEventSeq    INT NOT NULL CONSTRAINT DF_ChatRun_LastEventSeq DEFAULT (0),
        InstanceId      VARCHAR(64) NOT NULL,
        StopRequestedAt DATETIME2(7) NULL,
        CreatedAt       DATETIME2(7) NOT NULL CONSTRAINT DF_ChatRun_CreatedAt DEFAULT (SYSUTCDATETIME()),
        HeartbeatAt     DATETIME2(7) NOT NULL CONSTRAINT DF_ChatRun_HeartbeatAt DEFAULT (SYSUTCDATETIME()),
        CompletedAt     DATETIME2(7) NULL,
        CONSTRAINT PK_ChatRun PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ChatRun_Chat FOREIGN KEY (ChatId) REFERENCES dbo.Chat(Id) ON DELETE CASCADE,
        CONSTRAINT FK_ChatRun_User FOREIGN KEY (UserId) REFERENCES dbo.[User](Id),
        CONSTRAINT CK_ChatRun_Kind CHECK (Kind IN (1, 2, 3, 4)),
        CONSTRAINT CK_ChatRun_Status CHECK (Status IN (1, 2, 3, 4)),
        CONSTRAINT CK_ChatRun_Completed CHECK ((Status = 1 AND CompletedAt IS NULL) OR (Status <> 1 AND CompletedAt IS NOT NULL))
    );
END;
ELSE
    PRINT N'    -> ChatRun 表已存在，跳过创建';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ChatRun') AND name = N'IX_ChatRun_Chat_Status')
    CREATE INDEX IX_ChatRun_Chat_Status ON dbo.ChatRun (ChatId, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ChatRun') AND name = N'IX_ChatRun_Status_Heartbeat')
    CREATE INDEX IX_ChatRun_Status_Heartbeat ON dbo.ChatRun (Status, HeartbeatAt);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ChatRun') AND name = N'IX_ChatRun_CompletedAt')
    CREATE INDEX IX_ChatRun_CompletedAt ON dbo.ChatRun (CompletedAt) WHERE CompletedAt IS NOT NULL;
GO

IF OBJECT_ID(N'dbo.ChatRunEvent', N'U') IS NULL
BEGIN
    PRINT N'[Step 2.4] 创建 dbo.ChatRunEvent';
    CREATE TABLE dbo.ChatRunEvent
    (
        Id          BIGINT NOT NULL IDENTITY(1,1),
        RunId       BIGINT NOT NULL,
        -- Inclusive sequence range of the events batched into Payload (JSON array).
        FirstSeq    INT NOT NULL,
        LastSeq     INT NOT NULL,
        Payload     NVARCHAR(MAX) NOT NULL,
        CreatedAt   DATETIME2(7) NOT NULL CONSTRAINT DF_ChatRunEvent_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_ChatRunEvent PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ChatRunEvent_Run FOREIGN KEY (RunId) REFERENCES dbo.ChatRun(Id) ON DELETE CASCADE,
        CONSTRAINT CK_ChatRunEvent_Seq CHECK (FirstSeq >= 1 AND LastSeq >= FirstSeq)
    );
END;
ELSE
    PRINT N'    -> ChatRunEvent 表已存在，跳过创建';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ChatRunEvent') AND name = N'UX_ChatRunEvent_Run_FirstSeq')
    CREATE UNIQUE INDEX UX_ChatRunEvent_Run_FirstSeq ON dbo.ChatRunEvent (RunId, FirstSeq) INCLUDE (LastSeq);
GO

IF OBJECT_ID(N'dbo.ChatRunSteering', N'U') IS NULL
BEGIN
    PRINT N'[Step 2.5] 创建 dbo.ChatRunSteering';
    CREATE TABLE dbo.ChatRunSteering
    (
        Id              BIGINT NOT NULL IDENTITY(1,1),
        RunId           BIGINT NOT NULL,
        Content         NVARCHAR(MAX) NOT NULL,
        -- 1=queued, 2=injected, 3=discarded
        Status          TINYINT NOT NULL CONSTRAINT DF_ChatRunSteering_Status DEFAULT (1),
        CreatedAt       DATETIME2(7) NOT NULL CONSTRAINT DF_ChatRunSteering_CreatedAt DEFAULT (SYSUTCDATETIME()),
        HandledAt       DATETIME2(7) NULL,
        InjectedStepId  BIGINT NULL,
        CONSTRAINT PK_ChatRunSteering PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ChatRunSteering_Run FOREIGN KEY (RunId) REFERENCES dbo.ChatRun(Id) ON DELETE CASCADE,
        CONSTRAINT CK_ChatRunSteering_Status CHECK (Status IN (1, 2, 3)),
        CONSTRAINT CK_ChatRunSteering_Handled CHECK ((Status = 1 AND HandledAt IS NULL) OR (Status <> 1 AND HandledAt IS NOT NULL))
    );
END;
ELSE
    PRINT N'    -> ChatRunSteering 表已存在，跳过创建';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ChatRunSteering') AND name = N'IX_ChatRunSteering_Run_Status')
    CREATE INDEX IX_ChatRunSteering_Run_Status ON dbo.ChatRunSteering (RunId, Status);
GO

/* Step 2.6: website hosting (gateway, DNS provider, hosted domain, published site). */
IF OBJECT_ID(N'dbo.WebGateway', N'U') IS NULL
BEGIN
    PRINT N'[Step 2.6] 创建 dbo.WebGateway';
    CREATE TABLE dbo.WebGateway
    (
        Id              INT NOT NULL IDENTITY(1,1),
        Name            NVARCHAR(128) NOT NULL,
        -- 1=Caddy2 Admin API
        GatewayType     TINYINT NOT NULL,
        AdminEndpoint   VARCHAR(2048) NOT NULL,
        -- Encrypted JSON credential (optional, e.g. basic auth for the admin endpoint).
        Credential      NVARCHAR(MAX) NULL,
        -- Comma-separated gateway server names to install routes into (Caddy: apps.http.servers keys).
        ServerNames     VARCHAR(512) NOT NULL,
        PublicIPv4      VARCHAR(45) NULL,
        PublicIPv6      VARCHAR(45) NULL,
        CnameTarget     VARCHAR(253) NULL,
        PublicScheme    VARCHAR(8) NOT NULL CONSTRAINT DF_WebGateway_PublicScheme DEFAULT ('https'),
        PublicPort      INT NOT NULL CONSTRAINT DF_WebGateway_PublicPort DEFAULT (443),
        IsEnabled       BIT NOT NULL CONSTRAINT DF_WebGateway_IsEnabled DEFAULT (1),
        CreatedAt       DATETIME2(7) NOT NULL CONSTRAINT DF_WebGateway_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt       DATETIME2(7) NOT NULL CONSTRAINT DF_WebGateway_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_WebGateway PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_WebGateway_Name UNIQUE (Name),
        CONSTRAINT CK_WebGateway_Type CHECK (GatewayType IN (1)),
        CONSTRAINT CK_WebGateway_Public CHECK (PublicScheme IN ('http', 'https') AND PublicPort BETWEEN 1 AND 65535)
    );
END;
ELSE
    PRINT N'    -> WebGateway 表已存在，跳过创建';
GO

IF OBJECT_ID(N'dbo.DnsProvider', N'U') IS NULL
BEGIN
    PRINT N'[Step 2.7] 创建 dbo.DnsProvider';
    CREATE TABLE dbo.DnsProvider
    (
        Id              INT NOT NULL IDENTITY(1,1),
        Name            NVARCHAR(128) NOT NULL,
        -- 1=Cloudflare, 2=Aliyun DNS
        ProviderType    TINYINT NOT NULL,
        -- Encrypted JSON credential.
        Credential      NVARCHAR(MAX) NOT NULL,
        IsEnabled       BIT NOT NULL CONSTRAINT DF_DnsProvider_IsEnabled DEFAULT (1),
        CreatedAt       DATETIME2(7) NOT NULL CONSTRAINT DF_DnsProvider_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt       DATETIME2(7) NOT NULL CONSTRAINT DF_DnsProvider_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_DnsProvider PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_DnsProvider_Name UNIQUE (Name),
        CONSTRAINT CK_DnsProvider_Type CHECK (ProviderType IN (1, 2))
    );
END;
ELSE
    PRINT N'    -> DnsProvider 表已存在，跳过创建';
GO

IF OBJECT_ID(N'dbo.HostedDomain', N'U') IS NULL
BEGIN
    PRINT N'[Step 2.8] 创建 dbo.HostedDomain';
    CREATE TABLE dbo.HostedDomain
    (
        Id              INT NOT NULL IDENTITY(1,1),
        -- Parent domain under which sites are published, e.g. chats-test.sdcb.ai
        Domain          VARCHAR(253) NOT NULL,
        -- DNS zone managed by the provider, e.g. sdcb.ai
        DnsZone         VARCHAR(253) NOT NULL,
        DnsProviderId   INT NOT NULL,
        WebGatewayId    INT NOT NULL,
        -- 1=A/AAAA to gateway public IPs, 2=CNAME to gateway CnameTarget
        RecordMode      TINYINT NOT NULL CONSTRAINT DF_HostedDomain_RecordMode DEFAULT (1),
        RecordTtl       INT NOT NULL CONSTRAINT DF_HostedDomain_RecordTtl DEFAULT (600),
        -- Cloudflare only: proxied record.
        IsProxied       BIT NOT NULL CONSTRAINT DF_HostedDomain_IsProxied DEFAULT (0),
        AllowAllUsers   BIT NOT NULL CONSTRAINT DF_HostedDomain_AllowAllUsers DEFAULT (0),
        IsEnabled       BIT NOT NULL CONSTRAINT DF_HostedDomain_IsEnabled DEFAULT (1),
        CreatedAt       DATETIME2(7) NOT NULL CONSTRAINT DF_HostedDomain_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt       DATETIME2(7) NOT NULL CONSTRAINT DF_HostedDomain_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_HostedDomain PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_HostedDomain_Domain UNIQUE (Domain),
        CONSTRAINT FK_HostedDomain_DnsProvider FOREIGN KEY (DnsProviderId) REFERENCES dbo.DnsProvider(Id),
        CONSTRAINT FK_HostedDomain_WebGateway FOREIGN KEY (WebGatewayId) REFERENCES dbo.WebGateway(Id),
        CONSTRAINT CK_HostedDomain_RecordMode CHECK (RecordMode IN (1, 2)),
        CONSTRAINT CK_HostedDomain_Ttl CHECK (RecordTtl >= 1)
    );
END;
ELSE
    PRINT N'    -> HostedDomain 表已存在，跳过创建';
GO

IF OBJECT_ID(N'dbo.PublishedSite', N'U') IS NULL
BEGIN
    PRINT N'[Step 2.9] 创建 dbo.PublishedSite';
    CREATE TABLE dbo.PublishedSite
    (
        Id                      BIGINT NOT NULL IDENTITY(1,1),
        UserId                  INT NOT NULL,
        HostedDomainId          INT NOT NULL,
        Subdomain               VARCHAR(63) NOT NULL,
        Hostname                VARCHAR(253) NOT NULL,
        ContainerResourceId     BIGINT NOT NULL,
        ContainerPortMappingId  BIGINT NULL,
        ContainerPort           INT NOT NULL,
        -- host:port the gateway proxies to.
        UpstreamDial            VARCHAR(300) NULL,
        -- 1=publishing, 2=active, 3=failed, 4=unpublishing, 5=unpublished
        Status                  TINYINT NOT NULL,
        -- JSON: provider record ids / gateway route ids created by Chats.
        DnsRecords              NVARCHAR(MAX) NULL,
        GatewayRoutes           NVARCHAR(MAX) NULL,
        LastError               NVARCHAR(MAX) NULL,
        CreatedByChatId         INT NULL,
        CreatedAt               DATETIME2(7) NOT NULL CONSTRAINT DF_PublishedSite_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt               DATETIME2(7) NOT NULL CONSTRAINT DF_PublishedSite_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UnpublishedAt           DATETIME2(7) NULL,
        CONSTRAINT PK_PublishedSite PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_PublishedSite_User FOREIGN KEY (UserId) REFERENCES dbo.[User](Id),
        CONSTRAINT FK_PublishedSite_HostedDomain FOREIGN KEY (HostedDomainId) REFERENCES dbo.HostedDomain(Id),
        CONSTRAINT FK_PublishedSite_Container FOREIGN KEY (ContainerResourceId) REFERENCES dbo.ContainerResource(Id),
        CONSTRAINT FK_PublishedSite_PortMapping FOREIGN KEY (ContainerPortMappingId) REFERENCES dbo.ContainerPortMapping(Id),
        CONSTRAINT CK_PublishedSite_Status CHECK (Status IN (1, 2, 3, 4, 5)),
        CONSTRAINT CK_PublishedSite_Port CHECK (ContainerPort BETWEEN 1 AND 65535)
    );
END;
ELSE
    PRINT N'    -> PublishedSite 表已存在，跳过创建';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PublishedSite') AND name = N'UX_PublishedSite_Hostname_Live')
    CREATE UNIQUE INDEX UX_PublishedSite_Hostname_Live ON dbo.PublishedSite (Hostname) WHERE Status <> 5;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PublishedSite') AND name = N'IX_PublishedSite_User_Status')
    CREATE INDEX IX_PublishedSite_User_Status ON dbo.PublishedSite (UserId, Status);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PublishedSite') AND name = N'IX_PublishedSite_Container')
    CREATE INDEX IX_PublishedSite_Container ON dbo.PublishedSite (ContainerResourceId);
GO

IF OBJECT_ID(N'dbo.PublishedSiteEvent', N'U') IS NULL
BEGIN
    PRINT N'[Step 2.10] 创建 dbo.PublishedSiteEvent';
    CREATE TABLE dbo.PublishedSiteEvent
    (
        Id              BIGINT NOT NULL IDENTITY(1,1),
        PublishedSiteId BIGINT NOT NULL,
        -- Short action name, e.g. publish, dns-create, route-create, unpublish, rollback, reconcile
        Action          VARCHAR(64) NOT NULL,
        IsSuccess       BIT NOT NULL,
        Message         NVARCHAR(MAX) NULL,
        OperatorUserId  INT NULL,
        CreatedAt       DATETIME2(7) NOT NULL CONSTRAINT DF_PublishedSiteEvent_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_PublishedSiteEvent PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_PublishedSiteEvent_Site FOREIGN KEY (PublishedSiteId) REFERENCES dbo.PublishedSite(Id) ON DELETE CASCADE
    );
END;
ELSE
    PRINT N'    -> PublishedSiteEvent 表已存在，跳过创建';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.PublishedSiteEvent') AND name = N'IX_PublishedSiteEvent_Site')
    CREATE INDEX IX_PublishedSiteEvent_Site ON dbo.PublishedSiteEvent (PublishedSiteId, Id);
GO

/* Step 2.11: provider-side file ids (OpenAI/Azure Responses, Anthropic, DeepSeek Files APIs). */
IF OBJECT_ID(N'dbo.ProviderFileMapping', N'U') IS NULL
BEGIN
    PRINT N'[Step 2.11] 创建 dbo.ProviderFileMapping';
    CREATE TABLE dbo.ProviderFileMapping
    (
        Id              BIGINT NOT NULL IDENTITY(1,1),
        FileId          INT NOT NULL,
        ModelKeyId      SMALLINT NOT NULL,
        -- Provider protocol family used for upload, e.g. openai, anthropic.
        Protocol        VARCHAR(32) NOT NULL,
        Purpose         VARCHAR(64) NOT NULL,
        RemoteFileId    VARCHAR(256) NOT NULL,
        -- 1=active, 2=expired, 3=invalid, 4=deleted
        Status          TINYINT NOT NULL CONSTRAINT DF_ProviderFileMapping_Status DEFAULT (1),
        UploadedAt      DATETIME2(7) NOT NULL CONSTRAINT DF_ProviderFileMapping_UploadedAt DEFAULT (SYSUTCDATETIME()),
        ExpiresAt       DATETIME2(7) NULL,
        LastUsedAt      DATETIME2(7) NULL,
        CONSTRAINT PK_ProviderFileMapping PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ProviderFileMapping_File FOREIGN KEY (FileId) REFERENCES dbo.[File](Id) ON DELETE CASCADE,
        CONSTRAINT FK_ProviderFileMapping_ModelKey FOREIGN KEY (ModelKeyId) REFERENCES dbo.ModelKey(Id) ON DELETE CASCADE,
        CONSTRAINT CK_ProviderFileMapping_Status CHECK (Status IN (1, 2, 3, 4))
    );
END;
ELSE
    PRINT N'    -> ProviderFileMapping 表已存在，跳过创建';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ProviderFileMapping') AND name = N'UX_ProviderFileMapping_Active')
    CREATE UNIQUE INDEX UX_ProviderFileMapping_Active ON dbo.ProviderFileMapping (FileId, ModelKeyId, Protocol, Purpose) WHERE Status = 1;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.ProviderFileMapping') AND name = N'IX_ProviderFileMapping_ExpiresAt')
    CREATE INDEX IX_ProviderFileMapping_ExpiresAt ON dbo.ProviderFileMapping (Status, ExpiresAt);
GO

PRINT N'[第二步] 2.0 运行态与扩展结构创建完成';
GO
