using Chats.BE.Controllers.Chats.Prompts;
using Chats.BE.Controllers.Chats.Prompts.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Sessions;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Chats.BE.UnitTest.Controllers.Chats;

public class PromptsControllerTests
{
    [Fact]
    public async Task GetPrompts_ReturnsOwnAndSystemPromptsOnly()
    {
        await using ChatsDB db = CreateDb();
        SeedPrompts(db);
        PromptsController controller = CreateController(db, userId: 1, role: "user");

        ActionResult<PromptDto[]> result = await controller.GetPrompts(CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        PromptDto[] prompts = Assert.IsType<PromptDto[]>(ok.Value);
        Assert.Equal([1, 3, 4], prompts.Select(x => x.Id).Order().ToArray());
    }

    [Fact]
    public async Task GetBriefPrompts_ReturnsOwnAndSystemPromptsOnly()
    {
        await using ChatsDB db = CreateDb();
        SeedPrompts(db);
        PromptsController controller = CreateController(db, userId: 1, role: "user");

        ActionResult<BriefPromptDto[]> result = await controller.GetBriefPrompts(CancellationToken.None);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result.Result);
        BriefPromptDto[] prompts = Assert.IsType<BriefPromptDto[]>(ok.Value);
        Assert.Equal([1, 3, 4], prompts.Select(x => x.Id).Order().ToArray());
    }

    [Fact]
    public async Task NormalUser_CannotUpdateOrDeleteOwnedSystemPrompt()
    {
        await using ChatsDB db = CreateDb();
        db.Prompts.Add(CreatePrompt(id: 1, createUserId: 1, isSystem: true));
        await db.SaveChangesAsync(CancellationToken.None);
        PromptsController controller = CreateController(db, userId: 1, role: "user");
        CreatePromptDto request = new()
        {
            Name = "Changed",
            Content = "Changed",
            IsDefault = false,
            IsSystem = true,
            Temperature = null,
        };

        ActionResult<PromptDto> update = await controller.UpdatePrompt(1, request, CancellationToken.None);
        ActionResult delete = await controller.DeletePrompt(1, CancellationToken.None);

        Assert.IsType<NotFoundResult>(update.Result);
        Assert.IsType<NotFoundResult>(delete);
        Prompt prompt = await db.Prompts.SingleAsync(CancellationToken.None);
        Assert.Equal("Prompt 1", prompt.Name);
    }

    [Fact]
    public async Task Admin_CanUpdateAndDeleteSystemPrompt()
    {
        await using ChatsDB db = CreateDb();
        db.Prompts.Add(CreatePrompt(id: 1, createUserId: 2, isSystem: true));
        await db.SaveChangesAsync(CancellationToken.None);
        PromptsController controller = CreateController(db, userId: 1, role: "admin");
        CreatePromptDto request = new()
        {
            Name = "Changed",
            Content = "Changed",
            IsDefault = false,
            IsSystem = true,
            Temperature = null,
        };

        ActionResult<PromptDto> update = await controller.UpdatePrompt(1, request, CancellationToken.None);
        ActionResult delete = await controller.DeletePrompt(1, CancellationToken.None);

        Assert.IsType<OkObjectResult>(update.Result);
        Assert.IsType<NoContentResult>(delete);
        Assert.Empty(db.Prompts);
    }

    private static ChatsDB CreateDb()
    {
        DbContextOptions<ChatsDB> options = new DbContextOptionsBuilder<ChatsDB>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ChatsDB(options);
    }

    private static PromptsController CreateController(ChatsDB db, int userId, string role)
    {
        DefaultHttpContext httpContext = new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(JwtPropertyKeys.UserId, userId.ToString()),
                new Claim(JwtPropertyKeys.UserName, $"user-{userId}"),
                new Claim(JwtPropertyKeys.Role, role),
            ], "Test")),
        };
        HttpContextAccessor accessor = new() { HttpContext = httpContext };
        CurrentUser currentUser = new(accessor, new NoOpUrlEncryptionService());
        return new PromptsController(db, currentUser)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };
    }

    private static void SeedPrompts(ChatsDB db)
    {
        db.Prompts.AddRange(
            CreatePrompt(id: 1, createUserId: 1, isSystem: false),
            CreatePrompt(id: 2, createUserId: 2, isSystem: false),
            CreatePrompt(id: 3, createUserId: 2, isSystem: true),
            CreatePrompt(id: 4, createUserId: 3, isSystem: true));
        db.SaveChanges();
    }

    private static Prompt CreatePrompt(int id, int createUserId, bool isSystem)
    {
        DateTime now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        return new Prompt
        {
            Id = id,
            Name = $"Prompt {id}",
            Content = $"Content {id}",
            IsDefault = false,
            IsSystem = isSystem,
            CreatedAt = now,
            UpdatedAt = now.AddMinutes(id),
            CreateUserId = createUserId,
        };
    }
}
