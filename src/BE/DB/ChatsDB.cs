using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

public partial class ChatsDB : DbContext
{
    public ChatsDB(DbContextOptions<ChatsDB> options)
        : base(options)
    {
    }

    public virtual DbSet<BalanceTransaction> BalanceTransactions { get; set; }

    public virtual DbSet<Chat> Chats { get; set; }

    public virtual DbSet<ChatConfig> ChatConfigs { get; set; }

    public virtual DbSet<ChatConfigArchived> ChatConfigArchiveds { get; set; }

    public virtual DbSet<ChatConfigMcp> ChatConfigMcps { get; set; }

    public virtual DbSet<ChatGroup> ChatGroups { get; set; }

    public virtual DbSet<ChatPreset> ChatPresets { get; set; }

    public virtual DbSet<ChatPresetSpan> ChatPresetSpans { get; set; }

    public virtual DbSet<ChatRole> ChatRoles { get; set; }

    public virtual DbSet<ChatShare> ChatShares { get; set; }

    public virtual DbSet<ChatSpan> ChatSpans { get; set; }

    public virtual DbSet<ChatTag> ChatTags { get; set; }

    public virtual DbSet<ChatTurn> ChatTurns { get; set; }

    public virtual DbSet<ClientInfo> ClientInfos { get; set; }

    public virtual DbSet<ClientIp> ClientIps { get; set; }

    public virtual DbSet<ClientUserAgent> ClientUserAgents { get; set; }

    public virtual DbSet<Config> Configs { get; set; }

    public virtual DbSet<CurrencyRate> CurrencyRates { get; set; }

    public virtual DbSet<File> Files { get; set; }

    public virtual DbSet<FileContentType> FileContentTypes { get; set; }

    public virtual DbSet<FileImageInfo> FileImageInfos { get; set; }

    public virtual DbSet<FileService> FileServices { get; set; }

    public virtual DbSet<FileServiceType> FileServiceTypes { get; set; }

    public virtual DbSet<FinishReason> FinishReasons { get; set; }

    public virtual DbSet<InvitationCode> InvitationCodes { get; set; }

    public virtual DbSet<KnownImageSize> KnownImageSizes { get; set; }

    public virtual DbSet<LoginService> LoginServices { get; set; }

    public virtual DbSet<McpServer> McpServers { get; set; }

    public virtual DbSet<McpTool> McpTools { get; set; }

    public virtual DbSet<Model> Models { get; set; }

    public virtual DbSet<ModelKey> ModelKeys { get; set; }

    public virtual DbSet<ModelProvider> ModelProviders { get; set; }

    public virtual DbSet<ModelReference> ModelReferences { get; set; }

    public virtual DbSet<Prompt> Prompts { get; set; }

    public virtual DbSet<ReasoningResponseKind> ReasoningResponseKinds { get; set; }

    public virtual DbSet<SmsAttempt> SmsAttempts { get; set; }

    public virtual DbSet<SmsRecord> SmsRecords { get; set; }

    public virtual DbSet<SmsStatus> SmsStatuses { get; set; }

    public virtual DbSet<SmsType> SmsTypes { get; set; }

    public virtual DbSet<Step> Steps { get; set; }

    public virtual DbSet<StepContent> StepContents { get; set; }

    public virtual DbSet<StepContentBlob> StepContentBlobs { get; set; }

    public virtual DbSet<StepContentFile> StepContentFiles { get; set; }

    public virtual DbSet<StepContentText> StepContentTexts { get; set; }

    public virtual DbSet<StepContentToolCall> StepContentToolCalls { get; set; }

    public virtual DbSet<StepContentToolCallResponse> StepContentToolCallResponses { get; set; }

    public virtual DbSet<StepContentType> StepContentTypes { get; set; }

    public virtual DbSet<Tokenizer> Tokenizers { get; set; }

    public virtual DbSet<TransactionType> TransactionTypes { get; set; }

