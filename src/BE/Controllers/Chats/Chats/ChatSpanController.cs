using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.Controllers.Chats.UserChats.Dtos;
using Chats.BE.DB;
using Chats.BE.Infrastructure;
using Chats.BE.Services;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Chats.Chats;

[Route("api/chat/{encryptedChatId}/span"), Authorize]
public class ChatSpanController(ChatsDB db, IUrlEncryptionService idEncryption, CurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ChatSpanDto>> CreateChatSpan(string encryptedChatId, [FromBody] CreateChatSpanRequest request,
        [FromServices] UserModelManager userModelManager,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        Chat? chat = await db.Chats
            .Include(x => x.ChatSpans.OrderByDescending(x => x.SpanId))
            .Include(x => x.ChatSpans.First().ChatConfig)
            .FirstOrDefaultAsync(x => x.Id == idEncryption.DecryptChatId(encryptedChatId) && x.UserId == currentUser.Id && !x.IsArchived, cancellationToken);
        if (chat == null)
        {
            return NotFound();
        }

        ChatSpan toAdd = null!;
        if (chat.ChatSpans.Count == 0)
        {
            IQueryable<UserModel> query = userModelManager.GetValidModelsByUserId(currentUser.Id);

            if (request.ModelId != null)
            {
                query = query.Where(x => x.ModelId == request.ModelId.Value);
            }

            UserModel? um = await query.FirstOrDefaultAsync(cancellationToken);
            if (um == null)
            {
                return BadRequest("No models available");
            }

            toAdd = new ChatSpan
            {
                ChatId = chat.Id,
                SpanId = 0,
                Enabled = true, 
                ChatConfig = new ChatConfig
                {
                    ModelId = um.ModelId,
                    Model = um.Model,
                    Temperature = request.SetsTemperature ? request.Temperature : null,
                    WebSearchEnabled = request.EnableSearch ?? false,
                    HashCode = 0,
                    MaxOutputTokens = null,
                    ReasoningEffort = null,
                    SystemPrompt = null,
                }
            };
        }
        else
        {
            if (chat.ChatSpans.Count >= 10)
            {
                return BadRequest("Max span count reached");
            }

            ChatSpan refSpan = chat.ChatSpans.First();
            short modelId = request.ModelId ?? refSpan.ModelId;
            UserModel? um = await userModelManager.GetValidModelsByUserId(currentUser.Id).FirstOrDefaultAsync(x => x.ModelId == modelId, cancellationToken);
            if (um == null)
            {
                return BadRequest("Model not available");
            }

            toAdd = new ChatSpan()
            {
                ChatId = chat.Id,
                SpanId = FindAvailableSpanId(chat.ChatSpans),
                Enabled = true, 
                ChatConfig = new()
                {
                    Model = um.Model,
                }
            };
        }
        request.ApplyTo(toAdd);
        chat.ChatSpans.Add(toAdd);
        await db.SaveChangesAsync(cancellationToken);

        return Created(default(string), ChatSpanDto.FromDB(toAdd));
    }

    /// <summary>
    /// Finds the next available SpanId for a new ChatSpan.
    /// </summary>
    /// <param name="spans">The SpanId desc ordered collection of existing ChatSpans.</param>
    /// <returns>The next available SpanId.</returns>
    static byte FindAvailableSpanId(ICollection<ChatSpan> spans)
    {
        // Suggest the next SpanId based on the last SpanId in the collection
        byte suggested = spans.First().SpanId;
        if (suggested < 255)
        {
            // If the suggested SpanId is less than 255, increment it by 1
            return (byte)(suggested + 1);
        }
        else
        {
            // If the suggested SpanId is 255, find the first available SpanId starting from 0
            byte spanId = 0;
            while (spans.Any(x => x.SpanId == spanId))
            {
                spanId++;
            }
            return spanId;
        }
    }

    [HttpPut("{spanId}")]
    public async Task<ActionResult<ChatSpanDto>> UpdateChatSpan(string encryptedChatId, byte spanId, [FromBody] CreateChatSpanRequest request,
        [FromServices] UserModelManager userModelManager,
        CancellationToken cancellationToken)
    {
        int chatId = idEncryption.DecryptChatId(encryptedChatId);

        ChatSpan? span = await db.ChatSpans.FirstOrDefaultAsync(x => 
            x.ChatId == chatId && x.SpanId == spanId && x.Chat.UserId == currentUser.Id && !x.Chat.IsArchived, cancellationToken);
        if (span == null)
        {
            return NotFound();
        }

        if (request.ModelId != null)
        {
            UserModel? um = await userModelManager.GetValidModelsByUserId(currentUser.Id).FirstOrDefaultAsync(x => x.ModelId == request.ModelId, cancellationToken);
            if (um == null)
            {
                return BadRequest("Model not available");
            }

            span.ModelId = request.ModelId.Value;
            span.Model = um.Model;
        }

        if (request.SetsTemperature)
        {
            span.Temperature = request.Temperature;
        }

        if (request.EnableSearch != null)
        {
            span.EnableSearch = request.EnableSearch.Value;
        }

        await db.SaveChangesAsync(cancellationToken);
        return Ok(ChatSpanDto.FromDB(span));
    }

    [HttpDelete("{spanId}")]
    public async Task<IActionResult> DeleteChatSpan(string encryptedChatId, byte spanId, CancellationToken cancellationToken)
    {
        int chatId = idEncryption.DecryptChatId(encryptedChatId);
        ChatSpan? span = await db.ChatSpans.FirstOrDefaultAsync(x =>
            x.ChatId == chatId && x.SpanId == spanId && x.Chat.UserId == currentUser.Id && !x.Chat.IsArchived, cancellationToken);
        if (span == null)
        {
            return NotFound();
        }

        db.ChatSpans.Remove(span);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
