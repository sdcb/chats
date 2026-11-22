-- [2.0] Persistent containers, agent runs, website hosting and provider file ids for SQLite
-- Mirrors src/scripts/db-migration/2.x/2.0.sql (SQL Server).
-- Not idempotent: run exactly once against a 1.17-era database.
--
-- Datetime columns follow EF Core's SQLite TEXT format ("yyyy-MM-dd HH:mm:ss.FFFFFFF", UTC).

PRAGMA foreign_keys = ON;

BEGIN IMMEDIATE TRANSACTION;

-- ============================================================
-- Step 1.1: remove the 1.x temporary-session model
-- ============================================================
DROP TABLE IF EXISTS "ChatDockerSession";

-- ============================================================
-- Step 1.2: ContainerRuntimeNode (+ 2.0 port pool columns)
-- ============================================================
CREATE TABLE "ContainerRuntimeNode" (
    "Id"             INTEGER NOT NULL CONSTRAINT "PK_ContainerRuntimeNode" PRIMARY KEY AUTOINCREMENT,
    "Name"           TEXT NOT NULL,
    "AiName"         TEXT NOT NULL,
    "Description"    TEXT NULL,
    -- 1=Docker, 2=Windows Docker, 3=Kubernetes, 4=Other
    "BackendType"    INTEGER NOT NULL CHECK ("BackendType" IN (1, 2, 3, 4)),
    "Endpoint"       TEXT NULL,
    "Credential"     TEXT NULL,
    "IsEnabled"      INTEGER NOT NULL DEFAULT 1,
    "CreatedAt"      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "UpdatedAt"      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "PublicHost"     TEXT NULL,
    "PortRangeStart" INTEGER NULL,
    "PortRangeEnd"   INTEGER NULL,
    CONSTRAINT "UQ_ContainerRuntimeNode_Name" UNIQUE ("Name"),
    CONSTRAINT "UQ_ContainerRuntimeNode_AiName" UNIQUE ("AiName"),
    CONSTRAINT "CK_ContainerRuntimeNode_PortRange" CHECK
        (("PortRangeStart" IS NULL AND "PortRangeEnd" IS NULL) OR
         ("PortRangeStart" BETWEEN 1 AND 65535 AND "PortRangeEnd" BETWEEN "PortRangeStart" AND 65535))
);

INSERT INTO "ContainerRuntimeNode" ("Name", "AiName", "Description", "BackendType", "Endpoint", "Credential", "IsEnabled")
VALUES ('default-docker', 'linux', 'Default Linux Docker runtime', 1, NULL, NULL, 1);

