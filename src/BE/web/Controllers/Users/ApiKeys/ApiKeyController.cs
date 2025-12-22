using Chats.Web.Controllers.Users.ApiKeys.Dtos;
using Chats.Web.DB;
using Chats.Web.Infrastructure;
using Chats.Web.Services;
using Chats.Web.Services.UrlEncryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Chats.Web.Controllers.Users.ApiKeys;

[Authorize, Route("api/user/api-key")]
public class ApiKeyController(ChatsDB db, CurrentUser currentUser, IUrlEncryptionService idEncryption) : ControllerBase
{
    [HttpGet]
    public async Task<ListApiKeyDto[]> ListMyApiKeys(CancellationToken cancellationToken)
    {
        ListApiKeyDto[] result = await db.UserApiKeys
            .Where(x => x.UserId == currentUser.Id && !x.IsDeleted)
            .Select(x => new ListApiKeyDto
            {
                Id = idEncryption.EncryptApiKeyId(x.Id),
                Key = x.Key,
                IsRevoked = x.IsRevoked,
                Comment = x.Comment,
                AllowEnumerate = x.AllowEnumerate,
                AllowAllModels = x.AllowAllModels,
                Expires = x.Expires,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                LastUsedAt = x.UserApiUsages.FirstOrDefault(v => v.UsageId == x.UserApiUsages.Select(x => x.UsageId).Max())!.Usage.CreatedAt, 
                ModelCount = x.Models.Count
            })
            .ToArrayAsync(cancellationToken);
        return result;
    }

    [HttpGet("{apiKeyId}")]
    public async Task<ActionResult<short[]>> GetApiKeySupportedModels(string apiKeyId, [FromServices] UserModelManager userModelManager, CancellationToken cancellationToken)
    {
        UserApiKey? dbEntry = await db.UserApiKeys
            .Where(x => x.UserId == currentUser.Id && !x.IsDeleted)
            .Where(x => x.Id == idEncryption.DecryptApiKeyId(apiKeyId))
            .FirstOrDefaultAsync(cancellationToken);

        if (dbEntry is null) return NotFound();
        if (dbEntry.AllowAllModels)
        {
            HashSet<short> allowedModelIds = await userModelManager.GetValidModelsByUserId(currentUser.Id)
                .Select(x => x.ModelId)
                .ToHashSetAsync(cancellationToken);
            return Ok(allowedModelIds.ToArray());
        }
        else
        {
            return Ok(dbEntry.Models.Select(x => x.Id).ToArray());
        }
    }

    [HttpPost]
    public async Task<ListApiKeyDto> CreateApiKey(CancellationToken cancellationToken)
    {
        const int keyLength = 36;
        UserApiKey dbEntry = new()
        {
            UserId = currentUser.Id,
            Key = $"sk-{GenerateBase62Key(keyLength)}",
            Comment = $"New key",
            IsRevoked = false, 
            IsDeleted = false, 
            AllowEnumerate = true,
            AllowAllModels = true,
            Expires = DateTime.UtcNow.AddYears(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.UserApiKeys.Add(dbEntry);
        await db.SaveChangesAsync(cancellationToken);

        return new ListApiKeyDto()
        {
            Id =  idEncryption.EncryptApiKeyId(dbEntry.Id),
            Key = dbEntry.Key,
            IsRevoked = dbEntry.IsRevoked,
            Comment = dbEntry.Comment,
            AllowEnumerate = dbEntry.AllowEnumerate,
            AllowAllModels = dbEntry.AllowAllModels,
            Expires = dbEntry.Expires,
            CreatedAt = dbEntry.CreatedAt,
            UpdatedAt = dbEntry.UpdatedAt,
            LastUsedAt = null, 
            ModelCount = 0
        };
    }

    static string GenerateBase62Key(int length)
    {
        const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        // Calculate the number of bytes needed to generate a secure key of the specified length
        int byteSize = (int)Math.Ceiling(length * 5.954196310386875 / 8);

        // Generate random bytes
        byte[] randomBytes = new byte[byteSize];
        RandomNumberGenerator.Fill(randomBytes);

        // Convert to BigInteger
        BigInteger bigInt = new(randomBytes, isUnsigned: true);

        // Build Base62 string
        StringBuilder base62 = new();
        while (bigInt > 0 && base62.Length < length)
        {
            bigInt = BigInteger.DivRem(bigInt, 62, out BigInteger remainder);
            base62.Insert(0, Base62Chars[(int)remainder]);
        }

        // Pad the key if it's shorter than the desired length
        while (base62.Length < length)
        {
            base62.Insert(0, '0');
        }

        return base62.ToString();
    }

    [HttpDelete("{apiKeyId}")]
    public async Task<ActionResult> DeleteApiKey(string apiKeyId, CancellationToken cancellationToken)
    {
        UserApiKey? dbEntry = await db.UserApiKeys
            .Where(x => x.UserId == currentUser.Id && !x.IsDeleted)
            .Where(x => x.Id == idEncryption.DecryptApiKeyId(apiKeyId))
            .FirstOrDefaultAsync(cancellationToken);
        if (dbEntry == null) return NotFound();

        bool everUsed = await db.UserApiUsages
            .AnyAsync(x => x.ApiKeyId == idEncryption.DecryptApiKeyId(apiKeyId), cancellationToken);
        if (everUsed)
        {
            dbEntry.IsDeleted = true;
            dbEntry.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            db.UserApiKeys.Remove(dbEntry);
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }

    [HttpPut("{apiKeyId}")]
    public async Task<ActionResult> UpdateApiKey(string apiKeyId, [FromBody] UpdateApiKeyDto dto, CancellationToken cancellationToken)
    {
        UserApiKey? dbEntry = await db.UserApiKeys
            .Include(x => x.Models)
            .Where(x => x.UserId == currentUser.Id && !x.IsDeleted)
            .Where(x => x.Id == idEncryption.DecryptApiKeyId(apiKeyId))
            .FirstOrDefaultAsync(cancellationToken);

        if (dbEntry is null) return NotFound();

        dto.ApplyTo(dbEntry);
        if (db.ChangeTracker.HasChanges())
        {
            dbEntry.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return NoContent();
    }
}
