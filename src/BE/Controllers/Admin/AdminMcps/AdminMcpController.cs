using Chats.BE.Controllers.Admin.AdminMcps.Dtos;
using Chats.BE.Controllers.Common;
using Chats.BE.DB;
using Chats.BE.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Client;
using System.Text.Json;

namespace Chats.BE.Controllers.Admin.AdminMcps;

[Route("api/mcp")]
public class AdminMcpController(ChatsDB db, CurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<McpServerListItemDto[]>> ListAllMcpServers(CancellationToken cancellationToken)
    {
        IQueryable<McpServer> query = db.McpServers;
        if (!currentUser.IsAdmin)
        {
            query = query.Where(x => x.IsPublic || x.UserMcps.Any(um => um.UserId == currentUser.Id));
        }

        McpServerListItemDto[] data = await query
            .OrderByDescending(x => x.Id)
            .Select(x => new McpServerListItemDto
            {
                Id = x.Id,
                Label = x.Label,
                Url = x.Url,
                RequireApproval = x.RequireApproval,
                IsPublic = x.IsPublic,
                Owner = x.OwnerUser != null ? x.OwnerUser.DisplayName : null,
                CreatedAt = x.CreatedAt,
                LastFetchAt = x.LastFetchAt,
                ToolsCount = x.McpTools.Count,
            })
            .ToArrayAsync(cancellationToken);
        return Ok(data);
    }

    [HttpGet("{mcpId:int}")]
    public async Task<ActionResult<McpServerDetailsDto>> GetMcpServerDetails(int mcpId, CancellationToken cancellationToken)
    {
        IQueryable<McpServer> query = db.McpServers.Where(x => x.Id == mcpId);
        if (!currentUser.IsAdmin)
        {
            query = query.Where(x => x.IsPublic || x.UserMcps.Any(um => um.UserId == currentUser.Id));
        }

        McpServerDetailsDto? dto = await query
            .Select(x => new McpServerDetailsDto
            {
                Id = x.Id,
                Label = x.Label,
                Url = x.Url,
                RequireApproval = x.RequireApproval,
                Headers = x.Headers,
                IsPublic = x.IsPublic,
                Owner = x.OwnerUser != null ? x.OwnerUser.DisplayName : null,
                CreatedAt = x.CreatedAt,
                LastFetchAt = x.LastFetchAt,
                ToolsCount = x.McpTools.Count,
                Tools = x.McpTools
                    .OrderBy(t => t.Id)
                    .Select(t => new McpToolDto
                    {
                        Name = t.ToolName,
                        Description = t.Description,
                        Parameters = t.Parameters,
                    }).ToArray(),
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
        if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
        {
            return this.BadRequestMessage("Invalid URL");
        }

        McpServer server = new()
        {
            Label = request.Label,
            Url = request.Url,
            RequireApproval = request.RequireApproval,
            Headers = string.IsNullOrWhiteSpace(request.Headers) ? null : request.Headers,
            IsPublic = request.IsPublic,
            OwnerUserId = currentUser.IsAdmin ? null : currentUser.Id,
            CreatedAt = DateTime.UtcNow,
            LastFetchAt = null,
        };

        db.McpServers.Add(server);
        await db.SaveChangesAsync(cancellationToken);

        return await GetMcpServerDetails(server.Id, cancellationToken);
    }

    [HttpPut("{mcpId:int}")]
    public async Task<ActionResult<McpServerDetailsDto>> UpdateMcpServer(int mcpId, [FromBody] UpdateMcpServerRequest request, CancellationToken cancellationToken)
    {
        IQueryable<McpServer> finder = db.McpServers.Where(x => x.Id == mcpId);
        if (!currentUser.IsAdmin)
        {
            finder = finder.Where(x => x.OwnerUserId == currentUser.Id); // user can manage own only
        }

        McpServer? server = await finder.FirstOrDefaultAsync(cancellationToken);
        if (server == null)
        {
            return NotFound();
        }

        if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
        {
            return this.BadRequestMessage("Invalid URL");
        }

        server.Label = request.Label;
        server.Url = request.Url;
        server.RequireApproval = request.RequireApproval;
        server.Headers = string.IsNullOrWhiteSpace(request.Headers) ? null : request.Headers;
        server.IsPublic = request.IsPublic;
        // Do not change OwnerUserId on update
        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return await GetMcpServerDetails(mcpId, cancellationToken);
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

        bool anyReferences = await db.ChatConfigMcps.AnyAsync(x => x.McpServerId == mcpId, cancellationToken) ||
                             await db.UserMcps.AnyAsync(x => x.McpServerId == mcpId, cancellationToken);
        if (anyReferences)
        {
            return this.BadRequestMessage("MCP server is referenced by other entities");
        }

        db.McpServers.Remove(server);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("assign-user-mcps")]
    public async Task<ActionResult> AssignMcpToolsToUser([FromBody] AssignUserMcpRequest req, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAdmin)
        {
            return Forbid();
        }

        User? user = await db.Users.FirstOrDefaultAsync(x => x.Id == req.UserId, cancellationToken);
        if (user == null)
        {
            return NotFound();
        }

        if (req.HasDuplicateMcpServerIds())
        {
            return this.BadRequestMessage("Duplicate MCP server IDs in request");
        }

        Dictionary<int, McpServer> desiredServers = await db.McpServers
            .Where(x => req.McpServerIds.Contains(x.Id))
            .ToDictionaryAsync(k => k.Id, v => v, cancellationToken);
        if (desiredServers.Count != req.McpServerIds.Length)
        {
            return this.BadRequestMessage("Some MCP servers not found");
        }

        List<UserMcp> existing = await db.UserMcps.Where(x => x.UserId == req.UserId).ToListAsync(cancellationToken);
        HashSet<int> existingIds = [.. existing.Select(x => x.McpServerId)];
        HashSet<int> desiredIds = [.. req.McpServerIds.Where(id => desiredServers.ContainsKey(id))];

        foreach (int toAdd in desiredIds.Except(existingIds))
        {
            db.UserMcps.Add(new UserMcp
            {
                UserId = req.UserId,
                McpServerId = toAdd,
            });
        }

        foreach (UserMcp toRemove in existing.Where(x => !desiredIds.Contains(x.McpServerId)))
        {
            db.UserMcps.Remove(toRemove);
        }

        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("fetch-tools")]
    public async Task<ActionResult<List<McpToolDto>>> FetchMcpTools([FromBody] FetchToolsRequest req, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(req.ServerUrl, UriKind.Absolute, out Uri? serverUri))
        {
            return this.BadRequestMessage("Invalid serverUrl");
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
                return this.BadRequestMessage("headers must be a JSON object");
            }
        }

        SseClientTransportOptions options = new()
        {
            Endpoint = serverUri,
        };
        if (headerDict != null)
        {
            options.AdditionalHeaders = headerDict;
        }

        IMcpClient client = await McpClientFactory.CreateAsync(new SseClientTransport(options), cancellationToken: cancellationToken);
        List<McpToolDto> tools = [];
        await foreach (McpClientTool tool in client.EnumerateToolsAsync(cancellationToken: cancellationToken))
        {
            tools.Add(new McpToolDto
            {
                Name = tool.Name,
                Description = tool.Description,
                Parameters = tool.JsonSchema.GetRawText(),
            });
        }
        return Ok(tools);
    }
}
