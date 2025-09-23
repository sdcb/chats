using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("UserModelUsage")]
[Index("CreatedAt", Name = "IX_ModelUsage_CreatedAt")]
[Index("ModelId", Name = "IX_UserModelUsage_ModelId")]
[Index("UserId", Name = "IX_UserModelUsage_UserId")]
public partial class UserModelUsage
{
    [Key]
    public long Id { get; set; }

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

    public int UserId { get; set; }

    public short ModelId { get; set; }

    [ForeignKey("BalanceTransactionId")]
    [InverseProperty("UserModelUsage")]
    public virtual BalanceTransaction? BalanceTransaction { get; set; }

    [ForeignKey("ClientInfoId")]
    [InverseProperty("UserModelUsages")]
    public virtual ClientInfo ClientInfo { get; set; } = null!;

    [ForeignKey("FinishReasonId")]
    [InverseProperty("UserModelUsages")]
    public virtual FinishReason FinishReason { get; set; } = null!;

    [ForeignKey("ModelId")]
    [InverseProperty("UserModelUsages")]
    public virtual Model Model { get; set; } = null!;

    [InverseProperty("Usage")]
    public virtual ICollection<Step> Steps { get; set; } = new List<Step>();

    [ForeignKey("UsageTransactionId")]
    [InverseProperty("UserModelUsage")]
    public virtual UsageTransaction? UsageTransaction { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("UserModelUsages")]
    public virtual User User { get; set; } = null!;

    [InverseProperty("Usage")]
    public virtual UserApiUsage? UserApiUsage { get; set; }
}
