using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("UsageTransaction")]
[Index("CreditUserId", Name = "IX_UsageTransaction_CreditUser")]
[Index("ModelSnapshotId", Name = "IX_UsageTransaction_ModelSnapshotId")]
public partial class UsageTransaction
{
    [Key]
    public long Id { get; set; }

    public byte TransactionTypeId { get; set; }

    public int TokenAmount { get; set; }

    public int CountAmount { get; set; }

    public int CreditUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public int ModelSnapshotId { get; set; }

    [ForeignKey("CreditUserId")]
    [InverseProperty("UsageTransactions")]
    public virtual User CreditUser { get; set; } = null!;

    [ForeignKey("ModelSnapshotId")]
    [InverseProperty("UsageTransactions")]
    public virtual ModelSnapshot ModelSnapshot { get; set; } = null!;

    [InverseProperty("UsageTransaction")]
    public virtual UserModelUsage? UserModelUsage { get; set; }
}
