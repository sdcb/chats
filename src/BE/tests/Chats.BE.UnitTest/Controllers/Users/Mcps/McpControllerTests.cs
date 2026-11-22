using System.Security.Claims;
using System.Text.Json;
using Chats.BE.Controllers.Users.Mcps;
using Chats.BE.Controllers.Users.Mcps.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Sessions;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.UnitTest.Controllers.Users.Mcps;

public sealed class McpControllerTests
{
    private const string OwnerHeaders = "{\"Authorization\":\"Bearer owner-secret\"}";
    private const string ServerUrl = "https://mcp.internal.example/sse?token=secret-url-token";
    private const string UserACustomHeaders = "{\"Authorization\":\"Bearer user-a-token\"}";
    private const string UserBCustomHeaders = "{\"Authorization\":\"Bearer user-b-token\"}";

    [Fact]
    public async Task GetAssignedUserDetails_ReturnsNotFoundForAssignedNonOwner()
    {
        await using ChatsDB db = await CreateSeededDbAsync();
        McpController controller = CreateController(db, userId: 2, role: "-");

        ActionResult<AssignedUserDetailsDto[]> result = await controller.GetAssignedUserDetails(100, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetAssignedUserDetails_ReturnsNotFoundForUnassignedUser()
    {
        await using ChatsDB db = await CreateSeededDbAsync();
        McpController controller = CreateController(db, userId: 4, role: "-");

        ActionResult<AssignedUserDetailsDto[]> result = await controller.GetAssignedUserDetails(100, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task GetAssignedUserDetails_ReturnsAllAssignedHeadersForOwnerAndAdmin()
    {
        await using ChatsDB db = await CreateSeededDbAsync();

        McpController ownerController = CreateController(db, userId: 1, role: "-");
        ActionResult<AssignedUserDetailsDto[]> ownerResult = await ownerController.GetAssignedUserDetails(100, CancellationToken.None);
        OkObjectResult ownerOk = Assert.IsType<OkObjectResult>(ownerResult.Result);
        AssignedUserDetailsDto[] ownerPayload = Assert.IsType<AssignedUserDetailsDto[]>(ownerOk.Value);
        Assert.Equal(2, ownerPayload.Length);
        Assert.Contains(ownerPayload, x => x.Id == 2 && x.CustomHeaders == UserACustomHeaders);
        Assert.Contains(ownerPayload, x => x.Id == 3 && x.CustomHeaders == UserBCustomHeaders);

        McpController adminController = CreateController(db, userId: 99, role: "admin");
        ActionResult<AssignedUserDetailsDto[]> adminResult = await adminController.GetAssignedUserDetails(100, CancellationToken.None);
        OkObjectResult adminOk = Assert.IsType<OkObjectResult>(adminResult.Result);
        AssignedUserDetailsDto[] adminPayload = Assert.IsType<AssignedUserDetailsDto[]>(adminOk.Value);
        Assert.Equal(2, adminPayload.Length);
        Assert.Contains(adminPayload, x => x.Id == 2 && x.CustomHeaders == UserACustomHeaders);
        Assert.Contains(adminPayload, x => x.Id == 3 && x.CustomHeaders == UserBCustomHeaders);
    }

    [Fact]
    public async Task GetMcpServerDetails_ReturnsReadOnlyDtoWithoutUrlOrHeadersForAssignedNonOwner()
    {
        await using ChatsDB db = await CreateSeededDbAsync();
        McpController controller = CreateController(db, userId: 2, role: "-");

        ActionResult<McpServerDetailsDto> result = await controller.GetMcpServerDetails(100, CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        McpServerDetailsDto dto = Assert.IsType<McpServerDetailsDto>(ok.Value, exactMatch: true);
        Assert.False(dto.Editable);
        Assert.Equal("shared_mcp", dto.Name);
        Assert.Equal("Shared MCP", dto.DisplayName);
        Assert.Equal("Use carefully", dto.ServerInstructions);
        Assert.Single(dto.Tools);

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(dto));
        Assert.False(json.RootElement.TryGetProperty("url", out _));
        Assert.False(json.RootElement.TryGetProperty("headers", out _));
        Assert.False(json.RootElement.TryGetProperty("$type", out _));
    }

    [Fact]
    public async Task GetMcpServerDetails_ReturnsEditableDtoWithHeadersAndUrlForOwnerAndAdmin()
    {
        await using ChatsDB db = await CreateSeededDbAsync();

        McpController ownerController = CreateController(db, userId: 1, role: "-");
        ActionResult<McpServerDetailsDto> ownerResult = await ownerController.GetMcpServerDetails(100, CancellationToken.None);
        McpServerDetailsDto ownerValue = Assert.IsAssignableFrom<McpServerDetailsDto>(Assert.IsType<OkObjectResult>(ownerResult.Result).Value);
        EditableMcpServerDetailsDto ownerDto = Assert.IsType<EditableMcpServerDetailsDto>(ownerValue);
        Assert.True(ownerDto.Editable);
        Assert.Equal(OwnerHeaders, ownerDto.Headers);
        Assert.Equal(ServerUrl, ownerDto.Url);

        using JsonDocument ownerJson = JsonDocument.Parse(JsonSerializer.Serialize(ownerValue));
        Assert.Equal(ServerUrl, ownerJson.RootElement.GetProperty("url").GetString());
        Assert.Equal(OwnerHeaders, ownerJson.RootElement.GetProperty("headers").GetString());
        Assert.False(ownerJson.RootElement.TryGetProperty("$type", out _));

        McpController adminController = CreateController(db, userId: 99, role: "admin");
        ActionResult<McpServerDetailsDto> adminResult = await adminController.GetMcpServerDetails(100, CancellationToken.None);
        EditableMcpServerDetailsDto adminDto = Assert.IsType<EditableMcpServerDetailsDto>(Assert.IsType<OkObjectResult>(adminResult.Result).Value);
        Assert.True(adminDto.Editable);
        Assert.Equal(OwnerHeaders, adminDto.Headers);
        Assert.Equal(ServerUrl, adminDto.Url);
    }

    [Fact]
    public async Task ListAllMcpServersForManagement_ReturnsEditableDtoOnlyForEditableServers()
    {
        await using ChatsDB db = await CreateSeededDbAsync();
        db.McpServers.Add(new McpServer
        {
            Id = 200,
            OwnerUserId = 2,
            Name = "user2_mcp",
            DisplayName = "User 2 MCP",
            Url = "https://user2.example/mcp",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        McpController assignedUserController = CreateController(db, userId: 2, role: "-");
        ActionResult<ManagementMcpServerDto[]> userResult = await assignedUserController.ListAllMcpServersForManagement(mineOnly: true, CancellationToken.None);
        ManagementMcpServerDto[] userItems = Assert.IsType<ManagementMcpServerDto[]>(Assert.IsType<OkObjectResult>(userResult.Result).Value);

        EditableManagementMcpServerDto ownServer = Assert.IsType<EditableManagementMcpServerDto>(Assert.Single(userItems, x => x.Id == 200));
        Assert.True(ownServer.Editable);
        Assert.Equal("https://user2.example/mcp", ownServer.Url);

        ManagementMcpServerDto sharedServer = Assert.IsType<ManagementMcpServerDto>(Assert.Single(userItems, x => x.Id == 100), exactMatch: true);
        Assert.False(sharedServer.Editable);

        using JsonDocument itemsJson = JsonDocument.Parse(JsonSerializer.Serialize(userItems));
        JsonElement[] elements = [.. itemsJson.RootElement.EnumerateArray()];
        JsonElement ownJson = Assert.Single(elements, e => e.GetProperty("id").GetInt32() == 200);
        Assert.Equal("https://user2.example/mcp", ownJson.GetProperty("url").GetString());
        Assert.False(ownJson.TryGetProperty("$type", out _));

        JsonElement sharedJson = Assert.Single(elements, e => e.GetProperty("id").GetInt32() == 100);
        Assert.False(sharedJson.TryGetProperty("url", out _));
        Assert.False(sharedJson.TryGetProperty("$type", out _));

        McpController adminController = CreateController(db, userId: 99, role: "admin");
        ActionResult<ManagementMcpServerDto[]> adminResult = await adminController.ListAllMcpServersForManagement(mineOnly: false, CancellationToken.None);
        ManagementMcpServerDto[] adminItems = Assert.IsType<ManagementMcpServerDto[]>(Assert.IsType<OkObjectResult>(adminResult.Result).Value);
        Assert.Equal(ServerUrl, Assert.IsType<EditableManagementMcpServerDto>(Assert.Single(adminItems, x => x.Id == 100)).Url);
        Assert.Equal("https://user2.example/mcp", Assert.IsType<EditableManagementMcpServerDto>(Assert.Single(adminItems, x => x.Id == 200)).Url);
    }

    private static async Task<ChatsDB> CreateSeededDbAsync()
    {
        DbContextOptions<ChatsDB> options = new DbContextOptionsBuilder<ChatsDB>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        ChatsDB db = new(options);

        DateTime now = DateTime.UtcNow;
        db.Users.AddRange(
            CreateUser(1, "owner", "Owner", "-", now),
            CreateUser(2, "usera", "User A", "-", now),
            CreateUser(3, "userb", "User B", "-", now),
            CreateUser(4, "outsider", "Outsider", "-", now),
            CreateUser(99, "admin", "Admin", "admin", now));

        db.McpServers.Add(new McpServer
        {
            Id = 100,
            OwnerUserId = 1,
            Name = "shared_mcp",
            DisplayName = "Shared MCP",
            Url = ServerUrl,
            Headers = OwnerHeaders,
            ServerInstructions = "Use carefully",
            CreatedAt = now,
            UpdatedAt = now,
            McpTools =
            [
                new McpTool
                {
                    Id = 1000,
                    ToolName = "search",
                    Title = "Search",
                    Description = "Search tool",
                }
            ],
            UserMcps =
            [
                new UserMcp
                {
                    UserId = 2,
                    CustomHeaders = UserACustomHeaders,
                    ShowShortcut = true,
                },
                new UserMcp
                {
                    UserId = 3,
                    CustomHeaders = UserBCustomHeaders,
                    ShowShortcut = false,
                }
            ]
        });

        await db.SaveChangesAsync();
        return db;
    }

    private static User CreateUser(int id, string userName, string displayName, string role, DateTime now)
        => new()
        {
            Id = id,
            UserName = userName,
            DisplayName = displayName,
            Role = role,
            Enabled = true,
            CreatedAt = now,
            UpdatedAt = now,
        };

    private static McpController CreateController(ChatsDB db, int userId, string role)
    {
        NoOpUrlEncryptionService encryption = new();
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(JwtPropertyKeys.UserId, encryption.EncryptUserId(userId)),
                new Claim(JwtPropertyKeys.UserName, $"user{userId}"),
                new Claim(JwtPropertyKeys.Role, role),
                new Claim(JwtPropertyKeys.ApiKeyEnabled, "false"),
            ])),
        };

        CurrentUser currentUser = new(new HttpContextAccessor { HttpContext = httpContext }, encryption);
        return new McpController(db, currentUser);
    }
}