-- ============================================================
-- Step 1.3: ContainerResource
-- ============================================================
CREATE TABLE "ContainerResource" (
    "Id"                 INTEGER NOT NULL CONSTRAINT "PK_ContainerResource" PRIMARY KEY AUTOINCREMENT,
    "OwnerUserId"        INTEGER NOT NULL,
    "OwnerChatId"        INTEGER NULL,
    "OwnerTurnId"        INTEGER NULL,
    "RuntimeNodeId"      INTEGER NOT NULL,
    "IsPermanent"        INTEGER NOT NULL DEFAULT 0,
    "BackendResourceId"  TEXT NOT NULL,
    "Ip"                 TEXT NULL,
    "Name"               TEXT NOT NULL,
    "Image"              TEXT NOT NULL,
    "ShellPrefix"        TEXT NULL,
    "CpuCores"           REAL NULL,
    "MemoryBytes"        INTEGER NULL,
    "MaxProcesses"       INTEGER NULL,
    "BackendNetworkName" TEXT NULL,
    "CreatedAt"          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "UpdatedAt"          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "LastActiveAt"       TEXT NULL,
    "StoppedAt"          TEXT NULL,
    "DeletedAt"          TEXT NULL,
    "CleanupAt"          TEXT NULL,
    CONSTRAINT "FK_ContainerResource_User" FOREIGN KEY ("OwnerUserId") REFERENCES "User" ("Id"),
    CONSTRAINT "FK_ContainerResource_Chat" FOREIGN KEY ("OwnerChatId") REFERENCES "Chat" ("Id"),
    CONSTRAINT "FK_ContainerResource_Turn" FOREIGN KEY ("OwnerTurnId") REFERENCES "ChatTurn" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_ContainerResource_RuntimeNode" FOREIGN KEY ("RuntimeNodeId") REFERENCES "ContainerRuntimeNode" ("Id"),
    CONSTRAINT "CK_ContainerResource_Limits" CHECK
        (("CpuCores" IS NULL OR "CpuCores" >= 0) AND
         ("MemoryBytes" IS NULL OR "MemoryBytes" >= 0) AND
         ("MaxProcesses" IS NULL OR "MaxProcesses" >= 0)),
    CONSTRAINT "CK_ContainerResource_Cleanup" CHECK
        ("IsPermanent" = 1 OR "CleanupAt" IS NOT NULL OR "DeletedAt" IS NOT NULL)
);
CREATE INDEX "IX_ContainerResource_OwnerUser_Deleted_Stopped" ON "ContainerResource" ("OwnerUserId", "DeletedAt", "StoppedAt", "IsPermanent");
CREATE INDEX "IX_ContainerResource_OwnerTurn_Name" ON "ContainerResource" ("OwnerTurnId", "Name") WHERE "OwnerTurnId" IS NOT NULL;
CREATE INDEX "IX_ContainerResource_OwnerChat_Turn_Deleted_Stopped" ON "ContainerResource" ("OwnerChatId", "OwnerTurnId", "DeletedAt", "StoppedAt") WHERE "OwnerChatId" IS NOT NULL;
CREATE UNIQUE INDEX "UX_ContainerResource_RuntimeNode_BackendResource" ON "ContainerResource" ("RuntimeNodeId", "BackendResourceId");
CREATE INDEX "IX_ContainerResource_RuntimeNode_Deleted_Stopped" ON "ContainerResource" ("RuntimeNodeId", "DeletedAt", "StoppedAt");
CREATE INDEX "IX_ContainerResource_CleanupAt" ON "ContainerResource" ("CleanupAt") WHERE "CleanupAt" IS NOT NULL AND "DeletedAt" IS NULL;

-- ============================================================
-- Step 1.4 / 1.5: ContainerVolume, ContainerVolumeMount
-- ============================================================
CREATE TABLE "ContainerVolume" (
    "Id"                  INTEGER NOT NULL CONSTRAINT "PK_ContainerVolume" PRIMARY KEY AUTOINCREMENT,
    "OwnerUserId"         INTEGER NOT NULL,
    "RuntimeNodeId"       INTEGER NOT NULL,
    "ContainerResourceId" INTEGER NULL,
    "IsStandalone"        INTEGER NOT NULL DEFAULT 0,
    "BackendVolumeId"     TEXT NULL,
    "Name"                TEXT NOT NULL,
    "DeclaredBytes"       INTEGER NULL,
    "IsActive"            INTEGER NOT NULL DEFAULT 1,
    "CreatedAt"           TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "UpdatedAt"           TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "DeletedAt"           TEXT NULL,
    CONSTRAINT "FK_ContainerVolume_User" FOREIGN KEY ("OwnerUserId") REFERENCES "User" ("Id"),
    CONSTRAINT "FK_ContainerVolume_RuntimeNode" FOREIGN KEY ("RuntimeNodeId") REFERENCES "ContainerRuntimeNode" ("Id"),
    CONSTRAINT "FK_ContainerVolume_Container" FOREIGN KEY ("ContainerResourceId") REFERENCES "ContainerResource" ("Id"),
    CONSTRAINT "CK_ContainerVolume_Size" CHECK ("DeclaredBytes" IS NULL OR "DeclaredBytes" >= 0),
    CONSTRAINT "CK_ContainerVolume_Ownership" CHECK
        (("IsStandalone" = 0 AND "ContainerResourceId" IS NOT NULL) OR
         ("IsStandalone" = 1 AND "ContainerResourceId" IS NULL)),
    CONSTRAINT "CK_ContainerVolume_ActiveState" CHECK
        (("IsActive" = 1 AND "DeletedAt" IS NULL) OR ("IsActive" = 0 AND "DeletedAt" IS NOT NULL))
);
CREATE UNIQUE INDEX "UX_ContainerVolume_InternalContainer" ON "ContainerVolume" ("ContainerResourceId") WHERE "IsStandalone" = 0 AND "ContainerResourceId" IS NOT NULL;
CREATE INDEX "IX_ContainerVolume_OwnerUser_Active" ON "ContainerVolume" ("OwnerUserId", "IsActive", "IsStandalone");

