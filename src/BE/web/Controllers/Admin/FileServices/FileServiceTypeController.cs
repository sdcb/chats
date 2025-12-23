using Chats.DB;
using Chats.DB.Enums;
using Chats.BE.Controllers.Admin.Common;
using Microsoft.AspNetCore.Mvc;

namespace Chats.BE.Controllers.Admin.FileServices;

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