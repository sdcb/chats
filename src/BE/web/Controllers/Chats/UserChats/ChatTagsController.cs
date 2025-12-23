using Chats.DB;
using Chats.BE.Infrastructure;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Chats.UserChats;

[Route("api/chats/tags"), Authorize]
public class ChatTagsController(ChatsDB db, CurrentUser user, IUrlEncryptionService urlEncryption) : ControllerBase
{
    [HttpGet("{encryptedChatId}")]
    public async Task<ActionResult<string[]>> GetChatTags(string encryptedChatId, CancellationToken cancellationToken)
    {
        long chatId = urlEncryption.DecryptChatId(encryptedChatId);
        Chat? chat = await db.Chats
            .Include(x => x.ChatTags)
            .Where(x => x.Id == chatId && x.UserId == user.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (chat == null)
        {
            return NotFound();
        }

        return Ok(chat.ChatTags.Select(x => x.Name).ToArray());
    }

    [HttpPost("{encryptedChatId}")]
    public async Task<ActionResult> AddChatTag(string encryptedChatId, [FromBody] string tagName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return BadRequest("Tag name cannot be empty");
        }

        if (tagName.Length > 50)
        {
            return BadRequest("Tag name is too long");
        }

        long chatId = urlEncryption.DecryptChatId(encryptedChatId);
        Chat? chat = await db.Chats
            .Include(x => x.ChatTags)
            .Where(x => x.Id == chatId && x.UserId == user.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (chat == null)
        {
            return NotFound();
        }

        // Check if the tag already exists
        if (chat.ChatTags.Any(x => x.Name == tagName))
        {
            return Ok();
        }

        ChatTag? existingTag = await db.ChatTags.FirstOrDefaultAsync(x => x.Name == tagName, cancellationToken);
        ChatTag tag = existingTag ?? new ChatTag { Name = tagName };
        chat.ChatTags.Add(tag);

        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }

    [HttpDelete("{encryptedChatId}")]
    public async Task<ActionResult> RemoveChatTag(string encryptedChatId, [FromBody] string tagName, CancellationToken cancellationToken)
    {
        long chatId = urlEncryption.DecryptChatId(encryptedChatId);
        Chat? chat = await db.Chats
            .Include(x => x.ChatTags)
            .Where(x => x.Id == chatId && x.UserId == user.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (chat == null)
        {
            return NotFound();
        }
        ChatTag? tag = chat.ChatTags.FirstOrDefault(x => x.Name == tagName);
        if (tag == null)
        {
            return Ok();
        }
        chat.ChatTags.Remove(tag);
        await db.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
