using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("UserApiCache")]
[Index("ClientInfoId", Name = "IX_UserApiCache_ClientInfoId")]
[Index("Expires", Name = "IX_UserApiCache_CreatedAt")]
[Index("ModelId", Name = "IX_UserApiCache_ModelId")]
[Index("RequestHashCode", Name = "IX_UserApiCache_RequestHashCode")]
[Index("UserApiKeyId", Name = "IX_UserApiCache_UserApiKeyId")]
public partial class UserApiCache
{
    [Key]
    public int Id { get; set; }

    public int UserApiKeyId { get; set; }

    public short ModelId { get; set; }

    public long RequestHashCode { get; set; }

    public DateTime Expires { get; set; }

    public int ClientInfoId { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("ClientInfoId")]
    [InverseProperty("UserApiCaches")]
    public virtual ClientInfo ClientInfo { get; set; } = null!;

    [ForeignKey("ModelId")]
    [InverseProperty("UserApiCaches")]
    public virtual Model Model { get; set; } = null!;

    [InverseProperty("UserApiCache")]
    public virtual UserApiCacheBody? UserApiCacheBody { get; set; }

    [InverseProperty("UserApiCache")]
    public virtual ICollection<UserApiCacheUsage> UserApiCacheUsages { get; set; } = new List<UserApiCacheUsage>();

    [ForeignKey("UserApiKeyId")]
    [InverseProperty("UserApiCaches")]
    public virtual UserApiKey UserApiKey { get; set; } = null!;
}
