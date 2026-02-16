PRINT N'[1.11.0] 开始执行数据库迁移任务';

-- =============================================
-- Step 1: 创建 RequestTrace 主表
-- =============================================
PRINT N'[Step 1] 创建 RequestTrace 主表（若不存在）';

IF OBJECT_ID(N'dbo.RequestTrace', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RequestTrace
    (
        Id BIGINT NOT NULL IDENTITY(1, 1),                       -- 日志主键（高写入场景使用 BIGINT 自增）
        StartedAt DATETIME2(7) NOT NULL,                         -- 请求开始时间（UTC）
        DurationMs INT NOT NULL,                                 -- 耗时（毫秒）
        Direction TINYINT NOT NULL,                              -- 方向：0=Inbound（用户->后端），1=Outbound（后端->外部）
        Source NVARCHAR(100) NULL,                               -- 来源标识；入站可放来源IP/来源名，出站可放命名HttpClient或目标来源标签（100 足够覆盖 IPv6 文本）
        UserId INT NULL,                                         -- 关联用户（弱关联）；当前按你的要求保留索引+外键
        TraceId NVARCHAR(100) NULL,                              -- 串联键：用于把同一链路日志聚合；按我们约定可直接使用 HttpContext.TraceIdentifier
        [Method] VARCHAR(10) NOT NULL,                           -- HTTP 方法（GET/POST/...）
        [Url] NVARCHAR(2048) NOT NULL,                           -- 完整 URL；已去掉 Host/Path/Query 拆列，避免冗余字段
        RequestContentType NVARCHAR(200) NULL,                   -- 请求体 Content-Type
        ResponseContentType NVARCHAR(200) NULL,                  -- 响应体 Content-Type
        StatusCode SMALLINT NULL,                                -- 响应状态码；无响应异常场景允许为 NULL（不再使用 HasResponse）
        ErrorType NVARCHAR(50) NULL,                             -- 异常类型（建议枚举字符串，如 Timeout/Dns/Connect/UnhandledException）
        ErrorMessage NVARCHAR(MAX) NULL,                         -- 异常详情；inbound 可存 Exception.ToString()，outbound 通常为空
        RawRequestBodyBytes INT NOT NULL,                        -- 请求体原始字节数；0 表示无 body 或空 body
        RawResponseBodyBytes INT NULL,                           -- 响应体原始字节数；NULL 表示未知/未拿到响应，0 表示有响应但 body 为空
        IsRequestBodyTruncated BIT NOT NULL,                     -- 请求体是否被 maxBytes 截断
        IsResponseBodyTruncated BIT NOT NULL,                    -- 响应体是否被 maxBytes 截断

        CONSTRAINT PK_RequestTrace PRIMARY KEY CLUSTERED (Id),
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
        CREATE INDEX IX_RequestTrace_StartedAt
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

    IF OBJECT_ID(N'dbo.[User]', N'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_RequestTrace_User')
        BEGIN
            ALTER TABLE dbo.RequestTrace
            WITH CHECK ADD CONSTRAINT FK_RequestTrace_User
            FOREIGN KEY (UserId) REFERENCES dbo.[User](Id);

            ALTER TABLE dbo.RequestTrace
            CHECK CONSTRAINT FK_RequestTrace_User;

            PRINT N'    -> 已创建外键 FK_RequestTrace_User';
        END
        ELSE
        BEGIN
            PRINT N'    -> 外键 FK_RequestTrace_User 已存在，跳过';
        END
    END
    ELSE
    BEGIN
        PRINT N'    -> dbo.[User] 表不存在，跳过 FK_RequestTrace_User 创建';
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
        LogId BIGINT NOT NULL,                                                     -- 对应主表 RequestTrace.Id（1:1）
        RequestHeaders NVARCHAR(MAX) NOT NULL,                                     -- 请求头原始文本（按原样存储，建议使用 RFC 风格多行 header 文本；由应用层保证写入）
        ResponseHeaders NVARCHAR(MAX) NULL,                                        -- 响应头原始文本（按原样存储；无响应时可为空）
        RequestBody NVARCHAR(MAX) NULL,                                            -- 请求体文本（可检索、业务友好）
        ResponseBody NVARCHAR(MAX) NULL,                                           -- 响应体文本（可检索、业务友好）
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

PRINT N'[1.11.0] 数据库迁移任务完成';
GO
