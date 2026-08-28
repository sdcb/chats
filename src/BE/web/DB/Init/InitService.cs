using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Services;
using Chats.BE.Services.Configs;
using Chats.BE.Services.Models.ChatServices.Test;
using System.Text.Json;

namespace Chats.BE.DB.Init;

public class InitService(IServiceScopeFactory scopeFactory)
{
    public const string DefaultPrompt = "You are an AI assistant named Sdcb Chats. Please follow user instructions carefully and respond accordingly.";

    public async Task Init(CancellationToken cancellationToken = default)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        using ChatsDB db = scope.ServiceProvider.GetRequiredService<ChatsDB>();

        if (await db.Database.EnsureCreatedAsync(cancellationToken))
        {
            Console.WriteLine("Database created, inserting initial data...");
            await InsertInitialData(scope, db, cancellationToken);
            Console.WriteLine("Initial data inserted.");
        }
    }

    private static async Task InsertInitialData(IServiceScope scope, ChatsDB db, CancellationToken cancellationToken)
    {
        BasicData.InsertAll(db);
        await db.SaveChangesAsync(cancellationToken);

        DateTime now = DateTime.UtcNow;

        // Add default Linux container runtime node for Docker
        ContainerRuntimeNode runtimeNode = new()
        {
            Name = "default-docker",
            AiName = "linux",
            Description = "Default Linux Docker runtime",
            BackendType = 1,
            // Null lets CodePodConfig select npipe (Windows) or unix socket (Linux/macOS).
            Endpoint = null,
            Credential = null,
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.ContainerRuntimeNodes.Add(runtimeNode);
        await db.SaveChangesAsync(cancellationToken);

        // Add default container image for code interpreter
        db.ContainerImages.Add(new ContainerImage
        {
            Image = "code-interpreter:latest",
            Description = "Pre-installed with common packages, suitable for most daily tasks",
            IsEnabled = true,
        });
        // Add default container resource template for code interpreter
        db.ContainerResourceTemplates.Add(new ContainerResourceTemplate
        {
            Name = "default-code-interpreter",
            RuntimeNodeId = runtimeNode.Id,
            Image = "code-interpreter:latest",
            CpuCores = 2.0f,
            MemoryBytes = 2_147_483_648,
            MaxProcesses = 200,
            BackendNetworkName = "bridge",
            DefaultVolumeBytes = null,
            Visibility = 3,
            CreatedAt = now,
            UpdatedAt = now,
        });
        // Add global user container quota which applies to all users by default (UserId = null)
        db.UserContainerQuota.Add(new UserContainerQuotum
        {
            UserId = null,
            AllowCustomImage = false,
            AllowedNetworkModes = "none,bridge",
            UpdatedAt = now,
        });
        await db.SaveChangesAsync(cancellationToken);

        ModelKey modelKey = new()
        {
            UpdatedAt = now,
            CreatedAt = now,
            CurrentSnapshot = new ModelKeySnapshot
            {
                ModelProviderId = (short)DBModelProvider.Test,
                Name = "Hello-World Key",
                CreatedAt = now,
            },
        };
        db.ModelKeys.Add(modelKey);
        await db.SaveChangesAsync(cancellationToken);

        modelKey.CurrentSnapshot.ModelKeyId = modelKey.Id;

        Model model = new()
        {
            Enabled = true,
            UpdatedAt = now,
            CreatedAt = now,
            CurrentSnapshot = new ModelSnapshot
            {
                ModelId = 0,
                Name = "Hello-World Model",
                DeploymentName = Test2ChatService.ModelName,
                ModelKeyId = modelKey.Id,
                ModelKeySnapshotId = modelKey.CurrentSnapshotId,
                ModelKeySnapshot = modelKey.CurrentSnapshot,
                ApiTypeId = (byte)DBApiType.OpenAIChatCompletion,
                AllowStreaming = true,
                ContextWindow = 64000,
                MaxResponseTokens = 16000,
                CreatedAt = now,
            },
        };
        db.Models.Add(model);
        await db.SaveChangesAsync(cancellationToken);

        model.CurrentSnapshot.ModelId = model.Id;
        await db.SaveChangesAsync(cancellationToken);

        User adminUser = new()
        {
            UserName = "chats",
            DisplayName = "chats",
            CreatedAt = now,
            PasswordHash = scope.ServiceProvider.GetRequiredService<PasswordHasher>().HashPassword("RESET!!!"),
            Enabled = true,
            ApiKeyEnabled = true,
            Role = "admin",
            UpdatedAt = now,
            UserModels =
            [
                new UserModel
                {
                    ModelId = model.Id,
                    CreatedAt = now,
                    UpdatedAt = now,
                    ExpiresAt = now.AddYears(10),
                }
            ],
            UserBalance = new UserBalance
            {
                Balance = 100,
                CreatedAt = now,
                UpdatedAt = now,
            },
        };
        BalanceTransaction balanceTransaction = new()
        {
            Amount = 100,
            CreatedAt = now,
            TransactionTypeId = (byte)DBTransactionType.Initial,
            User = adminUser,
            CreditUser = adminUser,
        };
        db.Users.Add(adminUser);
        db.BalanceTransactions.Add(balanceTransaction);
        db.Prompts.Add(new Prompt
        {
            CreateUser = adminUser,
            UpdatedAt = now,
            CreatedAt = now,
            Content = DefaultPrompt,
            IsDefault = true,
            IsSystem = true,
            Name = "Default Prompt",
        });
        db.FileServices.Add(new()
        {
            Configs = "./AppData/Files",
            FileServiceTypeId = (byte)DBFileServiceType.Local,
            IsDefault = true,
            Name = "Local Files",
            CreatedAt = now,
            UpdatedAt = now,
        });
        db.Configs.Add(new()
        {
            Key = DBConfigKey.SiteInfo,
            Value = JsonSerializer.Serialize(new SiteInfo
            {
                CustomizedLine1 = "Default UserName/Password(PLEASE RESET ASAP): chats/RESET!!!",
                CustomizedLine2 = "Text here can be customized in Admin -> Global Config -> siteInfo",
            })
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
