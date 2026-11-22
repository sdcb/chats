using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

public partial class ChatsDB : DbContext
{
    public ChatsDB(DbContextOptions<ChatsDB> options)
        : base(options)
    {
    }

    public virtual DbSet<BalanceTransaction> BalanceTransactions { get; set; }

    public virtual DbSet<Chat> Chats { get; set; }

    public virtual DbSet<ChatConfig> ChatConfigs { get; set; }

    public virtual DbSet<ChatConfigMcp> ChatConfigMcps { get; set; }

    public virtual DbSet<ChatConfigSnapshot> ChatConfigSnapshots { get; set; }

    public virtual DbSet<ChatContainerResourceAccess> ChatContainerResourceAccesses { get; set; }

    public virtual DbSet<ChatGroup> ChatGroups { get; set; }

    public virtual DbSet<ChatPreset> ChatPresets { get; set; }

    public virtual DbSet<ChatPresetSpan> ChatPresetSpans { get; set; }

    public virtual DbSet<ChatRun> ChatRuns { get; set; }

    public virtual DbSet<ChatRunEvent> ChatRunEvents { get; set; }

    public virtual DbSet<ChatRunSteering> ChatRunSteerings { get; set; }

    public virtual DbSet<ChatShare> ChatShares { get; set; }

    public virtual DbSet<ChatSpan> ChatSpans { get; set; }

    public virtual DbSet<ChatTag> ChatTags { get; set; }

    public virtual DbSet<ChatTurn> ChatTurns { get; set; }

    public virtual DbSet<ClientInfo> ClientInfos { get; set; }

    public virtual DbSet<ClientIp> ClientIps { get; set; }

    public virtual DbSet<ClientUserAgent> ClientUserAgents { get; set; }

    public virtual DbSet<Config> Configs { get; set; }

    public virtual DbSet<ContainerImage> ContainerImages { get; set; }

    public virtual DbSet<ContainerPortMapping> ContainerPortMappings { get; set; }

    public virtual DbSet<ContainerResource> ContainerResources { get; set; }

    public virtual DbSet<ContainerResourceTemplate> ContainerResourceTemplates { get; set; }

    public virtual DbSet<ContainerRuntimeNode> ContainerRuntimeNodes { get; set; }

    public virtual DbSet<ContainerVolume> ContainerVolumes { get; set; }

    public virtual DbSet<ContainerVolumeMount> ContainerVolumeMounts { get; set; }

    public virtual DbSet<DnsProvider> DnsProviders { get; set; }

    public virtual DbSet<File> Files { get; set; }

    public virtual DbSet<FileImageInfo> FileImageInfos { get; set; }

    public virtual DbSet<FileService> FileServices { get; set; }

    public virtual DbSet<FinishReason> FinishReasons { get; set; }

    public virtual DbSet<HostedDomain> HostedDomains { get; set; }

    public virtual DbSet<InvitationCode> InvitationCodes { get; set; }

    public virtual DbSet<KeycloakAttempt> KeycloakAttempts { get; set; }

    public virtual DbSet<LoginService> LoginServices { get; set; }

    public virtual DbSet<McpServer> McpServers { get; set; }

    public virtual DbSet<McpTool> McpTools { get; set; }

    public virtual DbSet<Model> Models { get; set; }

    public virtual DbSet<ModelKey> ModelKeys { get; set; }

    public virtual DbSet<ModelKeySnapshot> ModelKeySnapshots { get; set; }

    public virtual DbSet<ModelProviderOrder> ModelProviderOrders { get; set; }

    public virtual DbSet<ModelSnapshot> ModelSnapshots { get; set; }

    public virtual DbSet<PasswordAttempt> PasswordAttempts { get; set; }

    public virtual DbSet<Prompt> Prompts { get; set; }

    public virtual DbSet<ProviderFileMapping> ProviderFileMappings { get; set; }

    public virtual DbSet<PublishedSite> PublishedSites { get; set; }

    public virtual DbSet<PublishedSiteEvent> PublishedSiteEvents { get; set; }

    public virtual DbSet<RequestTrace> RequestTraces { get; set; }

    public virtual DbSet<RequestTracePayload> RequestTracePayloads { get; set; }

    public virtual DbSet<SmsAttempt> SmsAttempts { get; set; }

    public virtual DbSet<SmsRecord> SmsRecords { get; set; }

