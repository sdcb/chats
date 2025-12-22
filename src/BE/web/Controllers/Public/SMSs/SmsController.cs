using Chats.Web.Controllers.Public.SMSs.Dtos;
using Chats.Web.DB;
using Chats.Web.DB.Enums;
using Chats.Web.Services.Common;
using Chats.Web.Services.Configs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TencentCloud.Common;
using TencentCloud.Sms.V20210111;
using TencentCloud.Sms.V20210111.Models;

namespace Chats.Web.Controllers.Public.SMSs;

[Route("api/public")]
public class SmsController(ChatsDB db, GlobalDBConfig globalConfig, ILogger<SmsController> logger) : ControllerBase
{
    public const int SmsExpirationSeconds = 300;
    public const int MaxAttempts = 3;
    public const int CodeLength = 6;

    [HttpPost("sms")]
    public async Task<IActionResult> SendSms([FromBody] SmsRequest req, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest("Invalid phone.");
        }
        if (!db.LoginServices.Any(x => x.Enabled && x.Type == KnownLoginProviders.Phone))
        {
            return BadRequest("Phone login is not enabled.");
        }

        SmsRecord? existingSms = await db.SmsRecords
            .Where(x => x.PhoneNumber == req.Phone && x.TypeId == (byte)req.Type && x.StatusId == (byte)DBSmsStatus.WaitingForVerification)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (existingSms != null && existingSms.CreatedAt + TimeSpan.FromSeconds(SmsExpirationSeconds) > DateTime.UtcNow)
        {
            return BadRequest("Sms already sent.");
        }

        if (req.Type == DBSmsType.Login)
        {
            return await SendLoginSms(req.Phone, cancellationToken);
        }
        else if (req.Type == DBSmsType.Register)
        {
            if (req.InvitationCode == null)
            {
                return BadRequest("Invitation code is required.");
            }
            return await SendRegisterSms(req.Phone, req.InvitationCode, cancellationToken);
        }

        return BadRequest("Invalid SMS type.");
    }

    private async Task<IActionResult> SendLoginSms(string phone, CancellationToken cancellationToken)
    {
        User? user = await db.Users.FirstOrDefaultAsync(x => x.Phone == phone, cancellationToken);
        if (user == null)
        {
            return BadRequest("Phone number not registered.");
        }

        return await SendSmsCommon(phone, DBSmsType.Login, cancellationToken);
    }

    private async Task<IActionResult> SendRegisterSms(string phone, string invitationCode, CancellationToken cancellationToken)
    {
        InvitationCode? code = await db.InvitationCodes.FirstOrDefaultAsync(x => x.Value == invitationCode && !x.IsDeleted, cancellationToken);
        if (code == null)
        {
            return BadRequest("Invalid invitation code.");
        }
        if (code.Count <= 0)
        {
            return BadRequest("Invitation code expired.");
        }

        User? user = await db.Users.FirstOrDefaultAsync(x => x.Phone == phone, cancellationToken);
        if (user != null)
        {
            return BadRequest("Phone number already registered.");
        }

        code.Count--;
        logger.LogInformation("Invitation code {code} used, remaining count: {count}", invitationCode, code.Count);

        return await SendSmsCommon(phone, DBSmsType.Register, cancellationToken);
    }

    async Task<IActionResult> SendSmsCommon(string phone, DBSmsType type, CancellationToken cancellationToken)
    {
        string code = Random.Shared.Next((int)Math.Pow(10, CodeLength) - 1).ToString($"D{CodeLength}");
        TencentSmsConfig smsConfig = await globalConfig.GetTencentSmsConfig(cancellationToken);
        SmsClient smsClient = new(new Credential
        {
            SecretId = smsConfig.SecretId,
            SecretKey = smsConfig.SecretKey
        }, "ap-guangzhou");

        SendSmsResponse resp = await smsClient.SendSms(new SendSmsRequest()
        {
            PhoneNumberSet = [phone],
            SignName = smsConfig.SignName,
            TemplateId = smsConfig.TemplateId,
            TemplateParamSet = [code],
            SmsSdkAppId = smsConfig.SdkAppId
        });
        if (resp.SendStatusSet.Length == 0)
        {
            logger.LogError("Failed to send sms to {phone}, no response.", phone);
            return BadRequest("Failed to send sms.");
        }
        else if (resp.SendStatusSet[0].Code != "Ok")
        {
            logger.LogError("Failed to send sms to {phone}, code: {code}, message: {message}",
                phone,
                resp.SendStatusSet[0].Code,
                resp.SendStatusSet[0].Message);
            return BadRequest("Failed to send sms.");
        }

        SmsRecord toInsert = new()
        {
            PhoneNumber = phone,
            TypeId = (byte)type,
            StatusId = (byte)DBSmsStatus.WaitingForVerification,
            ExpectedCode = code,
            CreatedAt = DateTime.UtcNow,
            UserId = db.Users.Where(x => x.Phone == phone).Select(x => (int?)x.Id).FirstOrDefault()
        };
        db.SmsRecords.Add(toInsert);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
