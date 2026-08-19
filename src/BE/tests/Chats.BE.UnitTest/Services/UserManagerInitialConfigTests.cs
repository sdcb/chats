using System.Text.Json;
using Chats.BE.DB.Jsons;
using Chats.BE.Services;
using Chats.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Chats.BE.UnitTest.Services;

public sealed class UserManagerInitialConfigTests
{
    [Fact]
    public void BuildInitialConfigQuery_UsesSpecificityThenStableOrdering()
    {
        DateTime now = DateTime.UtcNow;
        InvitationCode invitation = new() { Id = 10, Value = "invite" };
        UserInitialConfig[] configs =
        [
            CreateConfig(1, "-", null, now.AddMinutes(4)),
            CreateConfig(2, "Phone", null, now.AddMinutes(3)),
            CreateConfig(3, "-", invitation, now.AddMinutes(2)),
            CreateConfig(4, "Phone", invitation, now.AddMinutes(1)),
            CreateConfig(5, "Keycloak", invitation, now.AddMinutes(5)),
        ];

        UserInitialConfig? selected = UserManager
            .BuildInitialConfigQuery(configs.AsQueryable(), "Phone", "invite")
            .FirstOrDefault();

        Assert.NotNull(selected);
        Assert.Equal(4, selected.Id);
    }

    [Fact]
    public void BuildInitialConfigQuery_ExcludesMismatchedConditions()
    {
        InvitationCode otherInvitation = new() { Id = 11, Value = "other" };
        UserInitialConfig[] configs =
        [
            CreateConfig(1, "Keycloak", null, DateTime.UtcNow),
            CreateConfig(2, "-", otherInvitation, DateTime.UtcNow),
            CreateConfig(3, "-", null, DateTime.UtcNow),
        ];

        UserInitialConfig? selected = UserManager
            .BuildInitialConfigQuery(configs.AsQueryable(), "Phone", "invite")
            .FirstOrDefault();

        Assert.NotNull(selected);
        Assert.Equal(3, selected.Id);
    }

    [Fact]
    public async Task InitializeUserWithoutSave_AppliesApiKeyAndAvailableMcps()
    {
        await using ChatsDB db = CreateDb();
        McpServer server = new()
        {
            Id = 7,
            Name = "test",
            Url = "https://example.com/mcp",
            OwnerUserId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.McpServers.Add(server);
        db.UserInitialConfigs.Add(CreateConfig(
            1,
            "-",
            null,
            DateTime.UtcNow,
            apiKeyEnabled: false,
            mcps:
            [
                new JsonInitialMcp
                {
                    McpServerId = 7,
                    ShowShortcut = true,
                    CustomHeaders = " {\"Authorization\":\"test\"} ",
                },
                new JsonInitialMcp
                {
                    McpServerId = 999,
                    ShowShortcut = false,
                    CustomHeaders = null,
                },
            ]));
        await db.SaveChangesAsync();

        User user = new();
        UserManager manager = new(db, NullLogger<UserManager>.Instance);
        await manager.InitializeUserWithoutSave(user, null, null, null, CancellationToken.None);

        Assert.False(user.ApiKeyEnabled);
        UserMcp assignment = Assert.Single(user.UserMcps);
        Assert.Equal(7, assignment.McpServerId);
        Assert.True(assignment.ShowShortcut);
        Assert.Equal("{\"Authorization\":\"test\"}", assignment.CustomHeaders);
    }

    private static ChatsDB CreateDb()
    {
        DbContextOptions<ChatsDB> options = new DbContextOptionsBuilder<ChatsDB>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ChatsDB(options);
    }

    private static UserInitialConfig CreateConfig(
        int id,
        string? loginType,
        InvitationCode? invitationCode,
        DateTime updatedAt,
        bool apiKeyEnabled = true,
        JsonInitialMcp[]? mcps = null)
    {
        return new UserInitialConfig
        {
            Id = id,
            Name = $"config-{id}",
            LoginType = loginType,
            Price = 0,
            Models = "[]",
            Mcps = JsonSerializer.Serialize(mcps ?? []),
            ApiKeyEnabled = apiKeyEnabled,
            InvitationCodeId = invitationCode?.Id,
            InvitationCode = invitationCode,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
        };
    }
}
