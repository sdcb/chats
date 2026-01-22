using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("UserApiCacheUsage")]
[Index("ClientInfoId", Name = "IX_UserApiCacheUsage_ClientInfoId")]
[Index("UserApiCacheId", Name = "IX_UserApiCacheUsage_UserApiCacheId")]
public partial class UserApiCacheUsage
{
    [Key]
    public long Id { get; set; }

    public int UserApiCacheId { get; set; }

    public int ClientInfoId { get; set; }

    public DateTime UsedAt { get; set; }

    [ForeignKey("ClientInfoId")]
    [InverseProperty("UserApiCacheUsages")]
    public virtual ClientInfo ClientInfo { get; set; } = null!;

    [ForeignKey("UserApiCacheId")]
    [InverseProperty("UserApiCacheUsages")]
    public virtual UserApiCache UserApiCache { get; set; } = null!;
}