CREATE TABLE "ContainerVolumeMount" (
    "Id"                  INTEGER NOT NULL CONSTRAINT "PK_ContainerVolumeMount" PRIMARY KEY AUTOINCREMENT,
    "VolumeId"            INTEGER NOT NULL,
    "ContainerResourceId" INTEGER NOT NULL,
    "ContainerPath"       TEXT NOT NULL,
    "IsReadOnly"          INTEGER NOT NULL DEFAULT 0,
    "IsActive"            INTEGER NOT NULL DEFAULT 1,
    "MountedAt"           TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "UnmountedAt"         TEXT NULL,
    CONSTRAINT "FK_ContainerVolumeMount_Volume" FOREIGN KEY ("VolumeId") REFERENCES "ContainerVolume" ("Id"),
    CONSTRAINT "FK_ContainerVolumeMount_Container" FOREIGN KEY ("ContainerResourceId") REFERENCES "ContainerResource" ("Id"),
    CONSTRAINT "CK_ContainerVolumeMount_State" CHECK
        (("IsActive" = 1 AND "UnmountedAt" IS NULL) OR ("IsActive" = 0 AND "UnmountedAt" IS NOT NULL))
);
CREATE UNIQUE INDEX "UX_ContainerVolumeMount_ActivePath" ON "ContainerVolumeMount" ("VolumeId", "ContainerResourceId", "ContainerPath") WHERE "IsActive" = 1;
CREATE INDEX "IX_ContainerVolumeMount_Container_Active" ON "ContainerVolumeMount" ("ContainerResourceId", "IsActive");

-- ============================================================
-- Step 1.6: ChatContainerResourceAccess
-- ============================================================
CREATE TABLE "ChatContainerResourceAccess" (
    "Id"                  INTEGER NOT NULL CONSTRAINT "PK_ChatContainerResourceAccess" PRIMARY KEY AUTOINCREMENT,
    "ChatId"              INTEGER NOT NULL,
    "ContainerResourceId" INTEGER NOT NULL,
    "GrantedAt"           TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    CONSTRAINT "FK_ChatContainerAccess_Chat" FOREIGN KEY ("ChatId") REFERENCES "Chat" ("Id"),
    CONSTRAINT "FK_ChatContainerAccess_Container" FOREIGN KEY ("ContainerResourceId") REFERENCES "ContainerResource" ("Id"),
    CONSTRAINT "UQ_ChatContainerAccess_ChatContainer" UNIQUE ("ChatId", "ContainerResourceId")
);
CREATE INDEX "IX_ChatContainerAccess_Container" ON "ChatContainerResourceAccess" ("ContainerResourceId");

