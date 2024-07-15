﻿using Chats.BE.DB;
using Chats.BE.Services.Common;
using Chats.BE.Services.Keycloak;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Services;

public class UserManager(ChatsDB db)
{
    public async Task<User?> FindUserBySub(string sub, CancellationToken cancellationToken)
    {
        return await db.Users.FirstOrDefaultAsync(x => x.Sub == sub, cancellationToken);
    }

    public async Task<User> EnsureKeycloakUser(AccessTokenInfo token, CancellationToken cancellationToken)
    {
        User? user = await FindUserBySub(token.Sub, cancellationToken);
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(), 
                Provider = KnownLoginProviders.Keycloak,
                Sub = token.Sub,
                Account = token.GetSuggestedUserName(),
                Username = token.GetSuggestedUserName(),
                Password = null,
                Role = "-",
                Email = token.Email,
                Enabled = true, 
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            await InitializeUserWithoutSave(user, KnownLoginProviders.Keycloak, null, cancellationToken);
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
        }

        return user;
    }

    public async Task InitializeUserWithoutSave(User newUser, string provider, string? invitationCode, CancellationToken cancellationToken)
    {
        newUser.UserBalance = new()
        {
            Id = Guid.NewGuid(),
            UserId = newUser.Id,
            Balance = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        newUser.UserModel = new()
        {
            Id = Guid.NewGuid(),
            UserId = newUser.Id,
            Models = "[]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        UserInitialConfig? config = await db.UserInitialConfigs
            .OrderByDescending(x =>
                x.LoginType == provider ? 10 : 1 +
                x.InvitationCode!.Value == invitationCode ? 10 : 1)
            .FirstOrDefaultAsync(cancellationToken);

        if (provider == KnownLoginProviders.Phone && config == null)
        {
            // we don't allow phone login without invitation code
            throw new InvalidOperationException("Phone login without invitation code is not allowed");
        }

        if (config != null)
        {
            newUser.UserBalance.Balance = config.Price;
            newUser.UserModel.Models = config.Models; // See JsonUserModel
            db.BalanceLogs.Add(new BalanceLog
            {
                Id = Guid.NewGuid(),
                UserId = newUser.Id,
                CreateUserId = newUser.Id,
                Type = (int)BalanceLogType.Initial,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Value = config.Price,
            });
        }
    }
}