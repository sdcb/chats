using Microsoft.AspNetCore.Authorization;

namespace Chats.BE.Controllers.Admin.Common;

public class AuthorizeAdminAttribute : AuthorizeAttribute
{
    public AuthorizeAdminAttribute()
    {
        Roles = "admin";
    }
}
