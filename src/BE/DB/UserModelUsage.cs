using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("UserModelUsage")]
[Index("BalanceTransactionId", Name = "IX_ModelUsage_BalanceTransaction", IsUnique = true)]
[Index("CreatedAt", Name = "IX_ModelUsage_CreatedAt")]
[Index("UsageTransactionId", Name = "IX_ModelUsage_UsageTransaction", IsUnique = true)]
[Index("UserModelId", Name = "IX_ModelUsage_UserModelId")]
public partial class UserModelUsage
{
    [Key]
    public long Id { get; set; }

    public int UserModelId { get; set; }

    public byte FinishReasonId { get; set; }

    public short SegmentCount { get; set; }

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }

    public int ReasoningTokens { get; set; }

    public bool IsUsageReliable { get; set; }

    public int PreprocessDurationMs { get; set; }

    public int ReasoningDurationMs { get; set; }

    public int FirstResponseDurationMs { get; set; }

    public int PostprocessDurationMs { get; set; }

    public int TotalDurationMs { get; set; }

    [Column(TypeName = "decimal(14, 8)")]
    public decimal InputCost { get; set; }

    [Column(TypeName = "decimal(14, 8)")]
    public decimal OutputCost { get; set; }

    public long? BalanceTransactionId { get; set; }

    public long? UsageTransactionId { get; set; }

    public int ClientInfoId { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("BalanceTransactionId")]
    [InverseProperty("UserModelUsage")]
    public virtual BalanceTransaction? BalanceTransaction { get; set; }

    [ForeignKey("ClientInfoId")]
    [InverseProperty("UserModelUsages")]
    public virtual ClientInfo ClientInfo { get; set; } = null!;

    [ForeignKey("FinishReasonId")]
    [InverseProperty("UserModelUsages")]
    public virtual FinishReason FinishReason { get; set; } = null!;

    [InverseProperty("Usage")]
    public virtual ICollection<MessageResponse> MessageResponses { get; set; } = new List<MessageResponse>();

    [ForeignKey("UsageTransactionId")]
    [InverseProperty("UserModelUsage")]
    public virtual UsageTransaction? UsageTransaction { get; set; }

    [InverseProperty("Usage")]
    public virtual UserApiUsage? UserApiUsage { get; set; }

    [ForeignKey("UserModelId")]
    [InverseProperty("UserModelUsages")]
    public virtual UserModel UserModel { get; set; } = null!;
}
