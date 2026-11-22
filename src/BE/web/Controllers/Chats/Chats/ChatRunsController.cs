using Chats.BE.Controllers.Chats.Messages;
using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.AgentRuns;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Chats;

[Route("api/chat-runs"), Authorize]
public class ChatRunsController(
    ChatsDB db,
    CurrentUser currentUser,
    IUrlEncryptionService idEncryption,
    AgentRunManager agentRunManager) : ControllerBase
{
    public const int MaxSteeringLength = 20_000;

    [HttpGet("active")]
    public async Task<ActionResult<ActiveChatRunDto?>> GetActive(
        [FromQuery] string chatId,
        [FromServices] ChatMessageViewService viewService,
        CancellationToken cancellationToken)
    {
        int decryptedChatId = idEncryption.DecryptChatId(chatId);
        DateTime freshAfter = DateTime.UtcNow - AgentRunManager.HeartbeatTimeout;
        ChatRun? run = await db.ChatRuns
            .Where(x => x.ChatId == decryptedChatId
                && x.UserId == currentUser.Id
                && x.Status == (byte)ChatRunStatus.Running
                && x.HeartbeatAt > freshAfter)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (run == null)
        {
            return Ok(null);
        }

        ChatRunMetadata metadata = string.IsNullOrEmpty(run.Metadata)
            ? new ChatRunMetadata()
            : JsonSerializer.Deserialize<ChatRunMetadata>(run.Metadata, JSON.JsonSerializerOptions) ?? new ChatRunMetadata();

        TurnDto? userTurn = metadata.UserTurnId != null
            ? await viewService.GetTurnDtoAsync(decryptedChatId, metadata.UserTurnId.Value, cancellationToken)
            : null;
        TurnDto? assistantTurn = metadata.AssistantTurnId != null
            ? await viewService.GetTurnDtoAsync(decryptedChatId, metadata.AssistantTurnId.Value, cancellationToken)
            : null;

        SteeringDto[] steerings = await QuerySteerings(run.Id, cancellationToken);

        return Ok(new ActiveChatRunDto
        {
            RunId = idEncryption.EncryptChatRunId(run.Id),
            Kind = (ChatRunKind)run.Kind,
            Mode = metadata.Mode,
            CreatedAt = run.CreatedAt,
            Spans = metadata.Spans,
            UserTurn = userTurn,
            AssistantTurn = assistantTurn,
            ParentAssistantTurnId = idEncryption.EncryptTurnId(metadata.ParentAssistantTurnId),
            ParentUserTurnId = idEncryption.EncryptTurnId(metadata.ParentUserTurnId),
            Steerings = steerings,
        });
    }

    /// <summary>
    /// Server-sent events of a run, starting after <paramref name="after"/> (the last <c>id:</c> the client received).
    /// Events are replayed from memory when the run executes on this instance, otherwise from <c>ChatRunEvent</c>.
    /// </summary>
    [HttpGet("{runId}/events")]
    public async Task GetEvents(string runId, [FromQuery] int after, CancellationToken cancellationToken)
    {
        long id = idEncryption.DecryptChatRunId(runId);
        ChatRun? run = await db.ChatRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.UserId == currentUser.Id, cancellationToken);
        if (run == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        Response.Headers.Connection = "keep-alive";
        await Response.Body.FlushAsync(cancellationToken);

        try
        {
            if (agentRunManager.TryGet(id, out LiveRun? live))
            {
                await StreamLocal(live, after, cancellationToken);
            }
            else
            {
                await StreamFromDatabase(id, after, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // client disconnected
        }
    }

    private async Task StreamLocal(LiveRun live, int after, CancellationToken cancellationToken)
    {
        while (true)
        {
            (string[] events, int firstSeq, bool completed, Task changed) = live.Read(after);
            for (int i = 0; i < events.Length; i++)
            {
                await WriteEvent(firstSeq + i, events[i], cancellationToken);
            }
            after = firstSeq + events.Length - 1;
            await Response.Body.FlushAsync(cancellationToken);

            if (completed) return;

            Task delay = Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            if (await Task.WhenAny(changed, delay) == delay)
            {
                await WriteKeepAlive(cancellationToken);
            }
        }
    }

    private async Task StreamFromDatabase(long id, int after, CancellationToken cancellationToken)
    {
        DateTime lastWriteAt = DateTime.UtcNow;
        while (true)
        {
            // read the status first: events persisted before the run is marked finished are all visible afterwards
            var state = await db.ChatRuns.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new { x.Status, x.HeartbeatAt })
                .FirstAsync(cancellationToken);

            List<ChatRunEvent> batches = await db.ChatRunEvents.AsNoTracking()
                .Where(x => x.RunId == id && x.LastSeq > after)
                .OrderBy(x => x.FirstSeq)
                .ToListAsync(cancellationToken);
            foreach (ChatRunEvent batch in batches)
            {
                using JsonDocument doc = JsonDocument.Parse(batch.Payload);
                int seq = batch.FirstSeq;
                foreach (JsonElement element in doc.RootElement.EnumerateArray())
                {
                    if (seq > after)
                    {
                        await WriteEvent(seq, element.GetRawText(), cancellationToken);
                        after = seq;
                        lastWriteAt = DateTime.UtcNow;
                    }
                    seq++;
                }
            }
            await Response.Body.FlushAsync(cancellationToken);

            bool finished = state.Status != (byte)ChatRunStatus.Running
                || state.HeartbeatAt < DateTime.UtcNow - AgentRunManager.HeartbeatTimeout;
            if (finished) return;

            // the run may have moved here (unlikely) or simply still be running elsewhere
            if (agentRunManager.TryGet(id, out LiveRun? live))
            {
                await StreamLocal(live, after, cancellationToken);
                return;
            }

            if (DateTime.UtcNow - lastWriteAt > TimeSpan.FromSeconds(15))
            {
                await WriteKeepAlive(cancellationToken);
                lastWriteAt = DateTime.UtcNow;
            }
            await Task.Delay(500, cancellationToken);
        }
    }

    private async Task WriteEvent(int seq, string json, CancellationToken cancellationToken)
    {
        await Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"id: {seq}\r\ndata: {json}\r\n\r\n"), cancellationToken);
    }

    private async Task WriteKeepAlive(CancellationToken cancellationToken)
    {
        await Response.Body.WriteAsync(": keep-alive\r\n\r\n"u8.ToArray(), cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    [HttpPost("{runId}/stop")]
    public async Task<IActionResult> Stop(string runId, CancellationToken cancellationToken)
    {
        long id = idEncryption.DecryptChatRunId(runId);
        int affected = await db.ChatRuns
            .Where(x => x.Id == id && x.UserId == currentUser.Id && x.Status == (byte)ChatRunStatus.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.StopRequestedAt, DateTime.UtcNow), cancellationToken);
        if (affected == 0)
        {
            return NotFound();
        }

        // fast path when the run executes on this instance; other instances pick up StopRequestedAt by polling
        agentRunManager.TryCancelLocal(id);
        return Ok();
    }

    [HttpGet("{runId}/steerings")]
    public async Task<ActionResult<SteeringDto[]>> GetSteerings(string runId, CancellationToken cancellationToken)
    {
        long id = idEncryption.DecryptChatRunId(runId);
        if (!await db.ChatRuns.AnyAsync(x => x.Id == id && x.UserId == currentUser.Id, cancellationToken))
        {
            return NotFound();
        }
        return Ok(await QuerySteerings(id, cancellationToken));
    }

    [HttpPost("{runId}/steer")]
    public async Task<ActionResult<SteeringDto>> Steer(string runId, [FromBody] SteerRequest req, CancellationToken cancellationToken)
    {
        string content = req.Content?.Trim() ?? "";
        if (content.Length == 0)
        {
            return BadRequest("Content is required");
        }
        if (content.Length > MaxSteeringLength)
        {
            return BadRequest($"Content is too long (max {MaxSteeringLength} characters)");
        }

        long id = idEncryption.DecryptChatRunId(runId);
        DateTime freshAfter = DateTime.UtcNow - AgentRunManager.HeartbeatTimeout;
        ChatRun? run = await db.ChatRuns.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == currentUser.Id, cancellationToken);
        if (run == null)
        {
            return NotFound();
        }
        if (run.Status != (byte)ChatRunStatus.Running || run.StopRequestedAt != null || run.HeartbeatAt <= freshAfter)
        {
            return Conflict("The run is no longer running");
        }
        ChatRunMetadata? metadata = string.IsNullOrEmpty(run.Metadata)
            ? null
            : JsonSerializer.Deserialize<ChatRunMetadata>(run.Metadata, JSON.JsonSerializerOptions);
        if (metadata == null || metadata.Spans.Length != 1)
        {
            return BadRequest("Steering is only supported when a single model is responding");
        }

        ChatRunSteering steering = new()
        {
            RunId = id,
            Content = content,
            Status = (byte)ChatRunSteeringStatus.Queued,
            CreatedAt = DateTime.UtcNow,
        };
        db.ChatRunSteerings.Add(steering);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(steering));
    }

    [HttpDelete("{runId}/steer/{steeringId:long}")]
    public async Task<IActionResult> CancelSteer(string runId, long steeringId, CancellationToken cancellationToken)
    {
        long id = idEncryption.DecryptChatRunId(runId);
        if (!await db.ChatRuns.AnyAsync(x => x.Id == id && x.UserId == currentUser.Id, cancellationToken))
        {
            return NotFound();
        }

        int affected = await db.ChatRunSteerings
            .Where(x => x.Id == steeringId && x.RunId == id && x.Status == (byte)ChatRunSteeringStatus.Queued)
            .ExecuteDeleteAsync(cancellationToken);
        return affected > 0 ? NoContent() : Conflict("The message has already been delivered");
    }

    private async Task<SteeringDto[]> QuerySteerings(long runId, CancellationToken cancellationToken)
    {
        ChatRunSteering[] items = await db.ChatRunSteerings.AsNoTracking()
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.Id)
            .ToArrayAsync(cancellationToken);
        return [.. items.Select(ToDto)];
    }

    private static SteeringDto ToDto(ChatRunSteering x) => new()
    {
        Id = x.Id,
        Content = x.Content,
        Status = (ChatRunSteeringStatus)x.Status,
        CreatedAt = x.CreatedAt,
    };
}

public sealed record SteerRequest
{
    [JsonPropertyName("content")]
    public string? Content { get; init; }
}

public sealed record ActiveChatRunDto
{
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    [JsonPropertyName("kind")]
    public required ChatRunKind Kind { get; init; }

    [JsonPropertyName("mode")]
    public required string Mode { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("spans")]
    public required ChatRunSpanInfo[] Spans { get; init; }

    [JsonPropertyName("userTurn")]
    public TurnDto? UserTurn { get; init; }

    [JsonPropertyName("assistantTurn")]
    public TurnDto? AssistantTurn { get; init; }

    [JsonPropertyName("parentAssistantTurnId")]
    public string? ParentAssistantTurnId { get; init; }

    [JsonPropertyName("parentUserTurnId")]
    public string? ParentUserTurnId { get; init; }

    [JsonPropertyName("steerings")]
    public required SteeringDto[] Steerings { get; init; }
}
