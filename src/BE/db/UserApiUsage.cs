using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[PrimaryKey("ApiKeyId", "UsageId")]
[Table("UserApiUsage")]
[Index("UsageId", Name = "IX_UserApiUsage_UsageId", IsUnique = true)]
public partial class UserApiUsage
{
    [Key]
    public int ApiKeyId { get; set; }

    [Key]
    public long UsageId { get; set; }

    [ForeignKey("ApiKeyId")]
    [InverseProperty("UserApiUsages")]
    public virtual UserApiKey ApiKey { get; set; } = null!;

    [ForeignKey("UsageId")]
    [InverseProperty("UserApiUsage")]
    public virtual UserModelUsage Usage { get; set; } = null!;
}