-- ============================================================
-- Step 1.7: UserContainerQuota (+ 2.0 port/site limits)
-- ============================================================
CREATE TABLE "UserContainerQuota" (
    "Id"                      INTEGER NOT NULL CONSTRAINT "PK_UserContainerQuota" PRIMARY KEY AUTOINCREMENT,
    "UserId"                  INTEGER NULL,
    "AllowCustomImage"        INTEGER NOT NULL DEFAULT 0,
    "AllowedNetworkModes"     TEXT NOT NULL DEFAULT 'none,bridge',
    "MaxContainerCount"       INTEGER NULL,
    "MaxCpuCores"             REAL NULL,
    "MaxMemoryBytes"          INTEGER NULL,
    "MaxContainerProcesses"   INTEGER NULL,
    "MaxVolumeBytes"          INTEGER NULL,
    "MaxContainerCpuCores"    REAL NULL,
    "MaxContainerMemoryBytes" INTEGER NULL,
    "MaxVolumeBytesPerVolume" INTEGER NULL,
    "UpdatedAt"               TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "MaxPortMappings"         INTEGER NULL,
    "MaxPublishedSites"       INTEGER NULL,
    CONSTRAINT "FK_UserContainerQuota_User" FOREIGN KEY ("UserId") REFERENCES "User" ("Id"),
    CONSTRAINT "CK_UserContainerQuota_Values" CHECK
        (("MaxContainerCount" IS NULL OR "MaxContainerCount" >= 0) AND
         ("MaxCpuCores" IS NULL OR "MaxCpuCores" >= 0) AND
         ("MaxMemoryBytes" IS NULL OR "MaxMemoryBytes" >= 0) AND
         ("MaxContainerProcesses" IS NULL OR "MaxContainerProcesses" >= 0) AND
         ("MaxVolumeBytes" IS NULL OR "MaxVolumeBytes" >= 0) AND
         ("MaxContainerCpuCores" IS NULL OR "MaxContainerCpuCores" >= 0) AND
         ("MaxContainerMemoryBytes" IS NULL OR "MaxContainerMemoryBytes" >= 0) AND
         ("MaxVolumeBytesPerVolume" IS NULL OR "MaxVolumeBytesPerVolume" >= 0))
);
CREATE UNIQUE INDEX "UX_UserContainerQuota_User" ON "UserContainerQuota" ("UserId") WHERE "UserId" IS NOT NULL;
CREATE UNIQUE INDEX "UX_UserContainerQuota_Default" ON "UserContainerQuota" (ifnull("UserId", 0)) WHERE "UserId" IS NULL;

INSERT INTO "UserContainerQuota" ("UserId", "AllowCustomImage", "AllowedNetworkModes")
VALUES (NULL, 1, 'none,bridge');

-- ============================================================
-- Step 1.8: ContainerImage, ContainerResourceTemplate
-- ============================================================
CREATE TABLE "ContainerImage" (
    "Id"          INTEGER NOT NULL CONSTRAINT "PK_ContainerImage" PRIMARY KEY AUTOINCREMENT,
    "Image"       TEXT NOT NULL,
    "Description" TEXT NULL,
    "IsEnabled"   INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_ContainerImage_Image" UNIQUE ("Image"),
    CONSTRAINT "CK_ContainerImage_Image" CHECK (length(trim("Image")) > 0)
);
INSERT INTO "ContainerImage" ("Image", "Description", "IsEnabled")
VALUES ('code-interpreter:latest', 'Pre-installed with common packages, suitable for most daily tasks', 1);

