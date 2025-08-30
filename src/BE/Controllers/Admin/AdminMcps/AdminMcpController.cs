using Chats.BE.Controllers.Admin.AdminMcps.Dtos;
using Chats.BE.Controllers.Common;
using Chats.BE.DB;
using Chats.BE.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Client;
using System.Text.Json;

namespace Chats.BE.Controllers.Admin.AdminMcps;

[Route("api/mcp"), Authorize]
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
            .OrderByDescending(x => x.UserMcps.Any(um => um.UserId == currentUser.Id)) // user's own first
            .ThenByDescending(x => x.UserMcps.Where(um => um.UserId == currentUser.Id).Select(um => um.Id).FirstOrDefault()) // then by assignment time
            .ThenByDescending(x => x.Id)
            .Select(x => new McpServerListItemDto
            {
                Id = x.Id,
                Label = x.Label,
                Url = x.Url,
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
                        RequireApproval = t.RequireApproval,
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

        if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
        {
            return this.BadRequestMessage("Invalid URL");
        }

        if (!request.ValidateToolNameUnique())
        {
            return this.BadRequestMessage("Tool names must be unique within the server");
        }

        McpServer server = new()
        {
            Label = request.Label,
            Url = request.Url,
            Headers = string.IsNullOrWhiteSpace(request.Headers) ? null : request.Headers,
            IsPublic = request.IsPublic,
            OwnerUserId = currentUser.IsAdmin ? null : currentUser.Id,
            CreatedAt = DateTime.UtcNow,
            LastFetchAt = null,
        };

        // Add tools
        foreach (McpToolDto toolRequest in request.Tools)
        {
            server.McpTools.Add(new McpTool
            {
                McpServerId = server.Id,
                ToolName = toolRequest.Name,
                Description = toolRequest.Description,
                Parameters = toolRequest.Parameters,
                RequireApproval = toolRequest.RequireApproval,
            });
        }
        if (server.McpTools.Count != 0)
        {
            server.LastFetchAt = DateTime.UtcNow;
        }

        db.McpServers.Add(server);
        await db.SaveChangesAsync(cancellationToken);

        return await GetMcpServerDetails(server.Id, cancellationToken);
    }

    [HttpPut("{mcpId:int}")]
    public async Task<ActionResult<McpServerDetailsDto>> UpdateMcpServer(int mcpId, [FromBody] UpdateMcpServerRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        IQueryable<McpServer> finder = db.McpServers.Where(x => x.Id == mcpId);
        if (!currentUser.IsAdmin)
        {
            finder = finder.Where(x => x.OwnerUserId == currentUser.Id); // user can manage own only
        }

        McpServer? server = await finder.Include(x => x.McpTools).FirstOrDefaultAsync(cancellationToken);
        if (server == null)
        {
            return NotFound();
        }

        if (!Uri.IsWellFormedUriString(request.Url, UriKind.Absolute))
        {
            return this.BadRequestMessage("Invalid URL");
        }

        if (!request.ValidateToolNameUnique())
        {
            return this.BadRequestMessage("Tool names must be unique within the server");
        }

        server.Label = request.Label;
        server.Url = request.Url;
        server.Headers = string.IsNullOrWhiteSpace(request.Headers) ? null : request.Headers;
        server.IsPublic = request.IsPublic;
        
        // Update tools if provided
        if (request.Tools.Any())
        {
            UpdateMcpToolsInMemory(server, request.Tools);
            server.LastFetchAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);

        return await GetMcpServerDetails(mcpId, cancellationToken);
    }

    private static void UpdateMcpToolsInMemory(McpServer server, List<McpToolDto> requestTools)
    {
        // Create lookup dictionaries
        Dictionary<string, McpTool> existingToolsDict = server.McpTools.ToDictionary(t => t.ToolName, t => t);
        Dictionary<string, McpToolDto> requestToolsDict = requestTools.ToDictionary(t => t.Name, t => t);

        // Find tools to add (exist in request but not in database)
        IEnumerable<McpToolDto> toolsToAdd = requestTools.Where(rt => !existingToolsDict.ContainsKey(rt.Name));
        foreach (McpToolDto toolToAdd in toolsToAdd)
        {
            server.McpTools.Add(new McpTool
            {
                McpServerId = server.Id,
                ToolName = toolToAdd.Name,
                Description = toolToAdd.Description,
                Parameters = toolToAdd.Parameters,
                RequireApproval = toolToAdd.RequireApproval,
            });
        }

        // Find tools to update (exist in both request and database)
        IEnumerable<McpTool> toolsToUpdate = server.McpTools.Where(et => requestToolsDict.ContainsKey(et.ToolName));
        foreach (McpTool? existingTool in toolsToUpdate)
        {
            McpToolDto requestTool = requestToolsDict[existingTool.ToolName];
            existingTool.Description = requestTool.Description;
            existingTool.Parameters = requestTool.Parameters;
            existingTool.RequireApproval = requestTool.RequireApproval;
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
    public async Task<ActionResult<List<McpToolBasicInfo>>> FetchMcpTools([FromBody] FetchToolsRequest req, [FromServices] ILogger<AdminMcpController> logger, CancellationToken cancellationToken)
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

        try
        {
            IMcpClient client = await McpClientFactory.CreateAsync(new SseClientTransport(options), cancellationToken: cancellationToken);
            List<McpToolBasicInfo> tools = [];
            await foreach (McpClientTool tool in client.EnumerateToolsAsync(cancellationToken: cancellationToken))
            {
                tools.Add(new McpToolBasicInfo
                {
                    Name = tool.Name,
                    Description = tool.Description,
                    Parameters = tool.JsonSchema.GetRawText(),
                });
            }
            return Ok(tools);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to fetch MCP tools from {Url}", req.ServerUrl);
            return this.BadRequestMessage(ex.Message);
        }
    }
}
