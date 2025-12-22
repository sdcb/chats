using Chats.Web.Controllers.Admin.Common;
using Chats.Web.DB;
using Chats.Web.DB.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Chats.Web.Controllers.Admin.FileServices;

[Route("api/admin/file-service-type"), AuthorizeAdmin]
public class FileServiceTypeController : ControllerBase
{
    [HttpGet("{fileServiceTypeId:int}/initial-config")]
    public ActionResult<string> GetFileServiceTypeInitialConfig(byte fileServiceTypeId)
    {
        DBFileServiceType serviceType = (DBFileServiceType)fileServiceTypeId;
        if (!FileServiceTypeInfo.IsValidServiceTypeId(serviceType))
        {
            return NotFound();
        }

        return Ok(FileServiceTypeInfo.GetInitialConfig(serviceType));
    }
}