CREATE TABLE "ContainerResourceTemplate" (
    "Id"                 INTEGER NOT NULL CONSTRAINT "PK_ContainerResourceTemplate" PRIMARY KEY AUTOINCREMENT,
    "Name"               TEXT NOT NULL,
    "RuntimeNodeId"      INTEGER NOT NULL,
    "Image"              TEXT NOT NULL,
    "CpuCores"           REAL NOT NULL,
    "MemoryBytes"        INTEGER NOT NULL,
    "MaxProcesses"       INTEGER NOT NULL,
    "BackendNetworkName" TEXT NULL,
    "DefaultVolumeBytes" INTEGER NULL,
    -- 0=disabled, 1=user-visible, 2=AI-visible, 3=both.
    "Visibility"         INTEGER NOT NULL DEFAULT 3,
    "CreatedAt"          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "UpdatedAt"          TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    CONSTRAINT "UQ_ContainerResourceTemplate_Name" UNIQUE ("Name"),
    CONSTRAINT "FK_ContainerResourceTemplate_RuntimeNode" FOREIGN KEY ("RuntimeNodeId") REFERENCES "ContainerRuntimeNode" ("Id"),
    CONSTRAINT "CK_ContainerTemplate_Values" CHECK
        ("CpuCores" >= 0 AND "MemoryBytes" >= 0 AND "MaxProcesses" >= 0 AND ("DefaultVolumeBytes" IS NULL OR "DefaultVolumeBytes" >= 0)),
    CONSTRAINT "CK_ContainerTemplate_Visibility" CHECK ("Visibility" IN (0, 1, 2, 3))
);
INSERT INTO "ContainerResourceTemplate"
    ("Name", "RuntimeNodeId", "Image", "CpuCores", "MemoryBytes", "MaxProcesses", "BackendNetworkName", "DefaultVolumeBytes", "Visibility")
SELECT 'default-code-interpreter', "Id", 'code-interpreter:latest', 2.0, 2147483648, 200, 'bridge', NULL, 3
FROM "ContainerRuntimeNode" WHERE "Name" = 'default-docker';

-- ============================================================
-- Step 2.2: ContainerPortMapping
-- ============================================================
CREATE TABLE "ContainerPortMapping" (
    "Id"                  INTEGER NOT NULL CONSTRAINT "PK_ContainerPortMapping" PRIMARY KEY AUTOINCREMENT,
    "ContainerResourceId" INTEGER NOT NULL,
    "RuntimeNodeId"       INTEGER NOT NULL,
    "ContainerPort"       INTEGER NOT NULL,
    "HostPort"            INTEGER NOT NULL,
    "Protocol"            TEXT NOT NULL DEFAULT 'tcp',
    "IsActive"            INTEGER NOT NULL DEFAULT 1,
    "CreatedAt"           TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "RemovedAt"           TEXT NULL,
    CONSTRAINT "FK_ContainerPortMapping_Container" FOREIGN KEY ("ContainerResourceId") REFERENCES "ContainerResource" ("Id"),
    CONSTRAINT "FK_ContainerPortMapping_RuntimeNode" FOREIGN KEY ("RuntimeNodeId") REFERENCES "ContainerRuntimeNode" ("Id"),
    CONSTRAINT "CK_ContainerPortMapping_Ports" CHECK
        ("ContainerPort" BETWEEN 1 AND 65535 AND "HostPort" BETWEEN 1 AND 65535 AND "Protocol" IN ('tcp', 'udp')),
    CONSTRAINT "CK_ContainerPortMapping_State" CHECK
        (("IsActive" = 1 AND "RemovedAt" IS NULL) OR ("IsActive" = 0 AND "RemovedAt" IS NOT NULL))
);
CREATE UNIQUE INDEX "UX_ContainerPortMapping_NodeHostPort" ON "ContainerPortMapping" ("RuntimeNodeId", "HostPort", "Protocol") WHERE "IsActive" = 1;
CREATE UNIQUE INDEX "UX_ContainerPortMapping_ContainerPort" ON "ContainerPortMapping" ("ContainerResourceId", "ContainerPort", "Protocol") WHERE "IsActive" = 1;

