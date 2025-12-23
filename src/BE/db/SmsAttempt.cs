using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("SmsAttempt")]
[Index("ClientInfoId", Name = "IX_SmsAttempt")]
[Index("CreatedAt", Name = "IX_SmsAttempt_CreatedAt")]
[Index("SmsRecordId", Name = "IX_SmsAttempt_SmsHistoryId")]
public partial class SmsAttempt
{
    [Key]
    public int Id { get; set; }

    public int SmsRecordId { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string Code { get; set; } = null!;

    public int ClientInfoId { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("ClientInfoId")]
    [InverseProperty("SmsAttempts")]
    public virtual ClientInfo ClientInfo { get; set; } = null!;

    [ForeignKey("SmsRecordId")]
    [InverseProperty("SmsAttempts")]
    public virtual SmsRecord SmsRecord { get; set; } = null!;
}