    public virtual DbSet<UsageTransaction> UsageTransactions { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserApiCache> UserApiCaches { get; set; }

    public virtual DbSet<UserApiCacheBody> UserApiCacheBodies { get; set; }

    public virtual DbSet<UserApiCacheUsage> UserApiCacheUsages { get; set; }

    public virtual DbSet<UserApiKey> UserApiKeys { get; set; }

    public virtual DbSet<UserApiUsage> UserApiUsages { get; set; }

    public virtual DbSet<UserBalance> UserBalances { get; set; }

    public virtual DbSet<UserInitialConfig> UserInitialConfigs { get; set; }

    public virtual DbSet<UserMcp> UserMcps { get; set; }

    public virtual DbSet<UserModel> UserModels { get; set; }

    public virtual DbSet<UserModelUsage> UserModelUsages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("SQL_Latin1_General_CP1_CI_AS");

        modelBuilder.Entity<BalanceTransaction>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_BalanceLog2");

            entity.HasOne(d => d.CreditUser).WithMany(p => p.BalanceTransactionCreditUsers)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BalanceTransaction_CreditUserId");

            entity.HasOne(d => d.TransactionType).WithMany(p => p.BalanceTransactions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_BalanceLog2_BalanceLogType");

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
            entity.HasOne(d => d.ImageSize).WithMany(p => p.ChatConfigs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatConfig_ImageSize");

            entity.HasOne(d => d.Model).WithMany(p => p.ChatConfigs)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatConfig_Model");
        });

        modelBuilder.Entity<ChatConfigArchived>(entity =>
        {
            entity.Property(e => e.ChatConfigId).ValueGeneratedNever();

            entity.HasOne(d => d.ChatConfig).WithOne(p => p.ChatConfigArchived)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatConfigArchived_ChatConfig");
        });

        modelBuilder.Entity<ChatConfigMcp>(entity =>
        {
            entity.HasOne(d => d.ChatConfig).WithMany(p => p.ChatConfigMcps).HasConstraintName("FK_ChatConfigMcp_ChatConfig");

            entity.HasOne(d => d.McpServer).WithMany(p => p.ChatConfigMcps).HasConstraintName("FK_ChatConfigMcp_McpServer");
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

            entity.HasOne(d => d.ChatConfig).WithMany(p => p.ChatTurns).HasConstraintName("FK_ChatTurn_ChatConfig");

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

        modelBuilder.Entity<CurrencyRate>(entity =>
        {
            entity.Property(e => e.Code).IsFixedLength();
        });

        modelBuilder.Entity<File>(entity =>
        {
            entity.HasOne(d => d.ClientInfo).WithMany(p => p.Files)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_File_ClientInfo");

            entity.HasOne(d => d.CreateUser).WithMany(p => p.Files)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_File_User");

            entity.HasOne(d => d.FileContentType).WithMany(p => p.Files)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_File_FileContentType");

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

            entity.HasOne(d => d.FileServiceType).WithMany(p => p.FileServices)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_FileService_FileServiceType");
        });

        modelBuilder.Entity<InvitationCode>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("InvitationCode2_pkey");
        });

        modelBuilder.Entity<KnownImageSize>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<LoginService>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_LoginServices2");
        });

        modelBuilder.Entity<McpServer>(entity =>
        {
            entity.HasIndex(e => e.OwnerUserId, "IX_McpServer_OwnerUserId").HasFilter("([OwnerUserId] IS NOT NULL)");

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
            entity.HasOne(d => d.ModelKey).WithMany(p => p.Models)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Model_ModelKey2");

            entity.HasOne(d => d.ModelReference).WithMany(p => p.Models)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Model_ModelReference");
        });

        modelBuilder.Entity<ModelKey>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ModelKey2");

            entity.HasOne(d => d.ModelProvider).WithMany(p => p.ModelKeys)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ModelKey2_ModelProvider");
        });

        modelBuilder.Entity<ModelProvider>(entity =>
        {
            entity.ToTable("ModelProvider", tb => tb.HasComment("JSON"));

            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<ModelReference>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_ModelSetting");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CurrencyCode).IsFixedLength();

            entity.HasOne(d => d.CurrencyCodeNavigation).WithMany(p => p.ModelReferences)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ModelReference_CurrencyRate");

            entity.HasOne(d => d.Provider).WithMany(p => p.ModelReferences)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ModelSetting_ModelProvider");

            entity.HasOne(d => d.ReasoningResponseKind).WithMany(p => p.ModelReferences)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ModelReference_ReasoningResponseKind");

            entity.HasOne(d => d.Tokenizer).WithMany(p => p.ModelReferences).HasConstraintName("FK_ModelReference_Tokenizer");
        });

        modelBuilder.Entity<Prompt>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_Prompt2");

            entity.HasOne(d => d.CreateUser).WithMany(p => p.Prompts)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Prompt_CreateUserId");
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
            entity.HasOne(d => d.ChatRole).WithMany(p => p.Steps)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Step_ChatRole");

            entity.HasOne(d => d.Turn).WithMany(p => p.Steps).HasConstraintName("FK_Step_Turn");

            entity.HasOne(d => d.Usage).WithMany(p => p.Steps).HasConstraintName("FK_Step_Usage");
        });

        modelBuilder.Entity<StepContent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_MessageContent2");

            entity.HasOne(d => d.ContentType).WithMany(p => p.StepContents)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MessageContent2_MessageContentType");

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

        modelBuilder.Entity<StepContentType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__MessageC__3214EC07D7BA864A");
        });

        modelBuilder.Entity<Tokenizer>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<TransactionType>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_BalanceLogType");
        });

        modelBuilder.Entity<UsageTransaction>(entity =>
        {
            entity.HasOne(d => d.CreditUser).WithMany(p => p.UsageTransactions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UsageTransaction_User");

            entity.HasOne(d => d.Model).WithMany(p => p.UsageTransactions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UsageTransaction_Model");

            entity.HasOne(d => d.TransactionType).WithMany(p => p.UsageTransactions)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UsageTransaction_TransactionType");
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

            entity.HasOne(d => d.Model).WithMany(p => p.UserApiCaches)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserApiCache_ModelId");

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
                        .OnDelete(DeleteBehavior.ClientSetNull)
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

            entity.HasOne(d => d.Model).WithMany(p => p.UserModels)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserModel2_Model");

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

            entity.HasOne(d => d.Model).WithMany(p => p.UserModelUsages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserModelUsage_Model");

            entity.HasOne(d => d.UsageTransaction).WithOne(p => p.UserModelUsage).HasConstraintName("FK_ModelUsage_UsageTransactionLog");

            entity.HasOne(d => d.User).WithMany(p => p.UserModelUsages)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserModelUsage_User");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
