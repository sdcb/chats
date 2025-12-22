using Microsoft.AspNetCore.Authorization;

namespace Chats.Web.Controllers.Admin.Common;

public class AuthorizeAdminAttribute : AuthorizeAttribute
{
    public AuthorizeAdminAttribute()
    {
        Roles = "admin";
    }
}
