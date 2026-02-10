using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("OAuthAuthorizationCode")]
[Index("CodeHash", Name = "IX_OAuthAuthorizationCode_CodeHash", IsUnique = true)]
[Index("ClientId", Name = "IX_OAuthAuthorizationCode_ClientId")]
[Index("UserId", Name = "IX_OAuthAuthorizationCode_UserId")]
[Index("ApiKeyId", Name = "IX_OAuthAuthorizationCode_ApiKeyId")]
public partial class OAuthAuthorizationCode
{
    [Key]
    public long Id { get; set; }

    public int ClientId { get; set; }

    public int UserId { get; set; }

    public int ApiKeyId { get; set; }

    [StringLength(120)]
    [Unicode(false)]
    public string CodeHash { get; set; } = null!;

    [StringLength(1200)]
    [Unicode(false)]
    public string RedirectUri { get; set; } = null!;

    [StringLength(120)]
    [Unicode(false)]
    public string CodeChallenge { get; set; } = null!;

    [StringLength(20)]
    [Unicode(false)]
    public string CodeChallengeMethod { get; set; } = null!;

    [StringLength(500)]
    public string? Scope { get; set; }

    [StringLength(500)]
    public string? State { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UsedAt { get; set; }

    [ForeignKey("ApiKeyId")]
    [InverseProperty("AuthorizationCodes")]
    public virtual UserApiKey ApiKey { get; set; } = null!;

    [ForeignKey("ClientId")]
    [InverseProperty("AuthorizationCodes")]
    public virtual OAuthClient Client { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("OAuthAuthorizationCodes")]
    public virtual User User { get; set; } = null!;
}
