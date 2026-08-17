using Chats.DB;
using Chats.BE.Controllers.Users.Mcps.Dtos;
using Chats.BE.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Client;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Chats.BE.Services;
using Chats.BE.Services.RequestTracing;
using Chats.BE.Services.Mcp;

namespace Chats.BE.Controllers.Users.Mcps;

[Route("api/mcp"), Authorize]
public class McpController(ChatsDB db, CurrentUser currentUser) : ControllerBase
{
    private static bool IsNullOrWhiteSpaceOrJsonObject(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(text);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch
        {
            return false;
        }
    }

    private static string? NormalizeOptionalText(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    [HttpGet]
    public async Task<ActionResult<McpServerListItemDto[]>> ListAllMcpServers(CancellationToken cancellationToken)
    {
        McpServerListItemDto[] data = await db.UserMcps
            .Where(um => um.UserId == currentUser.Id)
            .OrderByDescending(um => um.McpServerId)
            .Select(um => new McpServerListItemDto
            {
                Id = um.McpServer.Id,
                Name = um.McpServer.Name,
                DisplayName = um.McpServer.DisplayName,
                ShowShortcut = um.ShowShortcut,
            })
            .ToArrayAsync(cancellationToken);
        return Ok(data);
    }

    [HttpGet("management")]
    public async Task<ActionResult<ManagementMcpServerDto[]>> ListAllMcpServersForManagement(
        [FromQuery] bool mineOnly = true,
        CancellationToken cancellationToken = default)
    {
        IQueryable<McpServer> query = ApplyManagementScope(
            db.McpServers,
            currentUser.Id,
            currentUser.IsAdmin,
            mineOnly);

        ManagementMcpServerDto[] data = await query
                .OrderBy(x => x.OwnerUserId == currentUser.Id ? 0 : 1) // own first
                .ThenByDescending(x => x.Id)
                .Select(x => new ManagementMcpServerDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    DisplayName = x.DisplayName,
                    Url = x.Url,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    ToolsCount = x.McpTools.Count,
                    Editable = currentUser.IsAdmin || x.OwnerUserId == currentUser.Id, // admin or owner can edit
                    Owner = x.OwnerUser.DisplayName,
                    AssignedUserCount = x.UserMcps.Count,
                    AssignedToMe = x.UserMcps.Any(um => um.UserId == currentUser.Id),
                    ShowShortcut = x.UserMcps
                        .Where(um => um.UserId == currentUser.Id)
                        .Select(um => (bool?)um.ShowShortcut)
                        .FirstOrDefault() ?? false,
                })
                .ToArrayAsync(cancellationToken);
        return Ok(data);
    }

    internal static IQueryable<McpServer> ApplyManagementScope(
        IQueryable<McpServer> query,
        int currentUserId,
        bool isAdmin,
        bool mineOnly)
    {
        if (!isAdmin || mineOnly)
        {
            query = query.Where(x =>
                x.OwnerUserId == currentUserId ||
                x.UserMcps.Any(um => um.UserId == currentUserId));
        }

        return query;
    }

    internal static IQueryable<McpServer> FindNameConflicts(
        IQueryable<McpServer> query,
        int ownerUserId,
        string name,
        int? excludedMcpServerId = null)
    {
        string normalizedName = name.ToUpper();
        return query.Where(x =>
            x.OwnerUserId == ownerUserId &&
            x.Name.ToUpper() == normalizedName &&
            (!excludedMcpServerId.HasValue || x.Id != excludedMcpServerId.Value));
    }

    IQueryable<McpServer> ApplyAdminFilter(IQueryable<McpServer> query)
    {
        if (!currentUser.IsAdmin)
        {
            query = query.Where(x => x.OwnerUserId == currentUser.Id || x.UserMcps.Any(um => um.UserId == currentUser.Id));
        }

        return query;
    }

