using Chats.BE.Controllers.Admin.Common;
using Chats.BE.Infrastructure;
using Chats.BE.Services.OAuth;
using Chats.DB;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Admin.OAuthClients;

[Route("api/admin/oauth-clients"), AuthorizeAdmin]
public class OAuthClientsController(ChatsDB db, CurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OAuthClientDto[]>> List(CancellationToken cancellationToken)
    {
        OAuthClientDto[] result = await db.OAuthClients
            .OrderByDescending(x => x.UpdatedAt)
            .Select(x => new OAuthClientDto
            {
                Id = x.Id,
                ClientId = x.ClientId,
                Name = x.Name,
                RedirectUris = ParseRedirectUris(x.RedirectUris),
                IsEnabled = x.IsEnabled,
                RequirePkce = x.RequirePkce,
                RequireClientSecret = x.RequireClientSecret,
                Scope = x.Scope,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
            })
            .ToArrayAsync(cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CreateOAuthClientResultDto>> Create([FromBody] UpsertOAuthClientRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ValidateRequest(request);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        string clientId = await GenerateUniqueClientId(cancellationToken);
        string? clientSecret = null;
        string? clientSecretHash = null;

        if (request.RequireClientSecret)
        {
            clientSecret = OAuthCrypto.GenerateOpaqueToken(36);
            clientSecretHash = OAuthCrypto.HashClientSecret(clientSecret);
        }

        OAuthClient entity = new()
        {
            ClientId = clientId,
            Name = request.Name.Trim(),
            RedirectUris = JoinRedirectUris(request.RedirectUris),
            IsEnabled = request.IsEnabled,
            RequirePkce = request.RequirePkce,
            RequireClientSecret = request.RequireClientSecret,
            ClientSecretHash = clientSecretHash,
            Scope = string.IsNullOrWhiteSpace(request.Scope) ? null : request.Scope.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = currentUser.Id,
        };

        db.OAuthClients.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return Created(string.Empty, new CreateOAuthClientResultDto
        {
            Id = entity.Id,
            ClientId = entity.ClientId,
            ClientSecret = clientSecret,
        });
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> Update(int id, [FromBody] UpsertOAuthClientRequest request, CancellationToken cancellationToken)
    {
        try
        {
            ValidateRequest(request);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }

        OAuthClient? entity = await db.OAuthClients.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        entity.Name = request.Name.Trim();
        entity.RedirectUris = JoinRedirectUris(request.RedirectUris);
        entity.IsEnabled = request.IsEnabled;
        entity.RequirePkce = request.RequirePkce;
        entity.Scope = string.IsNullOrWhiteSpace(request.Scope) ? null : request.Scope.Trim();

        if (entity.RequireClientSecret != request.RequireClientSecret)
        {
            entity.RequireClientSecret = request.RequireClientSecret;
            if (!request.RequireClientSecret)
            {
                entity.ClientSecretHash = null;
            }
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/reset-secret")]
    public async Task<ActionResult<ResetOAuthClientSecretResultDto>> ResetSecret(int id, CancellationToken cancellationToken)
    {
        OAuthClient? entity = await db.OAuthClients.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }
        if (!entity.RequireClientSecret)
        {
            return BadRequest("Client does not require secret.");
        }

        string clientSecret = OAuthCrypto.GenerateOpaqueToken(36);
        entity.ClientSecretHash = OAuthCrypto.HashClientSecret(clientSecret);
        entity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return Ok(new ResetOAuthClientSecretResultDto
        {
            ClientId = entity.ClientId,
            ClientSecret = clientSecret,
        });
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        OAuthClient? entity = await db.OAuthClients.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity == null)
        {
            return NotFound();
        }

        OAuthAuthorizationCode[] codes = await db.OAuthAuthorizationCodes.Where(x => x.ClientId == id).ToArrayAsync(cancellationToken);
        OAuthRefreshToken[] tokens = await db.OAuthRefreshTokens.Where(x => x.ClientId == id).ToArrayAsync(cancellationToken);
        db.OAuthAuthorizationCodes.RemoveRange(codes);
        db.OAuthRefreshTokens.RemoveRange(tokens);
        db.OAuthClients.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static void ValidateRequest(UpsertOAuthClientRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Name is required.");
        }
        if (request.RedirectUris.Length == 0)
        {
            throw new ArgumentException("At least one redirect uri is required.");
        }
        if (request.RedirectUris.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("redirect uris should not contain empty value.");
        }
    }

    private async Task<string> GenerateUniqueClientId(CancellationToken cancellationToken)
    {
        for (int i = 0; i < 8; i++)
        {
            string candidate = $"chats_cli_{OAuthCrypto.GenerateOpaqueToken(12)}";
            bool exists = await db.OAuthClients.AnyAsync(x => x.ClientId == candidate, cancellationToken);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Failed to generate unique client_id.");
    }

    private static string[] ParseRedirectUris(string redirectUris)
    {
        return redirectUris
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();
    }

    private static string JoinRedirectUris(string[] redirectUris)
    {
        return string.Join('\n', redirectUris.Select(x => x.Trim()));
    }
}

public record OAuthClientDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("redirectUris")]
    public required string[] RedirectUris { get; init; }

    [JsonPropertyName("isEnabled")]
    public required bool IsEnabled { get; init; }

    [JsonPropertyName("requirePkce")]
    public required bool RequirePkce { get; init; }

    [JsonPropertyName("requireClientSecret")]
    public required bool RequireClientSecret { get; init; }

    [JsonPropertyName("scope")]
    public required string? Scope { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTime CreatedAt { get; init; }

    [JsonPropertyName("updatedAt")]
    public required DateTime UpdatedAt { get; init; }
}

public record UpsertOAuthClientRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("redirectUris")]
    public required string[] RedirectUris { get; init; }

    [JsonPropertyName("isEnabled")]
    public required bool IsEnabled { get; init; }

    [JsonPropertyName("requirePkce")]
    public bool RequirePkce { get; init; } = true;

    [JsonPropertyName("requireClientSecret")]
    public bool RequireClientSecret { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }
}

public record CreateOAuthClientResultDto
{
    [JsonPropertyName("id")]
    public required int Id { get; init; }

    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    [JsonPropertyName("clientSecret")]
    public required string? ClientSecret { get; init; }
}

public record ResetOAuthClientSecretResultDto
{
    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    [JsonPropertyName("clientSecret")]
    public required string ClientSecret { get; init; }
}
