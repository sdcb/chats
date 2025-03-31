using Chats.BE.DB.Enums;
using Chats.BE.Services;
using Chats.BE.Services.Configs;
using Chats.BE.Services.Sessions;
using System.Text.Json;

namespace Chats.BE.DB.Init;

public class InitService(IServiceScopeFactory scopeFactory)
{
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

        Model model = new()
        {
            Name = "Hello-World Model",
            UpdatedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            ModelReferenceId = 0,
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
            Content = "You're an AI intelligent assistant, Sdcb Chats. Please follow user instructions carefully and respond. Current date: {{CURRENT_DATE}}",
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
                CustomizedLine1 = "Default UserName/Password: chats/RESET!!!",
                CustomizedLine2 = "You can change the customized text here in GlobalConfig -> siteInfo",
            })
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
