PRINT N'[1.11.0] 开始执行数据库迁移任务';

-- =============================================
-- Step 1: 创建 RequestTrace 主表
-- =============================================
PRINT N'[Step 1] 创建 RequestTrace 主表（若不存在）';

IF OBJECT_ID(N'dbo.RequestTrace', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RequestTrace
    (
        Id UNIQUEIDENTIFIER NOT NULL,                            -- 日志主键（由应用层生成有序 GUID，例如 Guid.CreateVersion7()）
        StartedAt DATETIME2(7) NOT NULL,                         -- 请求开始时间（UTC）
        RequestBodyAt DATETIME2(7) NULL,                         -- 请求体读取完成时间（UTC）
        ResponseHeaderAt DATETIME2(7) NULL,                      -- 响应头接收完成时间（UTC）
        ResponseBodyAt DATETIME2(7) NULL,                        -- 响应体读取完成时间（UTC）
        Direction TINYINT NOT NULL,                              -- 方向：0=Inbound（用户->后端），1=Outbound（后端->外部）
        Source NVARCHAR(100) NULL,                               -- 来源标识；入站可放来源IP，出站可放命名HttpClient或目标来源标签（100 足够覆盖 IPv6 文本）
        UserId INT NULL,                                         -- 关联用户；保留索引，不创建外键
        TraceId VARCHAR(100) NULL,                               -- 串联键：用于把同一链路日志聚合；按我们约定可直接使用 HttpContext.TraceIdentifier
        [Method] VARCHAR(10) NOT NULL,                           -- HTTP 方法（GET/POST/...）
        [Url] NVARCHAR(2048) NOT NULL,                           -- 完整 URL；已去掉 Host/Path/Query 拆列，避免冗余字段
        RequestContentType VARCHAR(200) NULL,                    -- 请求体 Content-Type
        ResponseContentType VARCHAR(200) NULL,                   -- 响应体 Content-Type
        StatusCode SMALLINT NULL,                                -- 响应状态码；无响应异常场景允许为 NULL（不再使用 HasResponse）
        ErrorType NVARCHAR(50) NULL,                             -- 异常类型（建议枚举字符串，如 Timeout/Dns/Connect/UnhandledException）
        RawRequestBodyBytes INT NOT NULL,                        -- 请求体原始字节数；0 表示无 body 或空 body
        RawResponseBodyBytes INT NULL,                           -- 响应体原始字节数；NULL 表示未知/未拿到响应，0 表示有响应但 body 为空
        RequestBodyLength INT NOT NULL,                          -- 请求体长度（字符数）
        ResponseBodyLength INT NULL,                             -- 响应体长度（字符数）；NULL 表示未知/未拿到响应，0 表示有响应但 body 为空
        ScheduledDeleteAt DATETIME2(7) NULL,                     -- 预约删除时间（UTC）；用于自动删除策略

        CONSTRAINT PK_RequestTrace PRIMARY KEY NONCLUSTERED (Id),
        CONSTRAINT CK_RequestTrace_Direction CHECK (Direction IN (0, 1))
    );

    PRINT N'    -> 已创建 RequestTrace 表';
END
ELSE
BEGIN
    PRINT N'    -> RequestTrace 表已存在，跳过创建';
END

GO

-- =============================================
-- Step 2: 创建主表索引
-- =============================================
PRINT N'[Step 2] 创建 RequestTrace 索引（若不存在）';

IF OBJECT_ID(N'dbo.RequestTrace', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_RequestTrace_StartedAt'
          AND object_id = OBJECT_ID(N'dbo.RequestTrace')
    )
    BEGIN
        CREATE CLUSTERED INDEX IX_RequestTrace_StartedAt
        ON dbo.RequestTrace (StartedAt);
        PRINT N'    -> 已创建索引 IX_RequestTrace_StartedAt';
    END
    ELSE
    BEGIN
        PRINT N'    -> 索引 IX_RequestTrace_StartedAt 已存在，跳过';
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_RequestTrace_UserId'
          AND object_id = OBJECT_ID(N'dbo.RequestTrace')
    )
    BEGIN
        CREATE INDEX IX_RequestTrace_UserId
        ON dbo.RequestTrace (UserId);
        PRINT N'    -> 已创建索引 IX_RequestTrace_UserId';
    END
    ELSE
    BEGIN
        PRINT N'    -> 索引 IX_RequestTrace_UserId 已存在，跳过';
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_RequestTrace_TraceId'
          AND object_id = OBJECT_ID(N'dbo.RequestTrace')
    )
    BEGIN
        CREATE INDEX IX_RequestTrace_TraceId
        ON dbo.RequestTrace (TraceId);
        PRINT N'    -> 已创建索引 IX_RequestTrace_TraceId';
    END
    ELSE
    BEGIN
        PRINT N'    -> 索引 IX_RequestTrace_TraceId 已存在，跳过';
    END

    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'IX_RequestTrace_ScheduledDeleteAt_NotNull'
          AND object_id = OBJECT_ID(N'dbo.RequestTrace')
    )
    BEGIN
        CREATE NONCLUSTERED INDEX IX_RequestTrace_ScheduledDeleteAt_NotNull
        ON dbo.RequestTrace (ScheduledDeleteAt)
        WHERE ScheduledDeleteAt IS NOT NULL;
        PRINT N'    -> 已创建索引 IX_RequestTrace_ScheduledDeleteAt_NotNull';
    END
    ELSE
    BEGIN
        PRINT N'    -> 索引 IX_RequestTrace_ScheduledDeleteAt_NotNull 已存在，跳过';
    END

