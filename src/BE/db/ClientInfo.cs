using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ClientInfo")]
[Index("ClientIpId", "ClientUserAgentId", Name = "IX_ClientInfo", IsUnique = true)]
public partial class ClientInfo
{
    [Key]
    public int Id { get; set; }

    public int ClientIpId { get; set; }

    public int ClientUserAgentId { get; set; }

    [ForeignKey("ClientIpId")]
    [InverseProperty("ClientInfos")]
    public virtual ClientIp ClientIp { get; set; } = null!;

    [ForeignKey("ClientUserAgentId")]
    [InverseProperty("ClientInfos")]
    public virtual ClientUserAgent ClientUserAgent { get; set; } = null!;

    [InverseProperty("ClientInfo")]
    public virtual ICollection<File> Files { get; set; } = new List<File>();

    [InverseProperty("ClientInfo")]
    public virtual ICollection<KeycloakAttempt> KeycloakAttempts { get; set; } = new List<KeycloakAttempt>();

    [InverseProperty("ClientInfo")]
    public virtual ICollection<PasswordAttempt> PasswordAttempts { get; set; } = new List<PasswordAttempt>();

    [InverseProperty("ClientInfo")]
    public virtual ICollection<SmsAttempt> SmsAttempts { get; set; } = new List<SmsAttempt>();

    [InverseProperty("ClientInfo")]
    public virtual ICollection<UserApiCacheUsage> UserApiCacheUsages { get; set; } = new List<UserApiCacheUsage>();

    [InverseProperty("ClientInfo")]
    public virtual ICollection<UserApiCache> UserApiCaches { get; set; } = new List<UserApiCache>();

    [InverseProperty("ClientInfo")]
    public virtual ICollection<UserModelUsage> UserModelUsages { get; set; } = new List<UserModelUsage>();
}
