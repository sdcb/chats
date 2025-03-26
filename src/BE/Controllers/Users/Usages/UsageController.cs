using Chats.BE.Controllers.Common.Dtos;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.DB;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Common;
using Chats.BE.Services.UrlEncryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Users.Usages;

[Route("api/usage"), Authorize]
public class UsageController(ChatsDB db, CurrentUser currentUser, IUrlEncryptionService idEncryption) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<UsageDto>>> GetUsages(UsageQuery query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        IQueryable<UserModelUsage> usagesQuery = db.UserModelUsages;

        if (currentUser.IsAdmin && !string.IsNullOrEmpty(query.User))
        {
            usagesQuery = usagesQuery.Where(u => u.UserModel.User.UserName == query.User);
        }
        else
        {
            usagesQuery = usagesQuery.Where(u => u.UserModel.UserId == currentUser.Id);
        }

        if (!string.IsNullOrEmpty(query.ApiKeyId))
        {
            usagesQuery = usagesQuery.Where(u => u.UserApiUsage!.ApiKey.Id == idEncryption.DecryptChatId(query.ApiKeyId));
        }

        if (!string.IsNullOrEmpty(query.Provider))
        {
            usagesQuery = usagesQuery.Where(u => u.UserModel.Model.ModelKey.ModelProvider.Name == query.Provider);
        }

        if (query.Start != null)
        {
            usagesQuery = usagesQuery.Where(u => u.CreatedAt >= query.Start);
        }

        if (query.End != null)
        {
            usagesQuery = usagesQuery.Where(u => u.CreatedAt <= query.End);
        }

        IQueryable<UsageDto> rows = usagesQuery
            .OrderByDescending(u => u.Id)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(u => new UsageDto
            {
                UserName = u.UserModel.User.UserName,
                ApiKeyId = idEncryption.EncryptApiKeyId((int?)u.UserApiUsage!.ApiKey.Id),
                ApiKey = u.UserApiUsage!.ApiKey.Key.ToMasked(),
                ModelProviderName = u.UserModel.Model.ModelReference.Provider.Name,
                ModelReferenceName = u.UserModel.Model.ModelReference.Name,
                ModelName = u.UserModel.Model.Name,
                PreprocessDurationMs = u.PreprocessDurationMs,
                FirstResponseDurationMs = u.FirstResponseDurationMs,
                PostprocessDurationMs = u.PostprocessDurationMs,
                TotalDurationMs = u.TotalDurationMs,
                FinishReason = u.FinishReason.Name,
                UserAgent = u.ClientInfo.ClientUserAgent.UserAgent,
                IP = u.ClientInfo.ClientIp.Ipaddress,
                InputTokens = u.InputTokens,
                OutputTokens = u.OutputTokens,
                ReasoningTokens = u.ReasoningTokens,
                InputCost = u.InputCost,
                OutputCost = u.OutputCost,
                UsagedCreatedAt = u.CreatedAt
            });

        PagedResult<UsageDto> result = await PagedResult.FromQuery(rows, query, cancellationToken);
        return Ok(result);
    }
}
