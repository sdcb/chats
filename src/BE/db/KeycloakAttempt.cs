using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("KeycloakAttempt")]
[Index("ClientInfoId", Name = "IX_KeycloakAttempt_ClientInfo")]
[Index("CreatedAt", Name = "IX_KeycloakAttempt_CreatedAt")]
[Index("UserId", Name = "IX_KeycloakAttempt_UserId")]
public partial class KeycloakAttempt
{
    [Key]
    public int Id { get; set; }

    public int ClientInfoId { get; set; }

    public int? UserId { get; set; }

    [StringLength(200)]
    [Unicode(false)]
    public string Provider { get; set; } = null!;

    [StringLength(1000)]
    public string? Sub { get; set; }

    [StringLength(1000)]
    public string? Email { get; set; }

    public bool IsSuccessful { get; set; }

    [StringLength(1000)]
    [Unicode(false)]
    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("ClientInfoId")]
    [InverseProperty("KeycloakAttempts")]
    public virtual ClientInfo ClientInfo { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("KeycloakAttempts")]
    public virtual User? User { get; set; }
}
