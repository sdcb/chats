using Chats.BE.Controllers.Admin.AdminUser.Dtos;
using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Controllers.Common.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Common;
using Chats.BE.Services;
using Chats.DB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using System.Linq.Expressions;

namespace Chats.BE.Controllers.Admin.AdminUser;

[Route("api/admin/users"), AuthorizeAdmin]
public class AdminUserController(ChatsDB db, CurrentUser adminUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> GetUsers([FromQuery] AdminUserQuery pagingRequest, CancellationToken cancellationToken)
    {
        IQueryable<User> query = BuildQuery(pagingRequest);

        return await PagedResult.FromTempQuery(
            query.Select(BuildProjection()),
            pagingRequest,
            x => x.ToDto(),
            cancellationToken);
    }

    [HttpGet("excel")]
    public ActionResult ExportExcel([FromQuery] AdminUserExportQuery req)
    {
        IQueryable<AdminUserDtoTemp> rows = BuildQuery(req)
            .Select(BuildProjection());

        List<string>? selectedColumns = ParseColumns(req.Columns);
        List<Dictionary<string, object?>> exportRows = rows
            .AsEnumerable()
            .Select(row => BuildExportRow(row, selectedColumns))
            .ToList();

        MemoryStream stream = new();
        MiniExcel.SaveAs(stream, exportRows);
        stream.Position = 0;
        return File(
            stream,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"users-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx");
    }

    [HttpPut]
    public async Task<IActionResult> UpdateUser([FromBody] UpdateUserDto dto, [FromServices] PasswordHasher passwordHasher, CancellationToken cancellationToken)
    {
        User? user = await db.Users.FindAsync([dto.UserId], cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        dto.ApplyToUser(user, passwordHasher);
        if (db.ChangeTracker.HasChanges())
        {
            user.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserDto dto, [FromServices] PasswordHasher passwordHasher, [FromServices] UserManager userManager, CancellationToken cancellationToken)
    {
        User? existingUser = await db.Users.FirstOrDefaultAsync(x => x.UserName == dto.UserName, cancellationToken);
        if (existingUser != null)
        {
            return BadRequest("User existed");
        }

        User user = new()
        {
            UserName = dto.UserName,
            DisplayName = dto.UserName,
            Email = dto.Email,
            Phone = dto.Phone,
            Role = dto.Role,
            Avatar = dto.Avatar,
            Enabled = dto.Enabled ?? false,
            Provider = null,
            PasswordHash = passwordHasher.HashPassword(dto.Password),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await userManager.InitializeUserWithoutSave(user, null, null, adminUser.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Created(default(string), value: user.Id);
    }

    private IQueryable<User> BuildQuery(AdminUserQuery query)
        => BuildQuery(query.Id, query.Username, query.Phone, query.Email, query.LoginType);

    private IQueryable<User> BuildQuery(AdminUserExportQuery query)
        => BuildQuery(query.Id, query.Username, query.Phone, query.Email, query.LoginType);

    private IQueryable<User> BuildQuery(string? id, string? username, string? phone, string? email, string? loginType)
    {
        IQueryable<User> rows = db.Users
            .OrderByDescending(x => x.UpdatedAt);

        if (!string.IsNullOrWhiteSpace(id))
        {
            if (!int.TryParse(id.Trim(), out int parsedId))
            {
                return rows.Where(_ => false);
            }

            rows = rows.Where(x => x.Id == parsedId);
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            string keyword = username.Trim();
            rows = rows.Where(x =>
                EF.Functions.Like(x.DisplayName, $"%{keyword}%") ||
                EF.Functions.Like(x.UserName, $"%{keyword}%"));
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            string keyword = phone.Trim();
            rows = rows.Where(x => x.Phone != null && EF.Functions.Like(x.Phone, $"%{keyword}%"));
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            string keyword = email.Trim();
            rows = rows.Where(x => x.Email != null && EF.Functions.Like(x.Email, $"%{keyword}%"));
        }

        if (!string.IsNullOrWhiteSpace(loginType))
        {
            string normalized = loginType.Trim().ToLowerInvariant();
            if (normalized == "password")
            {
                rows = rows.Where(x => x.Provider == null || x.Provider == "");
            }
            else if (normalized == "phone")
            {
                rows = rows.Where(x => x.Provider != null && x.Provider.ToLower() == KnownLoginProviders.Phone.ToLower());
            }
            else if (normalized == "keycloak")
            {
                rows = rows.Where(x => x.Provider != null && x.Provider.ToLower() == KnownLoginProviders.Keycloak.ToLower());
            }
        }

        return rows;
    }

    private static Expression<Func<User, AdminUserDtoTemp>> BuildProjection()
    {
        return x => new AdminUserDtoTemp
        {
            Id = x.Id,
            Username = x.DisplayName,
            Account = x.UserName,
            Balance = x.UserBalance!.Balance.ToString(),
            Role = x.Role,
            Avatar = x.Avatar,
            Phone = x.Phone,
            Email = x.Email,
            Provider = x.Provider,
            Enabled = x.Enabled,
            CreatedAt = x.CreatedAt,
            UserModelCount = x.UserModels.Count(),
        };
    }

    private static List<string>? ParseColumns(string? columns)
    {
        if (string.IsNullOrWhiteSpace(columns))
        {
            return null;
        }

        List<string> keys = columns
            .Split('~', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        return keys.Count > 0
            ? keys
            : null;
    }

    private static Dictionary<string, object?> BuildExportRow(AdminUserDtoTemp row, List<string>? selectedColumns)
    {
        Dictionary<string, object?> exportRow = new();

        void AddColumn(string title, object? value)
        {
            exportRow[title] = value;
        }

        IEnumerable<string> columns = selectedColumns ??
            [
                "id",
                "username",
                "role",
                "phone",
                "email",
                "balance",
                "modelCount"
            ];

        foreach (string column in columns)
        {
            switch (column)
            {
                case "id":
                    AddColumn("User Id", row.Id);
                    break;
                case "username":
                    AddColumn("User Name", row.Username);
                    break;
                case "account":
                    AddColumn("Account", row.Account);
                    break;
                case "role":
                    AddColumn("Role", row.Role);
                    break;
                case "phone":
                    AddColumn("Phone", row.Phone);
                    break;
                case "email":
                    AddColumn("E-Mail", row.Email);
                    break;
                case "loginType":
                    AddColumn("Login Type", ResolveLoginTypeLabel(row.Provider));
                    break;
                case "balance":
                    AddColumn("Balance", row.Balance);
                    break;
                case "modelCount":
                    AddColumn("Model Count", row.UserModelCount);
                    break;
            }
        }

        return exportRow;
    }

    private static string ResolveLoginTypeLabel(string? provider)
    {
        if (string.IsNullOrWhiteSpace(provider))
        {
            return "Account password login";
        }

        return provider.Equals(KnownLoginProviders.Phone, StringComparison.OrdinalIgnoreCase)
            ? "Phone"
            : provider.Equals(KnownLoginProviders.Keycloak, StringComparison.OrdinalIgnoreCase)
                ? "Keycloak"
                : provider;
    }
}