    [HttpGet("{mcpId:int}")]
    public async Task<ActionResult<McpServerDetailsDto>> GetMcpServerDetails(int mcpId, CancellationToken cancellationToken)
    {
        IQueryable<McpServer> query = db.McpServers.Where(x => x.Id == mcpId);
        query = ApplyAdminFilter(query);

        McpServerDetailsDto? dto = await query
            .Select(x => new McpServerDetailsDto
            {
                Id = x.Id,
                Name = x.Name,
                DisplayName = x.DisplayName,
                Url = x.Url,
                Headers = x.Headers,
                ServerInstructions = x.ServerInstructions,
                Owner = x.OwnerUser.DisplayName,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                ToolsCount = x.McpTools.Count,
                Editable = currentUser.IsAdmin || x.OwnerUserId == currentUser.Id,
                AssignedUserCount = x.UserMcps.Count,
                Tools = x.McpTools
                    .OrderBy(t => t.Id)
                    .Select(t => new McpToolBasicInfo
                    {
                        Name = t.ToolName,
                        Title = t.Title,
                        Description = t.Description,
                        Parameters = t.Parameters,
                        Destructive = t.Destructive,
                        Idempotent = t.Idempotent,
                        OpenWorld = t.OpenWorld,
                        ReadOnly = t.ReadOnly,
                    }).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (dto == null)
        {
            return NotFound();
        }
        return Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<McpServerDetailsDto>> CreateMcpServer([FromBody] UpdateMcpServerRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        string name = request.Name?.Trim() ?? string.Empty;
        if (!McpProtocolName.IsValidServerName(name))
        {
            return BadRequest("Name must match ^[A-Za-z0-9_-]{1,50}$");
        }

        bool nameExists = await FindNameConflicts(db.McpServers, currentUser.Id, name)
            .AnyAsync(cancellationToken);
        if (nameExists)
        {
            return BadRequest("This name is already taken, please choose another one");
        }

        if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
        {
            return BadRequest("Invalid URL");
        }

        if (!request.ValidateToolNameUnique())
        {
            return BadRequest("Tool names must be unique within the server");
        }

        string? toolNameError = McpProtocolName.ValidateTools(name, request.Tools.Select(x => x.Name));
        if (toolNameError is not null)
        {
            return BadRequest(toolNameError);
        }

        // Validate headers: allow null/empty/whitespace; non-empty must be a valid JSON object
        if (!string.IsNullOrWhiteSpace(request.Headers))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(request.Headers);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return BadRequest("Headers must be empty or a valid JSON object");
                }
            }
            catch
            {
                return BadRequest("Headers must be empty or a valid JSON object");
            }
        }

        McpServer server = new()
        {
            Name = name,
            DisplayName = NormalizeOptionalText(request.DisplayName),
            Url = request.Url,
            Headers = string.IsNullOrWhiteSpace(request.Headers) ? null : request.Headers,
            ServerInstructions = NormalizeOptionalText(request.ServerInstructions),
            OwnerUserId = currentUser.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            UserMcps =
            [
                new UserMcp
                {
                    UserId = currentUser.Id, // auto-assign to creator
                    ShowShortcut = false,
                }
            ]
        };

        // Add tools
        foreach (McpToolBasicInfo toolRequest in request.Tools)
        {
            server.McpTools.Add(toolRequest.ToDB());
        }

        db.McpServers.Add(server);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            bool conflict = await FindNameConflicts(db.McpServers, currentUser.Id, name)
                .AnyAsync(cancellationToken);
            if (conflict)
            {
                return BadRequest("This name is already taken, please choose another one");
            }

            throw;
        }