-- ============================================================
-- Step 2.3 - 2.5: ChatRun, ChatRunEvent, ChatRunSteering
-- ============================================================
CREATE TABLE "ChatRun" (
    "Id"              INTEGER NOT NULL CONSTRAINT "PK_ChatRun" PRIMARY KEY AUTOINCREMENT,
    "ChatId"          INTEGER NOT NULL,
    "UserId"          INTEGER NOT NULL,
    "Kind"            INTEGER NOT NULL CHECK ("Kind" IN (1, 2, 3, 4)),
    "Status"          INTEGER NOT NULL DEFAULT 1 CHECK ("Status" IN (1, 2, 3, 4)),
    "Metadata"        TEXT NULL,
    "LastEventSeq"    INTEGER NOT NULL DEFAULT 0,
    "InstanceId"      TEXT NOT NULL,
    "StopRequestedAt" TEXT NULL,
    "CreatedAt"       TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "HeartbeatAt"     TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "CompletedAt"     TEXT NULL,
    CONSTRAINT "FK_ChatRun_Chat" FOREIGN KEY ("ChatId") REFERENCES "Chat" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ChatRun_User" FOREIGN KEY ("UserId") REFERENCES "User" ("Id"),
    CONSTRAINT "CK_ChatRun_Completed" CHECK
        (("Status" = 1 AND "CompletedAt" IS NULL) OR ("Status" <> 1 AND "CompletedAt" IS NOT NULL))
);
CREATE INDEX "IX_ChatRun_Chat_Status" ON "ChatRun" ("ChatId", "Status");
CREATE INDEX "IX_ChatRun_Status_Heartbeat" ON "ChatRun" ("Status", "HeartbeatAt");
CREATE INDEX "IX_ChatRun_CompletedAt" ON "ChatRun" ("CompletedAt") WHERE "CompletedAt" IS NOT NULL;

CREATE TABLE "ChatRunEvent" (
    "Id"        INTEGER NOT NULL CONSTRAINT "PK_ChatRunEvent" PRIMARY KEY AUTOINCREMENT,
    "RunId"     INTEGER NOT NULL,
    "FirstSeq"  INTEGER NOT NULL,
    "LastSeq"   INTEGER NOT NULL,
    "Payload"   TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    CONSTRAINT "FK_ChatRunEvent_Run" FOREIGN KEY ("RunId") REFERENCES "ChatRun" ("Id") ON DELETE CASCADE,
    CONSTRAINT "CK_ChatRunEvent_Seq" CHECK ("FirstSeq" >= 1 AND "LastSeq" >= "FirstSeq")
);
CREATE UNIQUE INDEX "UX_ChatRunEvent_Run_FirstSeq" ON "ChatRunEvent" ("RunId", "FirstSeq");

CREATE TABLE "ChatRunSteering" (
    "Id"             INTEGER NOT NULL CONSTRAINT "PK_ChatRunSteering" PRIMARY KEY AUTOINCREMENT,
    "RunId"          INTEGER NOT NULL,
    "Content"        TEXT NOT NULL,
    "Status"         INTEGER NOT NULL DEFAULT 1 CHECK ("Status" IN (1, 2, 3)),
    "CreatedAt"      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "HandledAt"      TEXT NULL,
    "InjectedStepId" INTEGER NULL,
    CONSTRAINT "FK_ChatRunSteering_Run" FOREIGN KEY ("RunId") REFERENCES "ChatRun" ("Id") ON DELETE CASCADE,
    CONSTRAINT "CK_ChatRunSteering_Handled" CHECK
        (("Status" = 1 AND "HandledAt" IS NULL) OR ("Status" <> 1 AND "HandledAt" IS NOT NULL))
);
CREATE INDEX "IX_ChatRunSteering_Run_Status" ON "ChatRunSteering" ("RunId", "Status");

-- ============================================================
-- Step 2.6 - 2.10: website hosting
-- ============================================================
CREATE TABLE "WebGateway" (
    "Id"            INTEGER NOT NULL CONSTRAINT "PK_WebGateway" PRIMARY KEY AUTOINCREMENT,
    "Name"          TEXT NOT NULL,
    "GatewayType"   INTEGER NOT NULL CHECK ("GatewayType" IN (1)),
    "AdminEndpoint" TEXT NOT NULL,
    "Credential"    TEXT NULL,
    "ServerNames"   TEXT NOT NULL,
    "PublicIPv4"    TEXT NULL,
    "PublicIPv6"    TEXT NULL,
    "CnameTarget"   TEXT NULL,
    "PublicScheme"  TEXT NOT NULL DEFAULT 'https',
    "PublicPort"    INTEGER NOT NULL DEFAULT 443,
    "IsEnabled"     INTEGER NOT NULL DEFAULT 1,
    "CreatedAt"     TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "UpdatedAt"     TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    CONSTRAINT "UQ_WebGateway_Name" UNIQUE ("Name"),
    CONSTRAINT "CK_WebGateway_Public" CHECK ("PublicScheme" IN ('http', 'https') AND "PublicPort" BETWEEN 1 AND 65535)
);