END
ELSE
BEGIN
    PRINT N'    -> RequestTrace 表不存在，跳过索引创建';
END

GO

-- =============================================
-- Step 3: 创建 RequestTracePayload 子表
-- =============================================
PRINT N'[Step 3] 创建 RequestTracePayload 子表/外键（若不存在）';

IF OBJECT_ID(N'dbo.RequestTracePayload', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RequestTracePayload
    (
        LogId UNIQUEIDENTIFIER NOT NULL,                                           -- 对应主表 RequestTrace.Id（1:1）
        RequestHeaders VARCHAR(MAX) NOT NULL,                                      -- 请求头原始文本（按原样存储，建议使用 RFC 风格多行 header 文本；由应用层保证写入）
        ResponseHeaders VARCHAR(MAX) NULL,                                         -- 响应头原始文本（按原样存储；无响应时可为空）
        RequestBody NVARCHAR(MAX) NULL,                                            -- 请求体文本（可检索、业务友好）
        ResponseBody NVARCHAR(MAX) NULL,                                           -- 响应体文本（可检索、业务友好）
        ErrorMessage NVARCHAR(MAX) NULL,                                           -- 异常详情；inbound 可存 Exception.ToString()，outbound 通常为空
        RequestBodyRaw VARBINARY(MAX) NULL,                                        -- 原始请求体二进制（未解压、未de-chunk、尽量原样；按配置开启）
        ResponseBodyRaw VARBINARY(MAX) NULL,                                       -- 原始响应体二进制（未解压、未de-chunk、尽量原样；按配置开启）

        CONSTRAINT PK_RequestTracePayload PRIMARY KEY CLUSTERED (LogId)
    );

    PRINT N'    -> 已创建 RequestTracePayload 表';
END
ELSE
BEGIN
    PRINT N'    -> RequestTracePayload 表已存在，跳过创建';
END

IF OBJECT_ID(N'dbo.RequestTracePayload', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.RequestTrace', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_RequestTracePayload_RequestTrace')
    BEGIN
        ALTER TABLE dbo.RequestTracePayload
        WITH CHECK ADD CONSTRAINT FK_RequestTracePayload_RequestTrace
        FOREIGN KEY (LogId) REFERENCES dbo.RequestTrace(Id)
        ON DELETE CASCADE;

        ALTER TABLE dbo.RequestTracePayload
        CHECK CONSTRAINT FK_RequestTracePayload_RequestTrace;

        PRINT N'    -> 已创建外键 FK_RequestTracePayload_RequestTrace';
    END
    ELSE
    BEGIN
        PRINT N'    -> 外键 FK_RequestTracePayload_RequestTrace 已存在，跳过';
    END
END
ELSE
BEGIN
    PRINT N'    -> 主表或子表不存在，跳过外键创建';
END

GO

-- =============================================
-- Step 4: 扩展 Config.Value 到最大长度
-- =============================================
PRINT N'[Step 4] 扩展 Config.Value 到 NVARCHAR(MAX)（若需要）';

IF OBJECT_ID(N'dbo.Config', N'U') IS NOT NULL
BEGIN
    IF EXISTS (
        SELECT 1
        FROM sys.columns c
        INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.Config')
          AND c.name = N'Value'
          AND (
                t.name <> N'nvarchar'
                OR c.max_length <> -1
              )
    )
    BEGIN
        ALTER TABLE dbo.Config
        ALTER COLUMN [Value] NVARCHAR(MAX) NOT NULL;

        PRINT N'    -> 已将 Config.Value 扩展为 NVARCHAR(MAX)';
    END
    ELSE
    BEGIN
        PRINT N'    -> Config.Value 已是 NVARCHAR(MAX)，跳过';
    END
END
ELSE
BEGIN
    PRINT N'    -> Config 表不存在，跳过扩展';
END

GO

PRINT N'[1.11.0] 数据库迁移任务完成';
GO