    public virtual DbSet<SmsStatus> SmsStatuses { get; set; }

    public virtual DbSet<SmsType> SmsTypes { get; set; }

    public virtual DbSet<Step> Steps { get; set; }

    public virtual DbSet<StepContent> StepContents { get; set; }

    public virtual DbSet<StepContentBlob> StepContentBlobs { get; set; }

    public virtual DbSet<StepContentFile> StepContentFiles { get; set; }

    public virtual DbSet<StepContentText> StepContentTexts { get; set; }

    public virtual DbSet<StepContentThink> StepContentThinks { get; set; }

    public virtual DbSet<StepContentToolCall> StepContentToolCalls { get; set; }

    public virtual DbSet<StepContentToolCallResponse> StepContentToolCallResponses { get; set; }

    public virtual DbSet<UsageTransaction> UsageTransactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserApiCache> UserApiCaches { get; set; }

    public virtual DbSet<UserApiCacheBody> UserApiCacheBodies { get; set; }

    public virtual DbSet<UserApiCacheUsage> UserApiCacheUsages { get; set; }

    public virtual DbSet<UserApiKey> UserApiKeys { get; set; }

    public virtual DbSet<UserApiUsage> UserApiUsages { get; set; }

    public virtual DbSet<UserBalance> UserBalances { get; set; }

    public virtual DbSet<UserConfig> UserConfigs { get; set; }

    public virtual DbSet<UserContainerQuotum> UserContainerQuota { get; set; }

    public virtual DbSet<UserInitialConfig> UserInitialConfigs { get; set; }

    public virtual DbSet<UserMcp> UserMcps { get; set; }

    public virtual DbSet<UserModel> UserModels { get; set; }

    public virtual DbSet<UserModelUsage> UserModelUsages { get; set; }

