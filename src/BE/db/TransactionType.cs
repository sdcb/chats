using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("TransactionType")]
public partial class TransactionType
{
    [Key]
    public byte Id { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string Name { get; set; } = null!;

    [InverseProperty("TransactionType")]
    public virtual ICollection<BalanceTransaction> BalanceTransactions { get; set; } = new List<BalanceTransaction>();

    [InverseProperty("TransactionType")]
    public virtual ICollection<UsageTransaction> UsageTransactions { get; set; } = new List<UsageTransaction>();
}
