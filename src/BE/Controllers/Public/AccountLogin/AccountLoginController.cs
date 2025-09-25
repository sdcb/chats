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
using Chats.BE.Services.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Public.AccountLogin;

[Route("api/public")]
public class AccountLoginController(ChatsDB db, ILogger<AccountLoginController> logger, SessionManager sessionManager, ClientInfoManager clientInfoService, LoginRateLimiter loginRateLimiter) : ControllerBase
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
        ClientInfo clientInfo = await clientInfoService.GetClientInfo(cancellationToken);

        JsonKeycloakConfig? kcConfig = await kcStore.GetKeycloakConfig(cancellationToken);
        if (kcConfig == null)
        {
            await loginRateLimiter.RecordKeycloakAttemptAsync(KnownLoginProviders.Keycloak, clientInfo.Id, null, false, null, null, "config-not-found", cancellationToken);
            return NotFound("Keycloak config not found");
        }

        try
        {
            AccessTokenInfo token = await kcConfig.GetUserInfo(sso.Code, hostUrl.GetKeycloakSsoRedirectUrl(), cancellationToken);
            User user = await userManager.EnsureKeycloakUser(token, cancellationToken);
            ActionResult sessionResult = Ok(await sessionManager.GenerateSessionForUser(user, cancellationToken));
            await loginRateLimiter.RecordKeycloakAttemptAsync(KnownLoginProviders.Keycloak, clientInfo.Id, user.Id, true, token.Sub, token.Email, null, cancellationToken);
            return sessionResult;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Keycloak login failed. ClientInfoId: {ClientInfoId}", clientInfo.Id);
            await loginRateLimiter.RecordKeycloakAttemptAsync(KnownLoginProviders.Keycloak, clientInfo.Id, null, false, null, null, ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task<ActionResult> PasswordLogin(PasswordHasher passwordHasher, PasswordLoginRequest passwordDto, CancellationToken cancellationToken)
    {
        ClientInfo clientInfo = await clientInfoService.GetClientInfo(cancellationToken);

        LoginRateLimiter.RateLimitCheckResult limitResult = await loginRateLimiter.CheckPasswordAsync(clientInfo, cancellationToken);
        if (!limitResult.IsAllowed)
        {
            logger.LogWarning("Password login rate limit reached. User: {UserName}, ClientInfoId: {ClientInfoId}", passwordDto.UserName, clientInfo.Id);
            return BadRequest(limitResult.ErrorMessage ?? "Too many attempts. Please try again later.");
        }

        User? dbUser = await db.Users.FirstOrDefaultAsync(x => x.UserName == passwordDto.UserName, cancellationToken);

        if (dbUser == null)
        {
            logger.LogWarning("User not found: {UserName}", passwordDto.UserName);
            await loginRateLimiter.RecordPasswordAttemptAsync(passwordDto.UserName, clientInfo.Id, null, false, "user-not-found", cancellationToken);
            return BadRequest("Invalid username or password");
        }
        if (!dbUser.Enabled)
        {
            logger.LogWarning("User disabled: {UserName}", passwordDto.UserName);
            await loginRateLimiter.RecordPasswordAttemptAsync(passwordDto.UserName, clientInfo.Id, dbUser.Id, false, "user-disabled", cancellationToken);
            return BadRequest("Invalid username or password");
        }
        if (!passwordHasher.VerifyPassword(passwordDto.Password, dbUser.PasswordHash))
        {
            logger.LogWarning("Invalid password: {UserName}", passwordDto.UserName);
            await loginRateLimiter.RecordPasswordAttemptAsync(passwordDto.UserName, clientInfo.Id, dbUser.Id, false, "password-mismatch", cancellationToken);
            return BadRequest("Invalid username or password");
        }

        await loginRateLimiter.RecordPasswordAttemptAsync(passwordDto.UserName, clientInfo.Id, dbUser.Id, true, null, cancellationToken);
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
        ClientInfo clientInfo = await clientInfoService.GetClientInfo(cancellationToken);

        LoginRateLimiter.RateLimitCheckResult limitResult = await loginRateLimiter.CheckSmsAsync(clientInfo, cancellationToken);
        if (!limitResult.IsAllowed)
        {
            logger.LogWarning("SMS attempt rate limit reached. Phone: {Phone}, ClientInfoId: {ClientInfoId}", phoneNumber, clientInfo.Id);
            return BadRequest(limitResult.ErrorMessage ?? "Too many attempts.");
        }

        DateTime utcNow = DateTime.UtcNow;
        bool isCodeValid = existingSms.ExpectedCode == requestSmsCode;
        bool isExpired = existingSms.CreatedAt + TimeSpan.FromSeconds(SmsController.SmsExpirationSeconds) < utcNow;
        bool isSuccess = isCodeValid && !isExpired;

        SmsAttempt attempt = new()
        {
            SmsRecordId = existingSms.Id,
            CreatedAt = utcNow,
            Code = requestSmsCode,
            ClientInfoId = clientInfo.Id,
            ClientInfo = clientInfo,
        };

        if (isSuccess)
        {
            existingSms.StatusId = (byte)DBSmsStatus.Verified;
        }

        existingSms.SmsAttempts.Add(attempt);
        await db.SaveChangesAsync(cancellationToken);

        if (!isCodeValid)
        {
            return BadRequest("Invalid code.");
        }

        if (isExpired)
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