CREATE TABLE "DnsProvider" (
    "Id"           INTEGER NOT NULL CONSTRAINT "PK_DnsProvider" PRIMARY KEY AUTOINCREMENT,
    "Name"         TEXT NOT NULL,
    "ProviderType" INTEGER NOT NULL CHECK ("ProviderType" IN (1, 2)),
    "Credential"   TEXT NOT NULL,
    "IsEnabled"    INTEGER NOT NULL DEFAULT 1,
    "CreatedAt"    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "UpdatedAt"    TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    CONSTRAINT "UQ_DnsProvider_Name" UNIQUE ("Name")
);

CREATE TABLE "HostedDomain" (
    "Id"            INTEGER NOT NULL CONSTRAINT "PK_HostedDomain" PRIMARY KEY AUTOINCREMENT,
    "Domain"        TEXT NOT NULL,
    "DnsZone"       TEXT NOT NULL,
    "DnsProviderId" INTEGER NOT NULL,
    "WebGatewayId"  INTEGER NOT NULL,
    "RecordMode"    INTEGER NOT NULL DEFAULT 1 CHECK ("RecordMode" IN (1, 2)),
    "RecordTtl"     INTEGER NOT NULL DEFAULT 600 CHECK ("RecordTtl" >= 1),
    "IsProxied"     INTEGER NOT NULL DEFAULT 0,
    "AllowAllUsers" INTEGER NOT NULL DEFAULT 0,
    "IsEnabled"     INTEGER NOT NULL DEFAULT 1,
    "CreatedAt"     TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "UpdatedAt"     TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    CONSTRAINT "UQ_HostedDomain_Domain" UNIQUE ("Domain"),
    CONSTRAINT "FK_HostedDomain_DnsProvider" FOREIGN KEY ("DnsProviderId") REFERENCES "DnsProvider" ("Id"),
    CONSTRAINT "FK_HostedDomain_WebGateway" FOREIGN KEY ("WebGatewayId") REFERENCES "WebGateway" ("Id")
);

CREATE TABLE "PublishedSite" (
    "Id"                     INTEGER NOT NULL CONSTRAINT "PK_PublishedSite" PRIMARY KEY AUTOINCREMENT,
    "UserId"                 INTEGER NOT NULL,
    "HostedDomainId"         INTEGER NOT NULL,
    "Subdomain"              TEXT NOT NULL,
    "Hostname"               TEXT NOT NULL,
    "ContainerResourceId"    INTEGER NOT NULL,
    "ContainerPortMappingId" INTEGER NULL,
    "ContainerPort"          INTEGER NOT NULL CHECK ("ContainerPort" BETWEEN 1 AND 65535),
    "UpstreamDial"           TEXT NULL,
    "Status"                 INTEGER NOT NULL CHECK ("Status" IN (1, 2, 3, 4, 5)),
    "DnsRecords"             TEXT NULL,
    "GatewayRoutes"          TEXT NULL,
    "LastError"              TEXT NULL,
    "CreatedByChatId"        INTEGER NULL,
    "CreatedAt"              TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "UpdatedAt"              TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "UnpublishedAt"          TEXT NULL,
    CONSTRAINT "FK_PublishedSite_User" FOREIGN KEY ("UserId") REFERENCES "User" ("Id"),
    CONSTRAINT "FK_PublishedSite_HostedDomain" FOREIGN KEY ("HostedDomainId") REFERENCES "HostedDomain" ("Id"),
    CONSTRAINT "FK_PublishedSite_Container" FOREIGN KEY ("ContainerResourceId") REFERENCES "ContainerResource" ("Id"),
    CONSTRAINT "FK_PublishedSite_PortMapping" FOREIGN KEY ("ContainerPortMappingId") REFERENCES "ContainerPortMapping" ("Id")
);
CREATE UNIQUE INDEX "UX_PublishedSite_Hostname_Live" ON "PublishedSite" ("Hostname") WHERE "Status" <> 5;
CREATE INDEX "IX_PublishedSite_User_Status" ON "PublishedSite" ("UserId", "Status");
CREATE INDEX "IX_PublishedSite_Container" ON "PublishedSite" ("ContainerResourceId");