        return await GetMcpServerDetails(server.Id, cancellationToken);
    }

    [HttpPut("{mcpId:int}")]
    public async Task<ActionResult<McpServerDetailsDto>> UpdateMcpServer(int mcpId, [FromBody] UpdateMcpServerRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        string name = request.Name?.Trim() ?? string.Empty;
        if (!McpProtocolName.IsValidServerName(name))
        {
            return BadRequest("Name must match ^[A-Za-z0-9_-]{1,50}$");
        }

        IQueryable<McpServer> finder = db.McpServers
            .Include(x => x.UserMcps)
            .Where(x => x.Id == mcpId);
        if (!currentUser.IsAdmin)
        {
            finder = finder.Where(x => x.OwnerUserId == currentUser.Id); // user can manage own only
        }

        McpServer? server = await finder.Include(x => x.McpTools).FirstOrDefaultAsync(cancellationToken);
        if (server == null)
        {
            return NotFound();
        }

        bool nameExists = await FindNameConflicts(db.McpServers, server.OwnerUserId, name, mcpId)
            .AnyAsync(cancellationToken);
        if (nameExists)
        {
            return BadRequest("This name is already taken, please choose another one");
        }

        bool configNameConflict = await McpServerNameConflictValidator.HasRenameConflictAsync(
            db,
            mcpId,
            name,
            cancellationToken);
        if (configNameConflict)
        {
            return BadRequest(
                $"Renaming this MCP server to '{name}' would create duplicate names in an existing chat configuration.");
        }

        if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
        {
            return BadRequest("Invalid URL");
        }

        if (!request.ValidateToolNameUnique())
        {
            return BadRequest("Tool names must be unique within the server");
        }

        string? toolNameError = McpProtocolName.ValidateTools(name, request.Tools.Select(x => x.Name));
        if (toolNameError is not null)
        {
            return BadRequest(toolNameError);
        }

        // Validate headers: allow null/empty/whitespace; non-empty must be a valid JSON object
        if (!string.IsNullOrWhiteSpace(request.Headers))
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(request.Headers);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return BadRequest("Headers must be empty or a valid JSON object");
                }
            }
            catch
            {
                return BadRequest("Headers must be empty or a valid JSON object");
            }
        }

        server.Name = name;
        server.DisplayName = NormalizeOptionalText(request.DisplayName);
        server.Url = request.Url;
        server.Headers = string.IsNullOrWhiteSpace(request.Headers) ? null : request.Headers;
        server.ServerInstructions = NormalizeOptionalText(request.ServerInstructions);
        if (server.OwnerUserId == currentUser.Id && !server.UserMcps.Any(um => um.UserId == currentUser.Id))
        {
            server.UserMcps.Add(new UserMcp
            {
                UserId = currentUser.Id,
                ShowShortcut = false,
            });
        }

        // Always overwrite tools according to request (even when empty)
        UpdateMcpToolsInMemory(server, request.Tools);

        if (db.ChangeTracker.HasChanges())
        {
            server.UpdatedAt = DateTime.UtcNow;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            bool conflict = await FindNameConflicts(db.McpServers, server.OwnerUserId, name, mcpId)
                .AnyAsync(cancellationToken);
            if (conflict)
            {
                return BadRequest("This name is already taken, please choose another one");
            }

            throw;
        }
        return await GetMcpServerDetails(mcpId, cancellationToken);
    }

    private static void UpdateMcpToolsInMemory(McpServer server, List<McpToolBasicInfo> requestTools)
    {
        // Create lookup dictionaries
        Dictionary<string, McpTool> existingToolsDict = server.McpTools.ToDictionary(t => t.ToolName, t => t);
        Dictionary<string, McpToolBasicInfo> requestToolsDict = requestTools.ToDictionary(t => t.Name, t => t);

        // Find tools to add (exist in request but not in database)
        IEnumerable<McpToolBasicInfo> toolsToAdd = requestTools.Where(rt => !existingToolsDict.ContainsKey(rt.Name));
        foreach (McpToolBasicInfo toolToAdd in toolsToAdd)
        {
            server.McpTools.Add(toolToAdd.ToDB());
        }

        // Find tools to update (exist in both request and database)
        IEnumerable<McpTool> toolsToUpdate = server.McpTools.Where(et => requestToolsDict.ContainsKey(et.ToolName));
        foreach (McpTool? existingTool in toolsToUpdate)
        {
            McpToolBasicInfo requestTool = requestToolsDict[existingTool.ToolName];
            existingTool.Title = requestTool.GetNormalizedTitle();
            existingTool.Description = requestTool.Description;
            existingTool.Parameters = requestTool.Parameters;
            existingTool.Destructive = requestTool.Destructive;
            existingTool.Idempotent = requestTool.Idempotent;
            existingTool.OpenWorld = requestTool.OpenWorld;
            existingTool.ReadOnly = requestTool.ReadOnly;
        }

        // Find tools to remove (exist in database but not in request)
        List<McpTool> toolsToRemove = [.. server.McpTools.Where(et => !requestToolsDict.ContainsKey(et.ToolName))];
        foreach (McpTool? toolToRemove in toolsToRemove)
        {
            server.McpTools.Remove(toolToRemove);
        }
    }

    [HttpDelete("{mcpId:int}")]
    public async Task<ActionResult> DeleteMcpServer(int mcpId, CancellationToken cancellationToken)
    {
        IQueryable<McpServer> finder = db.McpServers.Where(x => x.Id == mcpId);
        if (!currentUser.IsAdmin)
        {
            finder = finder.Where(x => x.OwnerUserId == currentUser.Id); // user can delete own only
        }

        McpServer? server = await finder.Include(x => x.McpTools).FirstOrDefaultAsync(cancellationToken);
        if (server == null)
        {
            return NotFound();
        }

        db.McpServers.Remove(server);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("fetch-tools")]
    public async Task<ActionResult<FetchToolsResponse>> FetchMcpTools(
        [FromBody] FetchToolsRequest req,
        [FromServices] ILogger<McpController> logger,
        [FromServices] IHttpClientFactory httpClientFactory,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(req.ServerUrl, UriKind.Absolute, out Uri? serverUri))
        {
            return BadRequest("Invalid serverUrl");
        }

        Dictionary<string, string>? headerDict = null;
        if (!string.IsNullOrWhiteSpace(req.Headers))
        {
            try
            {
                headerDict = JsonSerializer.Deserialize<Dictionary<string, string>>(req.Headers);
            }
            catch (JsonException)
            {
                return BadRequest("headers must be a JSON object");
            }
        }

        HttpClientTransportOptions options = new()
        {
            Endpoint = serverUri,
        };
        if (headerDict != null)
        {
            options.AdditionalHeaders = headerDict;
        }

        try
        {
            await using HttpClientTransport transport = new(
                options,
                httpClientFactory.CreateClient(HttpClientNames.McpController),
                loggerFactory,
                ownsHttpClient: false);
            await using McpClient client = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            List<McpToolBasicInfo> tools = [];
            ListToolsResult mcpToolsResp = await client.ListToolsAsync(new ListToolsRequestParams(), cancellationToken);
            foreach (Tool tool in mcpToolsResp.Tools)
            {
                tools.Add(MapTool(tool));
            }
            return Ok(new FetchToolsResponse
            {
                Tools = tools,
                ServerInstructions = NormalizeOptionalText(client.ServerInstructions),
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or JsonException or IOException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Failed to fetch MCP tools from {Url}", req.ServerUrl);
            return BadRequest(ex.Message);
        }
    }

    internal static McpToolBasicInfo MapTool(Tool tool)
        => new()
        {
            Name = tool.Name,
            Title = tool.Title ?? tool.Annotations?.Title,
            Description = tool.Description,
            Parameters = JSON.Serialize(tool.InputSchema),
            Destructive = tool.Annotations?.DestructiveHint ?? false,
            Idempotent = tool.Annotations?.IdempotentHint ?? false,
            OpenWorld = tool.Annotations?.OpenWorldHint ?? false,
            ReadOnly = tool.Annotations?.ReadOnlyHint ?? false,
        };

    // 用户分配相关的API端点

    [HttpPost("{mcpId:int}/assign-to-users")]
    public async Task<ActionResult> AssignUsersToMcp(int mcpId, [FromBody] AssignUsersToMcpRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (request.ToAssignedUsers.Count != 0 && !currentUser.IsAdmin)
        {
            return Forbid(); // 只有管理员可以分配新用户
        }

        // 检查MCP服务器是否存在
        McpServer? server = await db.McpServers
            .Include(x => x.UserMcps)
            .FirstOrDefaultAsync(x => x.Id == mcpId, cancellationToken);

        if (server == null)
        {
            return NotFound();
        }

        if (!(currentUser.IsAdmin || server.OwnerUserId == currentUser.Id))
        {
            return Forbid(); // 只有管理员或拥有者可以分配用户
        }

        // 验证所有用户ID是否存在
        HashSet<int> allUserIds = [.. request.ToAssignedUsers.Select(x => x.Id), .. request.ToUpdateUsers.Select(x => x.Id), .. request.ToDeleteUserIds];
        List<int> existingUserIds = await db.Users
            .Where(u => allUserIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        if (existingUserIds.Count != allUserIds.Count)
        {
            List<int> missingUserIds = [.. allUserIds.Except(existingUserIds)];
            return BadRequest($"User IDs not found: {string.Join(", ", missingUserIds)}");
        }

        bool assignerShowShortcut = server.UserMcps
            .FirstOrDefault(um => um.UserId == currentUser.Id)
            ?.ShowShortcut ?? false;

        // 处理新分配的用户
        foreach (AssignedUserInfo userInfo in request.ToAssignedUsers)
        {
            // customHeaders 校验：允许空白或合法 JSON 对象
            if (!IsNullOrWhiteSpaceOrJsonObject(userInfo.CustomHeaders))
            {
                return BadRequest("Headers must be empty or a valid JSON object");
            }

            // 检查是否已经分配过
            if (server.UserMcps.Any(um => um.UserId == userInfo.Id))
            {
                return BadRequest($"User {userInfo.Id} is already assigned to this MCP server");
            }

            server.UserMcps.Add(new UserMcp
            {
                UserId = userInfo.Id,
                CustomHeaders = userInfo.CustomHeaders,
                ShowShortcut = userInfo.ShowShortcut ?? assignerShowShortcut,
                McpServerId = mcpId
            });
        }

        // 处理更新的用户
        foreach (AssignedUserInfo userInfo in request.ToUpdateUsers)
        {
            // customHeaders 校验：允许空白或合法 JSON 对象
            if (!IsNullOrWhiteSpaceOrJsonObject(userInfo.CustomHeaders))
            {
                return BadRequest("Headers must be empty or a valid JSON object");
            }
            UserMcp? existingAssignment = server.UserMcps.FirstOrDefault(um => um.UserId == userInfo.Id);
            if (existingAssignment == null)
            {
                return BadRequest($"User {userInfo.Id} is not assigned to this MCP server");
            }

            existingAssignment.CustomHeaders = userInfo.CustomHeaders;
            if (userInfo.ShowShortcut.HasValue)
            {
                existingAssignment.ShowShortcut = userInfo.ShowShortcut.Value;
            }
        }

        // 处理删除的用户
        foreach (int userId in request.ToDeleteUserIds)
        {
            UserMcp? existingAssignment = server.UserMcps.FirstOrDefault(um => um.UserId == userId);
            if (existingAssignment == null)
            {
                return BadRequest($"User {userId} is not assigned to this MCP server");
            }

            server.UserMcps.Remove(existingAssignment);
        }

        if (db.ChangeTracker.HasChanges())
        {
            server.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok();
    }

    [HttpGet("{mcpId:int}/get-unassigned-users")]
    public async Task<ActionResult<UnassignedUserDto[]>> GetUnassignedUsers(
        int mcpId,
        [FromQuery] string? search = null,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        // 只有管理员可以调用此API
        if (!currentUser.IsAdmin)
        {
            return Forbid();
        }

        // 检查MCP服务器是否存在
        bool mcpExists = await db.McpServers.AnyAsync(x => x.Id == mcpId, cancellationToken);
        if (!mcpExists)
        {
            return NotFound();
        }

        // 获取已分配的用户ID
        List<int> assignedUserIds = await db.UserMcps
            .Where(um => um.McpServerId == mcpId)
            .Select(um => um.UserId)
            .ToListAsync(cancellationToken);

        // 构建查询，排除已分配的用户
        IQueryable<User> query = db.Users.Where(u => !assignedUserIds.Contains(u.Id));

        // 如果有搜索条件，添加搜索过滤
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.DisplayName.Contains(search) || u.UserName.Contains(search));
        }

        // 按更新时间倒序排列，限制数量
        UnassignedUserDto[] unassignedUsers = await query
            .OrderByDescending(u => u.UpdatedAt)
            .Take(Math.Min(limit, 50)) // 最多50条
            .Select(u => new UnassignedUserDto
            {
                Id = u.Id,
                UserName = u.DisplayName
            })
            .ToArrayAsync(cancellationToken);

        return Ok(unassignedUsers);
    }

    [HttpGet("{mcpId:int}/assigned-user-details")]
    public async Task<ActionResult<AssignedUserDetailsDto[]>> GetAssignedUserDetails(int mcpId, CancellationToken cancellationToken)
    {
        IQueryable<McpServer> query = db.McpServers.Where(x => x.Id == mcpId);
        query = ApplyAdminFilter(query);

        bool hasAccess = await query.AnyAsync(cancellationToken);
        if (!hasAccess)
        {
            return NotFound();
        }

        AssignedUserDetailsDto[] assignedUsers = await db.UserMcps
            .Where(um => um.McpServerId == mcpId)
            .Include(um => um.User)
            .OrderBy(um => um.User.DisplayName)
            .Select(um => new AssignedUserDetailsDto
            {
                Id = um.UserId,
                UserName = um.User.DisplayName,
                CustomHeaders = um.CustomHeaders,
                ShowShortcut = um.ShowShortcut,
            })
            .ToArrayAsync(cancellationToken);

        return Ok(assignedUsers);
    }

    [HttpPut("{mcpId:int}/my-assignment")]
    public async Task<ActionResult> UpdateMyMcpAssignment(
        int mcpId,
        [FromBody] UpdateMyMcpAssignmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        UserMcp? assignment = await db.UserMcps
            .FirstOrDefaultAsync(um => um.McpServerId == mcpId && um.UserId == currentUser.Id, cancellationToken);
        if (assignment is null)
        {
            return NotFound();
        }

        if (assignment.ShowShortcut != request.ShowShortcut)
        {
            assignment.ShowShortcut = request.ShowShortcut;
            await db.SaveChangesAsync(cancellationToken);
        }

        return Ok();
    }

    [HttpGet("{mcpId:int}/assigned-user-names")]
    public async Task<ActionResult<AssignedUserNameDto[]>> GetAssignedUserNames(int mcpId, CancellationToken cancellationToken)
    {
        IQueryable<McpServer> query = db.McpServers.Where(x => x.Id == mcpId && (currentUser.IsAdmin || x.OwnerUserId == currentUser.Id));
        query = ApplyAdminFilter(query);

        bool hasAccess = await query.AnyAsync(cancellationToken);
        if (!hasAccess)
        {
            return NotFound();
        }

        AssignedUserNameDto[] assignedUsers = await db.UserMcps
            .Where(um => um.McpServerId == mcpId)
            .Include(um => um.User)
            .OrderBy(um => um.User.DisplayName)
            .Select(um => new AssignedUserNameDto
            {
                Id = um.UserId,
                UserName = um.User.DisplayName
            })
            .ToArrayAsync(cancellationToken);

        return Ok(assignedUsers);
    }
}
