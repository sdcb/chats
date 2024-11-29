﻿using Chats.BE.DB.Enums;
using Chats.BE.Services;
using Chats.BE.Services.Sessions;

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
        JwtKeyManager jwtKeyManager = scope.ServiceProvider.GetRequiredService<JwtKeyManager>();
        await jwtKeyManager.GetOrCreateSecretKey(cancellationToken);

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
            Account = "admin",
            Username = "admin",
            CreatedAt = DateTime.UtcNow,
            Password = scope.ServiceProvider.GetRequiredService<PasswordHasher>().HashPassword("please reset your password"),
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
            Content = "You are {{MODEL_NAME}}, please follow user instructions carefully and respond thoughtfully. Current date: {{CURRENT_DATE}}.",
            IsDefault = true,
            IsSystem = true,
            Name = "Default Prompt",
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}