CREATE TABLE "PublishedSiteEvent" (
    "Id"              INTEGER NOT NULL CONSTRAINT "PK_PublishedSiteEvent" PRIMARY KEY AUTOINCREMENT,
    "PublishedSiteId" INTEGER NOT NULL,
    "Action"          TEXT NOT NULL,
    "IsSuccess"       INTEGER NOT NULL,
    "Message"         TEXT NULL,
    "OperatorUserId"  INTEGER NULL,
    "CreatedAt"       TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    CONSTRAINT "FK_PublishedSiteEvent_Site" FOREIGN KEY ("PublishedSiteId") REFERENCES "PublishedSite" ("Id") ON DELETE CASCADE
);
CREATE INDEX "IX_PublishedSiteEvent_Site" ON "PublishedSiteEvent" ("PublishedSiteId", "Id");

-- ============================================================
-- Step 2.11: ProviderFileMapping
-- ============================================================
CREATE TABLE "ProviderFileMapping" (
    "Id"           INTEGER NOT NULL CONSTRAINT "PK_ProviderFileMapping" PRIMARY KEY AUTOINCREMENT,
    "FileId"       INTEGER NOT NULL,
    "ModelKeyId"   INTEGER NOT NULL,
    "Protocol"     TEXT NOT NULL,
    "Purpose"      TEXT NOT NULL,
    "RemoteFileId" TEXT NOT NULL,
    "Status"       INTEGER NOT NULL DEFAULT 1 CHECK ("Status" IN (1, 2, 3, 4)),
    "UploadedAt"   TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%f', 'now')),
    "ExpiresAt"    TEXT NULL,
    "LastUsedAt"   TEXT NULL,
    CONSTRAINT "FK_ProviderFileMapping_File" FOREIGN KEY ("FileId") REFERENCES "File" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ProviderFileMapping_ModelKey" FOREIGN KEY ("ModelKeyId") REFERENCES "ModelKey" ("Id") ON DELETE CASCADE
);
CREATE UNIQUE INDEX "UX_ProviderFileMapping_Active" ON "ProviderFileMapping" ("FileId", "ModelKeyId", "Protocol", "Purpose") WHERE "Status" = 1;
CREATE INDEX "IX_ProviderFileMapping_ExpiresAt" ON "ProviderFileMapping" ("Status", "ExpiresAt");

COMMIT;

-- ============================================================
-- Best-effort verification
-- ============================================================
SELECT name AS "TableName"
FROM sqlite_master
WHERE type = 'table'
  AND name IN ('ContainerRuntimeNode', 'ContainerResource', 'ContainerVolume', 'ContainerVolumeMount',
               'ChatContainerResourceAccess', 'UserContainerQuota', 'ContainerImage', 'ContainerResourceTemplate',
               'ContainerPortMapping', 'ChatRun', 'ChatRunEvent', 'ChatRunSteering', 'WebGateway', 'DnsProvider',
               'HostedDomain', 'PublishedSite', 'PublishedSiteEvent', 'ProviderFileMapping')
ORDER BY name;

PRAGMA foreign_key_check;
