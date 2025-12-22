using Chats.Web.Controllers.Common.Dtos;
using Chats.Web.Controllers.Users.Usages.Dtos;
using Chats.Web.DB;
using Chats.Web.DB.Enums;
using Chats.Web.Infrastructure;
using Chats.Web.Services.Common;
using Chats.Web.Services.UrlEncryption;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;

namespace Chats.Web.Controllers.Users.Usages;

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

        IQueryable<UsageDto> rows = ProcessQuery(query);
        PagedResult<UsageDto> result = await PagedResult.FromQuery(rows, query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("excel")]
    public ActionResult<PagedResult<UsageDto>> ExportExcel(UsageQueryNoPagination query)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        IQueryable<UsageDto> rows = ProcessQuery(query);

        MemoryStream stream = new();
        MiniExcel.SaveAs(stream, rows);
        stream.Position = 0;
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", query.ToExcelFileName());
    }

    [HttpGet("stat")]
    public async Task<ActionResult<UsageStatistics>> GetStatistics(UsageQueryNoPagination query, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        IQueryable<UsageDto> rows = ProcessQuery(query);
        UsageStatistics stat = await UsageStatistics.FromQuery(rows, cancellationToken);
        return Ok(stat);
    }

    private IQueryable<UsageDto> ProcessQuery(IUsageQuery query)
    {
        IQueryable<UserModelUsage> usagesQuery = db.UserModelUsages;

        if (currentUser.IsAdmin)
        {
            if (!string.IsNullOrEmpty(query.User))
            {
                usagesQuery = usagesQuery.Where(u => u.User.UserName == query.User);
            }
        }
        else
        {
            usagesQuery = usagesQuery.Where(u => u.UserId == currentUser.Id);
        }

        if (!string.IsNullOrEmpty(query.ApiKeyId))
        {
            usagesQuery = usagesQuery.Where(u => u.UserApiUsage!.ApiKey.Id == idEncryption.DecryptApiKeyId(query.ApiKeyId));
        }

        if (!string.IsNullOrEmpty(query.Provider))
        {
            // 通过 ModelProviderInfo 查找匹配的 ProviderId
            DBModelProvider? matchingProviderId = ModelProviderInfo.GetIdByName(query.Provider);
            if (matchingProviderId != null)
            {
                usagesQuery = usagesQuery.Where(u => u.Model.ModelKey.ModelProviderId == (short)matchingProviderId);
            }
        }

        if (!string.IsNullOrEmpty(query.ModelKey))
        {
            usagesQuery = usagesQuery.Where(u => u.Model.ModelKey.Name == query.ModelKey);
        }

        if (!string.IsNullOrEmpty(query.Model))
        {
            usagesQuery = usagesQuery.Where(u => u.Model.Name == query.Model);
        }

        if (query.Start != null)
        {
            DateTime localStart = query.Start.Value
                .ToDateTime(new TimeOnly(), DateTimeKind.Utc)
                .AddMinutes(query.TimezoneOffset);
            usagesQuery = usagesQuery.Where(u => u.CreatedAt >= localStart);
        }
        if (query.End != null)
        {
            DateTime localEnd = query.End.Value
                .AddDays(1)
                .ToDateTime(new TimeOnly(), DateTimeKind.Utc)
                .AddMinutes(query.TimezoneOffset);
            usagesQuery = usagesQuery.Where(u => u.CreatedAt < localEnd);
        }

        if (query.Source == UsageSource.WebChat)
        {
            usagesQuery = usagesQuery.Where(u => u.UserApiUsage == null);
        }
        if (query.Source == UsageSource.Api)
        {
            usagesQuery = usagesQuery.Where(u => u.UserApiUsage != null);
        }

        IQueryable<UsageDto> rows = usagesQuery
            .OrderByDescending(u => u.Id)
            .Select(u => new UsageDto
            {
                UserName = u.User.UserName,
                ApiKeyId = idEncryption.EncryptApiKeyId((int?)u.UserApiUsage!.ApiKey.Id),
                ApiKey = u.UserApiUsage!.ApiKey.Key.ToMaskedNull(),
                ModelProviderName = ModelProviderInfo.GetName((DBModelProvider)u.Model.ModelKey.ModelProviderId),
                ModelReferenceName = u.Model.DeploymentName,
                ModelName = u.Model.Name,
                PreprocessDurationMs = u.PreprocessDurationMs,
                FirstResponseDurationMs = u.FirstResponseDurationMs,
                PostprocessDurationMs = u.PostprocessDurationMs,
                TotalDurationMs = u.TotalDurationMs,
                FinishReason = u.FinishReason.Name,
                UserAgent = u.ClientInfo.ClientUserAgent.UserAgent,
                IP = u.ClientInfo.ClientIp.Ipaddress,
                InputTokens = u.InputFreshTokens + u.InputCachedTokens,
                OutputTokens = u.OutputTokens,
                ReasoningTokens = u.ReasoningTokens,
                InputCost = u.InputFreshCost + u.InputCachedCost,
                OutputCost = u.OutputCost,
                UsagedCreatedAt = u.CreatedAt
            });
        return rows;
    }
}
