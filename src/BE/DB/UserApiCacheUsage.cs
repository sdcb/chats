using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("UserApiCacheUsage")]
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