    public virtual DbSet<WebGateway> WebGateways { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BalanceTransaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_BalanceLog2");

            entity.HasOne(d => d.CreditUser).WithMany(p => p.BalanceTransactionCreditUsers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BalanceTransaction_CreditUserId");

            entity.HasOne(d => d.User).WithMany(p => p.BalanceTransactionUsers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BalanceTransaction_UserId");
        });

        modelBuilder.Entity<Chat>(entity =>
        {
            entity.HasOne(d => d.ChatGroup).WithMany(p => p.Chats)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_Chat_ChatGroup");

            entity.HasOne(d => d.LeafTurn).WithMany(p => p.Chats).HasConstraintName("FK_Chat_Message");

            entity.HasOne(d => d.User).WithMany(p => p.Chats)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Chat_UserId");

            entity.HasMany(d => d.ChatTags).WithMany(p => p.Chats)
                .UsingEntity<Dictionary<string, object>>(
                    "ChatTagChat",
                    r => r.HasOne<ChatTag>().WithMany()
                        .HasForeignKey("ChatTagId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_ChatTagChat_ChatTag"),
                    l => l.HasOne<Chat>().WithMany()
                        .HasForeignKey("ChatId")
                        .HasConstraintName("FK_ChatTagChat_Chat"),
                    j =>
                    {
                        j.HasKey("ChatId", "ChatTagId");
                        j.ToTable("ChatTagChat");
                    });
        });

        modelBuilder.Entity<ChatConfig>(entity =>
        {
            entity.HasOne(d => d.Model).WithMany(p => p.ChatConfigs)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ChatConfig_Model");
        });

        modelBuilder.Entity<ChatConfigMcp>(entity =>
        {
            entity.HasOne(d => d.ChatConfig).WithMany(p => p.ChatConfigMcps).HasConstraintName("FK_ChatConfigMcp_ChatConfig");

            entity.HasOne(d => d.McpServer).WithMany(p => p.ChatConfigMcps).HasConstraintName("FK_ChatConfigMcp_McpServer");
        });

        modelBuilder.Entity<ChatConfigSnapshot>(entity =>
        {
            entity.HasIndex(e => e.HashCode, "IX_ChatConfigSnapshot_HashCode").HasFilter("([HashCode] IS NOT NULL)");

            entity.HasIndex(e => new { e.ModelSnapshotId, e.HashCode }, "UX_ChatConfigSnapshot_ModelSnapshotId_HashCode")
                .IsUnique()
                .HasFilter("([HashCode] IS NOT NULL)");

            entity.HasOne(d => d.ModelSnapshot).WithMany(p => p.ChatConfigSnapshots)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatConfigSnapshot_ModelSnapshot");
        });

        modelBuilder.Entity<ChatContainerResourceAccess>(entity =>
        {
            entity.Property(e => e.GrantedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ChatContainerAccess_GrantedAt");

            entity.HasOne(d => d.Chat).WithMany(p => p.ChatContainerResourceAccesses)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatContainerAccess_Chat");

            entity.HasOne(d => d.ContainerResource).WithMany(p => p.ChatContainerResourceAccesses)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatContainerAccess_Container");
        });

        modelBuilder.Entity<ChatGroup>(entity =>
        {
            entity.HasOne(d => d.User).WithMany(p => p.ChatGroups)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatGroup_User");
        });

        modelBuilder.Entity<ChatPreset>(entity =>
        {
            entity.HasOne(d => d.User).WithMany(p => p.ChatPresets).HasConstraintName("FK_ChatPreset_User");
        });

        modelBuilder.Entity<ChatPresetSpan>(entity =>
        {
            entity.HasOne(d => d.ChatConfig).WithMany(p => p.ChatPresetSpans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatPresetSpan_Config");

            entity.HasOne(d => d.ChatPreset).WithMany(p => p.ChatPresetSpans).HasConstraintName("FK_ChatPresetSpan_Preset");
        });

        modelBuilder.Entity<ChatRun>(entity =>
        {
            entity.HasIndex(e => e.CompletedAt, "IX_ChatRun_CompletedAt").HasFilter("([CompletedAt] IS NOT NULL)");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ChatRun_CreatedAt");
            entity.Property(e => e.HeartbeatAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ChatRun_HeartbeatAt");
            entity.Property(e => e.Status).HasDefaultValue((byte)1, "DF_ChatRun_Status");

            entity.HasOne(d => d.Chat).WithMany(p => p.ChatRuns).HasConstraintName("FK_ChatRun_Chat");

            entity.HasOne(d => d.User).WithMany(p => p.ChatRuns)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatRun_User");
        });

        modelBuilder.Entity<ChatRunEvent>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ChatRunEvent_CreatedAt");

            entity.HasOne(d => d.Run).WithMany(p => p.ChatRunEvents).HasConstraintName("FK_ChatRunEvent_Run");
        });

        modelBuilder.Entity<ChatRunSteering>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ChatRunSteering_CreatedAt");
            entity.Property(e => e.Status).HasDefaultValue((byte)1, "DF_ChatRunSteering_Status");

            entity.HasOne(d => d.Run).WithMany(p => p.ChatRunSteerings).HasConstraintName("FK_ChatRunSteering_Run");
        });

        modelBuilder.Entity<ChatShare>(entity =>
        {
            entity.HasOne(d => d.Chat).WithMany(p => p.ChatShares).HasConstraintName("FK_ChatShare_Chat");
        });

        modelBuilder.Entity<ChatSpan>(entity =>
        {
            entity.HasOne(d => d.ChatConfig).WithMany(p => p.ChatSpans)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatSpan_ChatConfig");

            entity.HasOne(d => d.Chat).WithMany(p => p.ChatSpans).HasConstraintName("FK_ChatSpan_Chat");
        });

        modelBuilder.Entity<ChatTurn>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Message");

            entity.HasOne(d => d.ChatConfigSnapshot).WithMany(p => p.ChatTurns).HasConstraintName("FK_ChatTurn_ChatConfigSnapshot");

