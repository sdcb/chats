using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("UsageTransaction")]
[Index("CreditUserId", Name = "IX_UsageTransaction_CreditUser")]
[Index("ModelId", Name = "IX_UsageTransaction_ModelId")]
public partial class UsageTransaction
{
    [Key]
    public long Id { get; set; }

    public byte TransactionTypeId { get; set; }

    public int TokenAmount { get; set; }

    public int CountAmount { get; set; }

    public int CreditUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public short ModelId { get; set; }

    [ForeignKey("CreditUserId")]
    [InverseProperty("UsageTransactions")]
    public virtual User CreditUser { get; set; } = null!;

    [ForeignKey("ModelId")]
    [InverseProperty("UsageTransactions")]
    public virtual Model Model { get; set; } = null!;

    [ForeignKey("TransactionTypeId")]
    [InverseProperty("UsageTransactions")]
    public virtual TransactionType TransactionType { get; set; } = null!;

    [InverseProperty("UsageTransaction")]
    public virtual UserModelUsage? UserModelUsage { get; set; }
}
