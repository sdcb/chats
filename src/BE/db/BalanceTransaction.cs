using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("BalanceTransaction")]
[Index("CreditUserId", Name = "IX_BalanceTransaction_CreditUserId")]
[Index("UserId", Name = "IX_BalanceTransaction_UserId")]
public partial class BalanceTransaction
{
    [Key]
    public long Id { get; set; }

    public byte TransactionTypeId { get; set; }

    [Column(TypeName = "decimal(14, 8)")]
    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; }

    public int UserId { get; set; }

    public int CreditUserId { get; set; }

    [ForeignKey("CreditUserId")]
    [InverseProperty("BalanceTransactionCreditUsers")]
    public virtual User CreditUser { get; set; } = null!;

    [ForeignKey("TransactionTypeId")]
    [InverseProperty("BalanceTransactions")]
    public virtual TransactionType TransactionType { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("BalanceTransactionUsers")]
    public virtual User User { get; set; } = null!;

    [InverseProperty("BalanceTransaction")]
    public virtual UserModelUsage? UserModelUsage { get; set; }
}