            entity.HasOne(d => d.Chat).WithMany(p => p.ChatTurns).HasConstraintName("FK_Message_Chat");

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent).HasConstraintName("FK_Message_ParentMessage");
        });

        modelBuilder.Entity<ClientInfo>(entity =>
        {
            entity.HasOne(d => d.ClientIp).WithMany(p => p.ClientInfos)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClientInfo_ClientIP");

            entity.HasOne(d => d.ClientUserAgent).WithMany(p => p.ClientInfos)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ClientInfo_ClientUserAgent");
        });

        modelBuilder.Entity<Config>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK_Configs");
        });

        modelBuilder.Entity<ContainerImage>(entity =>
        {
            entity.Property(e => e.IsEnabled).HasDefaultValue(true, "DF_ContainerImage_IsEnabled");
        });

        modelBuilder.Entity<ContainerPortMapping>(entity =>
        {
            entity.HasIndex(e => new { e.ContainerResourceId, e.ContainerPort, e.Protocol }, "UX_ContainerPortMapping_ContainerPort")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.HasIndex(e => new { e.RuntimeNodeId, e.HostPort, e.Protocol }, "UX_ContainerPortMapping_NodeHostPort")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ContainerPortMapping_CreatedAt");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_ContainerPortMapping_IsActive");
            entity.Property(e => e.Protocol).HasDefaultValue("tcp", "DF_ContainerPortMapping_Protocol");

            entity.HasOne(d => d.ContainerResource).WithMany(p => p.ContainerPortMappings)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContainerPortMapping_Container");

            entity.HasOne(d => d.RuntimeNode).WithMany(p => p.ContainerPortMappings)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContainerPortMapping_RuntimeNode");
        });

        modelBuilder.Entity<ContainerResource>(entity =>
        {
            entity.HasIndex(e => e.CleanupAt, "IX_ContainerResource_CleanupAt").HasFilter("([CleanupAt] IS NOT NULL AND [DeletedAt] IS NULL)");

            entity.HasIndex(e => new { e.OwnerChatId, e.OwnerTurnId, e.DeletedAt, e.StoppedAt }, "IX_ContainerResource_OwnerChat_Turn_Deleted_Stopped").HasFilter("([OwnerChatId] IS NOT NULL)");

            entity.HasIndex(e => new { e.OwnerTurnId, e.Name }, "IX_ContainerResource_OwnerTurn_Name").HasFilter("([OwnerTurnId] IS NOT NULL)");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ContainerResource_CreatedAt");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ContainerResource_UpdatedAt");

            entity.HasOne(d => d.OwnerChat).WithMany(p => p.ContainerResources).HasConstraintName("FK_ContainerResource_Chat");

            entity.HasOne(d => d.OwnerTurn).WithMany(p => p.ContainerResources)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_ContainerResource_Turn");

            entity.HasOne(d => d.OwnerUser).WithMany(p => p.ContainerResources)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContainerResource_User");

            entity.HasOne(d => d.RuntimeNode).WithMany(p => p.ContainerResources)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContainerResource_RuntimeNode");
        });

        modelBuilder.Entity<ContainerResourceTemplate>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ContainerTemplate_CreatedAt");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ContainerTemplate_UpdatedAt");
            entity.Property(e => e.Visibility).HasDefaultValue((byte)3, "DF_ContainerTemplate_Visibility");

            entity.HasOne(d => d.RuntimeNode).WithMany(p => p.ContainerResourceTemplates)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContainerResourceTemplate_RuntimeNode");
        });

        modelBuilder.Entity<ContainerRuntimeNode>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ContainerRuntimeNode_CreatedAt");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true, "DF_ContainerRuntimeNode_IsEnabled");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ContainerRuntimeNode_UpdatedAt");
        });

        modelBuilder.Entity<ContainerVolume>(entity =>
        {
            entity.HasIndex(e => e.ContainerResourceId, "UX_ContainerVolume_InternalContainer")
                .IsUnique()
                .HasFilter("([IsStandalone]=(0) AND [ContainerResourceId] IS NOT NULL)");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ContainerVolume_CreatedAt");
            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_ContainerVolume_IsActive");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ContainerVolume_UpdatedAt");

            entity.HasOne(d => d.ContainerResource).WithOne(p => p.ContainerVolume).HasConstraintName("FK_ContainerVolume_Container");

            entity.HasOne(d => d.OwnerUser).WithMany(p => p.ContainerVolumes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContainerVolume_User");

            entity.HasOne(d => d.RuntimeNode).WithMany(p => p.ContainerVolumes)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContainerVolume_RuntimeNode");
        });

        modelBuilder.Entity<ContainerVolumeMount>(entity =>
        {
            entity.HasIndex(e => new { e.VolumeId, e.ContainerResourceId, e.ContainerPath }, "UX_ContainerVolumeMount_ActivePath")
                .IsUnique()
                .HasFilter("([IsActive]=(1))");

            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_ContainerVolumeMount_Active");
            entity.Property(e => e.MountedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ContainerVolumeMount_MountedAt");

            entity.HasOne(d => d.ContainerResource).WithMany(p => p.ContainerVolumeMounts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContainerVolumeMount_Container");

            entity.HasOne(d => d.Volume).WithMany(p => p.ContainerVolumeMounts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ContainerVolumeMount_Volume");
        });

        modelBuilder.Entity<DnsProvider>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_DnsProvider_CreatedAt");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true, "DF_DnsProvider_IsEnabled");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_DnsProvider_UpdatedAt");
        });

        modelBuilder.Entity<File>(entity =>
        {
            entity.HasOne(d => d.ClientInfo).WithMany(p => p.Files)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_File_ClientInfo");

            entity.HasOne(d => d.CreateUser).WithMany(p => p.Files)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_File_User");

            entity.HasOne(d => d.FileService).WithMany(p => p.Files)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_File_FileService");
        });

        modelBuilder.Entity<FileImageInfo>(entity =>
        {
            entity.Property(e => e.FileId).ValueGeneratedNever();

            entity.HasOne(d => d.File).WithOne(p => p.FileImageInfo).HasConstraintName("FK_FileImageInfo_File");
        });

        modelBuilder.Entity<FileService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_FileServices2");
        });

        modelBuilder.Entity<HostedDomain>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_HostedDomain_CreatedAt");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true, "DF_HostedDomain_IsEnabled");
            entity.Property(e => e.RecordMode).HasDefaultValue((byte)1, "DF_HostedDomain_RecordMode");
            entity.Property(e => e.RecordTtl).HasDefaultValue(600, "DF_HostedDomain_RecordTtl");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_HostedDomain_UpdatedAt");

            entity.HasOne(d => d.DnsProvider).WithMany(p => p.HostedDomains)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HostedDomain_DnsProvider");

            entity.HasOne(d => d.WebGateway).WithMany(p => p.HostedDomains)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_HostedDomain_WebGateway");
        });

        modelBuilder.Entity<InvitationCode>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("InvitationCode2_pkey");
        });

        modelBuilder.Entity<KeycloakAttempt>(entity =>
        {
            entity.HasOne(d => d.ClientInfo).WithMany(p => p.KeycloakAttempts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_KeycloakAttempt_ClientInfo");

            entity.HasOne(d => d.User).WithMany(p => p.KeycloakAttempts).HasConstraintName("FK_KeycloakAttempt_User");
        });

        modelBuilder.Entity<LoginService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_LoginServices2");
        });

        modelBuilder.Entity<McpServer>(entity =>
        {
            entity.HasOne(d => d.OwnerUser).WithMany(p => p.McpServers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_McpServer_User");
        });

        modelBuilder.Entity<McpTool>(entity =>
        {
            entity.HasOne(d => d.McpServer).WithMany(p => p.McpTools).HasConstraintName("FK_McpTool_McpServer");
        });

        modelBuilder.Entity<Model>(entity =>
        {
            entity.HasIndex(e => e.CurrentSnapshotId, "UX_Model_CurrentSnapshotId")
                .IsUnique()
                .HasFilter("([CurrentSnapshotId] IS NOT NULL)");

            entity.HasOne(d => d.CurrentSnapshot).WithOne(p => p.Model)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Model_CurrentSnapshot");
        });

        modelBuilder.Entity<ModelKey>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ModelKey2");

            entity.HasIndex(e => e.CurrentSnapshotId, "UX_ModelKey_CurrentSnapshotId")
                .IsUnique()
                .HasFilter("([CurrentSnapshotId] IS NOT NULL)");

            entity.HasOne(d => d.CurrentSnapshot).WithOne(p => p.ModelKey)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ModelKey_CurrentSnapshot");
        });

        modelBuilder.Entity<ModelProviderOrder>(entity =>
        {
            entity.Property(e => e.ModelProviderId).ValueGeneratedNever();
        });

        modelBuilder.Entity<ModelSnapshot>(entity =>
        {
            entity.HasOne(d => d.ModelKeySnapshot).WithMany(p => p.ModelSnapshots)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ModelSnapshot_ModelKeySnapshot");
        });

        modelBuilder.Entity<PasswordAttempt>(entity =>
        {
            entity.HasOne(d => d.ClientInfo).WithMany(p => p.PasswordAttempts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PasswordAttempt_ClientInfo");

            entity.HasOne(d => d.User).WithMany(p => p.PasswordAttempts).HasConstraintName("FK_PasswordAttempt_User");
        });

        modelBuilder.Entity<Prompt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Prompt2");

            entity.HasOne(d => d.CreateUser).WithMany(p => p.Prompts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Prompt_CreateUserId");
        });

        modelBuilder.Entity<ProviderFileMapping>(entity =>
        {
            entity.HasIndex(e => new { e.FileId, e.ModelKeyId, e.Protocol, e.Purpose }, "UX_ProviderFileMapping_Active")
                .IsUnique()
                .HasFilter("([Status]=(1))");

            entity.Property(e => e.Status).HasDefaultValue((byte)1, "DF_ProviderFileMapping_Status");
            entity.Property(e => e.UploadedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_ProviderFileMapping_UploadedAt");

            entity.HasOne(d => d.File).WithMany(p => p.ProviderFileMappings).HasConstraintName("FK_ProviderFileMapping_File");

            entity.HasOne(d => d.ModelKey).WithMany(p => p.ProviderFileMappings).HasConstraintName("FK_ProviderFileMapping_ModelKey");
        });

        modelBuilder.Entity<PublishedSite>(entity =>
        {
            entity.HasIndex(e => e.Hostname, "UX_PublishedSite_Hostname_Live")
                .IsUnique()
                .HasFilter("([Status]<>(5))");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_PublishedSite_CreatedAt");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_PublishedSite_UpdatedAt");

            entity.HasOne(d => d.ContainerPortMapping).WithMany(p => p.PublishedSites).HasConstraintName("FK_PublishedSite_PortMapping");

            entity.HasOne(d => d.ContainerResource).WithMany(p => p.PublishedSites)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PublishedSite_Container");

            entity.HasOne(d => d.HostedDomain).WithMany(p => p.PublishedSites)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PublishedSite_HostedDomain");

            entity.HasOne(d => d.User).WithMany(p => p.PublishedSites)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_PublishedSite_User");
        });

        modelBuilder.Entity<PublishedSiteEvent>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_PublishedSiteEvent_CreatedAt");

            entity.HasOne(d => d.PublishedSite).WithMany(p => p.PublishedSiteEvents).HasConstraintName("FK_PublishedSiteEvent_Site");
        });

        modelBuilder.Entity<RequestTrace>(entity =>
        {
            entity.HasKey(e => e.Id).IsClustered(false);

            entity.HasIndex(e => e.ScheduledDeleteAt, "IX_RequestTrace_ScheduledDeleteAt_NotNull").HasFilter("([ScheduledDeleteAt] IS NOT NULL)");

            entity.HasIndex(e => e.StartedAt, "IX_RequestTrace_StartedAt").IsClustered();

            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<RequestTracePayload>(entity =>
        {
            entity.Property(e => e.LogId).ValueGeneratedNever();

            entity.HasOne(d => d.Log).WithOne(p => p.RequestTracePayload).HasConstraintName("FK_RequestTracePayload_RequestTrace");
        });

        modelBuilder.Entity<SmsAttempt>(entity =>
        {
            entity.HasOne(d => d.ClientInfo).WithMany(p => p.SmsAttempts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SmsAttempt_ClientInfo");

            entity.HasOne(d => d.SmsRecord).WithMany(p => p.SmsAttempts).HasConstraintName("FK_SmsAttempt_SmsHistory");
        });

        modelBuilder.Entity<SmsRecord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_SmsHistory");

            entity.HasOne(d => d.Status).WithMany(p => p.SmsRecords)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SmsHistory_SmsStatus");

            entity.HasOne(d => d.Type).WithMany(p => p.SmsRecords)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SmsHistory_SmsType");

            entity.HasOne(d => d.User).WithMany(p => p.SmsRecords).HasConstraintName("FK_SmsRecord_UserId");
        });

        modelBuilder.Entity<Step>(entity =>
        {
            entity.HasOne(d => d.Turn).WithMany(p => p.Steps).HasConstraintName("FK_Step_Turn");

            entity.HasOne(d => d.Usage).WithMany(p => p.Steps).HasConstraintName("FK_Step_Usage");
        });

        modelBuilder.Entity<StepContent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_MessageContent2");

            entity.HasOne(d => d.Step).WithMany(p => p.StepContents).HasConstraintName("FK_StepContent_Step");
        });

        modelBuilder.Entity<StepContentBlob>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_MessageContentBlob");

            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.IdNavigation).WithOne(p => p.StepContentBlob).HasConstraintName("FK_MessageContentBlob_MessageContent");
        });

        modelBuilder.Entity<StepContentFile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_MessageContentFile");

            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.File).WithMany(p => p.StepContentFiles).HasConstraintName("FK_MessageContentFile_File");

            entity.HasOne(d => d.IdNavigation).WithOne(p => p.StepContentFile).HasConstraintName("FK_MessageContentFile_MessageContent");
        });

        modelBuilder.Entity<StepContentText>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_MessageContentText");

            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.IdNavigation).WithOne(p => p.StepContentText).HasConstraintName("FK_MessageContentUTF16_MessageContent");
        });

        modelBuilder.Entity<StepContentThink>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.IdNavigation).WithOne(p => p.StepContentThink).HasConstraintName("FK_StepContentThink_StepContent");
        });

        modelBuilder.Entity<StepContentToolCall>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_MessageContentToolCall");

            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.IdNavigation).WithOne(p => p.StepContentToolCall).HasConstraintName("FK_MessageContentToolCall_MessageContent");
        });

        modelBuilder.Entity<StepContentToolCallResponse>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_MessageContentToolCallResponse");

            entity.Property(e => e.Id).ValueGeneratedNever();

            entity.HasOne(d => d.IdNavigation).WithOne(p => p.StepContentToolCallResponse).HasConstraintName("FK_MessageContentToolCallResponse_MessageContent");
        });

        modelBuilder.Entity<UsageTransaction>(entity =>
        {
            entity.HasOne(d => d.CreditUser).WithMany(p => p.UsageTransactions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UsageTransaction_User");

            entity.HasOne(d => d.ModelSnapshot).WithMany(p => p.UsageTransactions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UsageTransaction_ModelSnapshot");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Users2_pkey");

            entity.HasMany(d => d.InvitationCodes).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "UserInvitation",
                    r => r.HasOne<InvitationCode>().WithMany()
                        .HasForeignKey("InvitationCodeId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_UserInvitation_InvitationCode"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("FK_UserInvitation_Users"),
                    j =>
                    {
                        j.HasKey("UserId", "InvitationCodeId").HasName("PK_UserInvitation_1");
                        j.ToTable("UserInvitation");
                    });
        });

        modelBuilder.Entity<UserApiCache>(entity =>
        {
            entity.HasOne(d => d.ClientInfo).WithMany(p => p.UserApiCaches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserApiCache_ClientInfoId");

            entity.HasOne(d => d.Model).WithMany(p => p.UserApiCaches).HasConstraintName("FK_UserApiCache_ModelId");

            entity.HasOne(d => d.UserApiKey).WithMany(p => p.UserApiCaches).HasConstraintName("FK_UserApiCache_UserApiKeyId");
        });

        modelBuilder.Entity<UserApiCacheBody>(entity =>
        {
            entity.Property(e => e.UserApiCacheId).ValueGeneratedNever();

            entity.HasOne(d => d.UserApiCache).WithOne(p => p.UserApiCacheBody).HasConstraintName("FK_UserApiCacheBody_Id");
        });

        modelBuilder.Entity<UserApiCacheUsage>(entity =>
        {
            entity.HasOne(d => d.ClientInfo).WithMany(p => p.UserApiCacheUsages).HasConstraintName("FK_UserApiCacheUsage_ClientInfoId");

            entity.HasOne(d => d.UserApiCache).WithMany(p => p.UserApiCacheUsages).HasConstraintName("FK_UserApiCacheUsage_UserApiCacheId");
        });

        modelBuilder.Entity<UserApiKey>(entity =>
        {
            entity.HasOne(d => d.User).WithMany(p => p.UserApiKeys)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserApiKey_UserId");

            entity.HasMany(d => d.Models).WithMany(p => p.ApiKeys)
                .UsingEntity<Dictionary<string, object>>(
                    "UserApiModel",
                    r => r.HasOne<Model>().WithMany()
                        .HasForeignKey("ModelId")
                        .HasConstraintName("FK_ApiKeyModel2_Model"),
                    l => l.HasOne<UserApiKey>().WithMany()
                        .HasForeignKey("ApiKeyId")
                        .HasConstraintName("FK_ApiKeyModel2_ApiKey"),
                    j =>
                    {
                        j.HasKey("ApiKeyId", "ModelId").HasName("PK_ApiKeyModel2");
                        j.ToTable("UserApiModel");
                    });
        });

        modelBuilder.Entity<UserApiUsage>(entity =>
        {
            entity.HasOne(d => d.ApiKey).WithMany(p => p.UserApiUsages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ApiUsage2_ApiKey");

            entity.HasOne(d => d.Usage).WithOne(p => p.UserApiUsage)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserApiUsage_UserModelUsage");
        });

        modelBuilder.Entity<UserBalance>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_UserBalances2");

            entity.HasOne(d => d.User).WithOne(p => p.UserBalance)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserBalance_UserId");
        });

        modelBuilder.Entity<UserConfig>(entity =>
        {
            entity.HasOne(d => d.User).WithMany(p => p.UserConfigs).HasConstraintName("FK_UserConfig_User");
        });

        modelBuilder.Entity<UserContainerQuotum>(entity =>
        {
            entity.HasIndex(e => e.UserId, "UX_UserContainerQuota_Default")
                .IsUnique()
                .HasFilter("([UserId] IS NULL)");

            entity.HasIndex(e => e.UserId, "UX_UserContainerQuota_User")
                .IsUnique()
                .HasFilter("([UserId] IS NOT NULL)");

            entity.Property(e => e.AllowedNetworkModes).HasDefaultValue("none,bridge", "DF_UserContainerQuota_AllowedNetworkModes");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_UserContainerQuota_UpdatedAt");

            entity.HasOne(d => d.User).WithOne(p => p.UserContainerQuotum).HasConstraintName("FK_UserContainerQuota_User");
        });

        modelBuilder.Entity<UserInitialConfig>(entity =>
        {
            entity.HasOne(d => d.InvitationCode).WithMany(p => p.UserInitialConfigs)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("FK_UserInitialConfig_InvitationCode");
        });

        modelBuilder.Entity<UserMcp>(entity =>
        {
            entity.HasOne(d => d.McpServer).WithMany(p => p.UserMcps).HasConstraintName("FK_UserMcp_McpServer");

            entity.HasOne(d => d.User).WithMany(p => p.UserMcps)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserMcp_User");
        });

        modelBuilder.Entity<UserModel>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_UserModel2");

            entity.HasOne(d => d.Model).WithMany(p => p.UserModels).HasConstraintName("FK_UserModel2_Model");

            entity.HasOne(d => d.User).WithMany(p => p.UserModels)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserModel_UserId");
        });

        modelBuilder.Entity<UserModelUsage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ModelUsage");

            entity.HasIndex(e => e.BalanceTransactionId, "IX_ModelUsage_BalanceTransaction")
                .IsUnique()
                .HasFilter("([BalanceTransactionId] IS NOT NULL)");

            entity.HasIndex(e => e.UsageTransactionId, "IX_ModelUsage_UsageTransaction")
                .IsUnique()
                .HasFilter("([UsageTransactionId] IS NOT NULL)");

            entity.HasOne(d => d.BalanceTransaction).WithOne(p => p.UserModelUsage).HasConstraintName("FK_ModelUsage_TransactionLog");

            entity.HasOne(d => d.ClientInfo).WithMany(p => p.UserModelUsages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ModelUsage_ClientInfo");

            entity.HasOne(d => d.FinishReason).WithMany(p => p.UserModelUsages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserModelUsage_FinishReason");

            entity.HasOne(d => d.ModelSnapshot).WithMany(p => p.UserModelUsages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserModelUsage_ModelSnapshot");

            entity.HasOne(d => d.UsageTransaction).WithOne(p => p.UserModelUsage).HasConstraintName("FK_ModelUsage_UsageTransactionLog");

            entity.HasOne(d => d.User).WithMany(p => p.UserModelUsages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserModelUsage_User");
        });

        modelBuilder.Entity<WebGateway>(entity =>
        {
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_WebGateway_CreatedAt");
            entity.Property(e => e.IsEnabled).HasDefaultValue(true, "DF_WebGateway_IsEnabled");
            entity.Property(e => e.PublicPort).HasDefaultValue(443, "DF_WebGateway_PublicPort");
            entity.Property(e => e.PublicScheme).HasDefaultValue("https", "DF_WebGateway_PublicScheme");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("(sysutcdatetime())", "DF_WebGateway_UpdatedAt");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
