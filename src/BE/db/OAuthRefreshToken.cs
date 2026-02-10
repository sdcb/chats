using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("OAuthRefreshToken")]
[Index("TokenHash", Name = "IX_OAuthRefreshToken_TokenHash", IsUnique = true)]
[Index("ClientId", Name = "IX_OAuthRefreshToken_ClientId")]
[Index("UserId", Name = "IX_OAuthRefreshToken_UserId")]
[Index("ApiKeyId", Name = "IX_OAuthRefreshToken_ApiKeyId")]
public partial class OAuthRefreshToken
{
    [Key]
    public long Id { get; set; }

    public int ClientId { get; set; }

    public int UserId { get; set; }

    public int ApiKeyId { get; set; }

    [StringLength(120)]
    [Unicode(false)]
    public string TokenHash { get; set; } = null!;

    [StringLength(500)]
    public string? Scope { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastUsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    [ForeignKey("ApiKeyId")]
    [InverseProperty("OAuthRefreshTokens")]
    public virtual UserApiKey ApiKey { get; set; } = null!;

    [ForeignKey("ClientId")]
    [InverseProperty("RefreshTokens")]
    public virtual OAuthClient Client { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("OAuthRefreshTokens")]
    public virtual User User { get; set; } = null!;
}
