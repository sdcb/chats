using Chats.BE.Controllers.Common;
using Chats.BE.Controllers.Common.Results;
using Chats.BE.Controllers.Public.AccountLogin.Dtos;
using Chats.BE.Controllers.Public.SMSs;
using Chats.BE.DB;
using Chats.BE.DB.Enums;
using Chats.BE.DB.Jsons;
using Chats.BE.Services;
using Chats.BE.Services.Common;
using Chats.BE.Services.Configs;
using Chats.BE.Services.Keycloak;
using Chats.BE.Services.Sessions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Public.AccountLogin;

[Route("api/public")]
public class AccountLoginController(ChatsDB db, ILogger<AccountLoginController> logger, SessionManager sessionManager, ClientInfoManager clientInfoService) : ControllerBase
{
    [HttpPost("account-login")]
    public async Task<ActionResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] PasswordHasher passwordHasher,
        [FromServices] GlobalDBConfig kcStore,
        [FromServices] UserManager userManager,
        [FromServices] HostUrlService hostUrl,
        CancellationToken cancellationToken)
    {
        object dto = request.AsLoginDto();
        if (dto is SsoLoginRequest sso)
        {
            if (sso.Provider == null) // WeChat
            {
                return new OldBEActionResult(sso);
            }
            else if (sso.Provider.Equals(KnownLoginProviders.Keycloak, StringComparison.OrdinalIgnoreCase))
            {
                return await KeycloakLogin(kcStore, userManager, sso, hostUrl, cancellationToken);
            }
        }
        else if (dto is PasswordLoginRequest passwordDto)
        {
            return await PasswordLogin(passwordHasher, passwordDto, cancellationToken);
        }

        throw new InvalidOperationException("Invalid login request.");
    }

    private async Task<ActionResult> KeycloakLogin(GlobalDBConfig kcStore, UserManager userManager, SsoLoginRequest sso, HostUrlService hostUrl, CancellationToken cancellationToken)
    {
        JsonKeycloakConfig? kcConfig = await kcStore.GetKeycloakConfig(cancellationToken);
        if (kcConfig == null)
        {
            return NotFound("Keycloak config not found");
        }

        AccessTokenInfo token = await kcConfig.GetUserInfo(sso.Code, hostUrl.GetKeycloakSsoRedirectUrl(), cancellationToken);
        User user = await userManager.EnsureKeycloakUser(token, cancellationToken);
        return Ok(await sessionManager.GenerateSessionForUser(user, cancellationToken));
    }

    private async Task<ActionResult> PasswordLogin(PasswordHasher passwordHasher, PasswordLoginRequest passwordDto, CancellationToken cancellationToken)
    {
        User? dbUser = await db.Users.FirstOrDefaultAsync(x => x.UserName == passwordDto.UserName, cancellationToken);

        if (dbUser == null)
        {
            logger.LogWarning("User not found: {UserName}", passwordDto.UserName);
            return BadRequest("Invalid username or password");
        }
        if (!dbUser.Enabled)
        {
            logger.LogWarning("User disabled: {UserName}", passwordDto.UserName);
            return BadRequest("Invalid username or password");
        }
        if (!passwordHasher.VerifyPassword(passwordDto.Password, dbUser.PasswordHash))
        {
            logger.LogWarning("Invalid password: {UserName}", passwordDto.UserName);
            return BadRequest("Invalid username or password");
        }

        return Ok(await sessionManager.GenerateSessionForUser(dbUser, cancellationToken));
    }

    [HttpPost("phone-login")]
    public async Task<IActionResult> PhoneLogin([FromBody] SmsLoginRequest req,
        [FromServices] SessionManager sessionManager,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest("Invalid phone.");
        }
        if (req.SmsCode.Length != SmsController.CodeLength)
        {
            return BadRequest("Invalid code.");
        }
        if (!db.LoginServices.Any(x => x.Enabled && x.Type == KnownLoginProviders.Phone))
        {
            return BadRequest("Phone login not enabled.");
        }

        SmsRecord? existingSms = await db.SmsRecords
            .Where(x => x.PhoneNumber == req.Phone && x.TypeId == (byte)DBSmsType.Login && x.StatusId == (byte)DBSmsStatus.WaitingForVerification)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingSms == null)
        {
            logger.LogWarning("Sms not sent: {Phone}, code: {Code}", req.Phone, req.SmsCode);
            return BadRequest("Invalid code.");
        }

        BadRequestObjectResult? commonCheckError = await PushAttemptCheck(req.Phone, req.SmsCode, existingSms, cancellationToken);
        if (commonCheckError != null)
        {
            return commonCheckError;
        }

        User? user = await db.Users.FirstOrDefaultAsync(x => x.Phone == req.Phone && x.Enabled, cancellationToken);
        if (user == null)
        {
            return BadRequest("Phone number not registered.");
        }

        return Ok(await sessionManager.GenerateSessionForUser(user, cancellationToken));
    }

    private async Task<BadRequestObjectResult?> PushAttemptCheck(string phoneNumber, string requestSmsCode, SmsRecord existingSms, CancellationToken cancellationToken)
    {
        int attemps = existingSms.SmsAttempts.Count;
        if (attemps >= SmsController.MaxAttempts)
        {
            logger.LogWarning("Too many attempts: {Phone}, attemp: {attemp}, code: {code}", phoneNumber, attemps, requestSmsCode);
            return BadRequest("Too many attempts.");
        }

        existingSms.SmsAttempts.Add(new SmsAttempt()
        {
            SmsRecordId = existingSms.Id,
            CreatedAt = DateTime.UtcNow,
            Code = requestSmsCode,
            ClientInfo = await clientInfoService.GetClientInfo(cancellationToken),
        });
        await db.SaveChangesAsync(cancellationToken);

        if (existingSms.ExpectedCode != requestSmsCode)
        {
            return BadRequest("Invalid code.");
        }

        if (existingSms.CreatedAt + TimeSpan.FromSeconds(SmsController.SmsExpirationSeconds) < DateTime.UtcNow)
        {
            return BadRequest("Sms expired.");
        }

        return null;
    }

    [HttpPost("phone-register")]
    public async Task<IActionResult> PhoneRegister([FromBody] PhoneRegisterRequest req, [FromServices] UserManager userManager, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest("Invalid phone.");
        }

        InvitationCode? code = await db.InvitationCodes.FirstOrDefaultAsync(x => x.Value == req.InvitationCode && !x.IsDeleted, cancellationToken);
        if (code == null)
        {
            return BadRequest("Invalid invitation code.");
        }

        User? existingUser = await db.Users.FirstOrDefaultAsync(x => x.Phone == req.Phone, cancellationToken);
        if (existingUser != null)
        {
            return BadRequest("Phone number already registered.");
        }

        SmsRecord? existingSms = await db.SmsRecords
            .Where(x => x.PhoneNumber == req.Phone && x.TypeId == (byte)DBSmsType.Register && x.StatusId == (byte)DBSmsStatus.WaitingForVerification)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingSms == null)
        {
            return BadRequest("Sms not sent.");
        }

        BadRequestObjectResult? commonCheckError = await PushAttemptCheck(req.Phone, req.SmsCode, existingSms, cancellationToken);
        if (commonCheckError != null)
        {
            return commonCheckError;
        }

        User user = new()
        {
            Phone = req.Phone,
            Enabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            UserName = req.Phone,
            DisplayName = req.Phone,
            PasswordHash = null,
            Avatar = null,
            Email = null, 
            Provider = KnownLoginProviders.Phone,
            Role = "-",
            Sub = null, 
            InvitationCodes = [code]
        };
        db.Users.Add(user);
        await userManager.InitializeUserWithoutSave(user, KnownLoginProviders.Phone, req.InvitationCode, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await sessionManager.GenerateSessionForUser(user, cancellationToken));
    }
}
