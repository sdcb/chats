using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("OAuthClient")]
[Index("ClientId", Name = "IX_OAuthClient_ClientId", IsUnique = true)]
public partial class OAuthClient
{
    [Key]
    public int Id { get; set; }

    [StringLength(120)]
    [Unicode(false)]
    public string ClientId { get; set; } = null!;

    [StringLength(120)]
    public string Name { get; set; } = null!;

    [StringLength(4000)]
    public string RedirectUris { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public bool RequirePkce { get; set; }

    public bool RequireClientSecret { get; set; }

    [StringLength(200)]
    [Unicode(false)]
    public string? ClientSecretHash { get; set; }

    [StringLength(500)]
    public string? Scope { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? CreatedByUserId { get; set; }

    [InverseProperty("Client")]
    public virtual ICollection<OAuthAuthorizationCode> AuthorizationCodes { get; set; } = new List<OAuthAuthorizationCode>();

    [InverseProperty("Client")]
    public virtual ICollection<OAuthRefreshToken> RefreshTokens { get; set; } = new List<OAuthRefreshToken>();
}
