using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("PasswordAttempt")]
[Index("ClientInfoId", Name = "IX_PasswordAttempt_ClientInfo")]
[Index("CreatedAt", Name = "IX_PasswordAttempt_CreatedAt")]
[Index("UserId", Name = "IX_PasswordAttempt_UserId")]
public partial class PasswordAttempt
{
    [Key]
    public int Id { get; set; }

    [StringLength(1000)]
    public string UserName { get; set; } = null!;

    public int ClientInfoId { get; set; }

    public int? UserId { get; set; }

    public bool IsSuccessful { get; set; }

    [StringLength(1000)]
    [Unicode(false)]
    public string? FailureReason { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("ClientInfoId")]
    [InverseProperty("PasswordAttempts")]
    public virtual ClientInfo ClientInfo { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("PasswordAttempts")]
    public virtual User? User { get; set; }
}
