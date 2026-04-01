using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Services;
using Chats.BE.Services.Configs;
using Chats.BE.Services.Models.ChatServices.Test;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using System.Text.Json;

namespace Chats.BE.DB.Init;

public class InitService(IServiceScopeFactory scopeFactory)
{
    public const string DefaultPrompt = "You are an AI assistant named Sdcb Chats. Please follow user instructions carefully and respond accordingly. Current date: {{CURRENT_DATE}}";

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

        await EnsureBackwardCompatibleSchemaAsync(db, cancellationToken);
    }

    private static async Task EnsureBackwardCompatibleSchemaAsync(ChatsDB db, CancellationToken cancellationToken)
    {
        if (!db.Database.IsSqlite())
        {
            return;
        }

        DbConnection connection = db.Database.GetDbConnection();
        bool shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            bool hasSourceId = false;
            await using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA table_info(\"UserModelUsage\");";
                await using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    if (string.Equals(reader.GetString(1), "SourceId", StringComparison.OrdinalIgnoreCase))
                    {
                        hasSourceId = true;
                        break;
                    }
                }
            }

            if (!hasSourceId)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "ALTER TABLE \"UserModelUsage\" ADD COLUMN \"SourceId\" INTEGER NOT NULL DEFAULT 0;",
                    cancellationToken);
                Console.WriteLine("Applied SQLite compatibility schema update: added UserModelUsage.SourceId.");
            }

            // Backfill historical rows that were defaulted to 0 before SourceId existed.
            int apiFromApiUsage = await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"UserModelUsage\" SET \"SourceId\" = 2 WHERE \"SourceId\" = 0 AND EXISTS (SELECT 1 FROM \"UserApiUsage\" uau WHERE uau.\"UsageId\" = \"UserModelUsage\".\"Id\");",
                cancellationToken);

            int summaryFromTransactions = await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"UserModelUsage\" SET \"SourceId\" = 3 WHERE \"SourceId\" = 0 AND (EXISTS (SELECT 1 FROM \"BalanceTransaction\" bt WHERE bt.\"Id\" = \"UserModelUsage\".\"BalanceTransactionId\" AND bt.\"TransactionTypeId\" = 5) OR EXISTS (SELECT 1 FROM \"UsageTransaction\" ut WHERE ut.\"Id\" = \"UserModelUsage\".\"UsageTransactionId\" AND ut.\"TransactionTypeId\" = 5));",
                cancellationToken);

            int apiFromTransactions = await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"UserModelUsage\" SET \"SourceId\" = 2 WHERE \"SourceId\" = 0 AND (EXISTS (SELECT 1 FROM \"BalanceTransaction\" bt WHERE bt.\"Id\" = \"UserModelUsage\".\"BalanceTransactionId\" AND bt.\"TransactionTypeId\" = 4) OR EXISTS (SELECT 1 FROM \"UsageTransaction\" ut WHERE ut.\"Id\" = \"UserModelUsage\".\"UsageTransactionId\" AND ut.\"TransactionTypeId\" = 4));",
                cancellationToken);

            int webChatDefaulted = await db.Database.ExecuteSqlRawAsync(
                "UPDATE \"UserModelUsage\" SET \"SourceId\" = 1 WHERE \"SourceId\" = 0;",
                cancellationToken);

            int totalBackfilled = apiFromApiUsage + summaryFromTransactions + apiFromTransactions + webChatDefaulted;
            if (totalBackfilled > 0)
            {
                Console.WriteLine($"Backfilled UserModelUsage.SourceId for {totalBackfilled} historical records.");
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task InsertInitialData(IServiceScope scope, ChatsDB db, CancellationToken cancellationToken)
    {
        BasicData.InsertAll(db);
        await db.SaveChangesAsync(cancellationToken);

        Model model = new()
        {
            Name = "Hello-World Model",
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            DeploymentName = Test2ChatService.ModelName,
            AllowStreaming = true, 
            ContextWindow = 64000,
            MaxResponseTokens = 16000,
        };
        ModelKey modelKey = new()
        {
            ModelProviderId = (byte)DBModelProvider.Test,
            Name = "Hello-World Key",
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            Models = [model],
        };
        db.ModelKeys.Add(modelKey);
        await db.SaveChangesAsync(cancellationToken);

        User adminUser = new()
        {
            UserName = "chats",
            DisplayName = "chats",
            CreatedAt = DateTime.UtcNow,
            PasswordHash = scope.ServiceProvider.GetRequiredService<PasswordHasher>().HashPassword("RESET!!!"),
            Enabled = true,
            Role = "admin",
            UpdatedAt = DateTime.UtcNow,
            UserModels =
            [
                new UserModel
                {
                    ModelId = model.Id,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddYears(10),
                }
            ],
            UserBalance = new UserBalance
            {
                Balance = 100,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
        };
        BalanceTransaction balanceTransaction = new()
        {
            Amount = 100,
            CreatedAt = DateTime.UtcNow,
            TransactionTypeId = (byte)DBTransactionType.Initial,
            User = adminUser,
            CreditUser = adminUser,
        };
        db.Users.Add(adminUser);
        db.BalanceTransactions.Add(balanceTransaction);
        db.Prompts.Add(new Prompt
        {
            CreateUser = adminUser,
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        db.Configs.Add(new()
        {
            Key = DBConfigKey.SiteInfo,
            Value = JsonSerializer.Serialize(new SiteInfo()
            {
                CustomizedLine1 = "Default UserName/Password(PLEASE RESET ASAP): chats/RESET!!!",
                CustomizedLine2 = "Text here can be customized in Admin -> Global Config -> siteInfo",
            })
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
