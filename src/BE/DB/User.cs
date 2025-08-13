using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("User")]
public partial class User
{
    [Key]
    public int Id { get; set; }

    [StringLength(1000)]
    public string? Avatar { get; set; }

    [StringLength(1000)]
    public string UserName { get; set; } = null!;

    [StringLength(1000)]
    public string DisplayName { get; set; } = null!;

    [StringLength(1000)]
    [Unicode(false)]
    public string? PasswordHash { get; set; }

    [StringLength(1000)]
    public string? Email { get; set; }

    [StringLength(1000)]
    public string? Phone { get; set; }

    [StringLength(1000)]
    public string Role { get; set; } = null!;

    public bool Enabled { get; set; }

    [StringLength(1000)]
    [Unicode(false)]
    public string? Provider { get; set; }

    [StringLength(1000)]
    public string? Sub { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [InverseProperty("CreditUser")]
    public virtual ICollection<BalanceTransaction> BalanceTransactionCreditUsers { get; set; } = new List<BalanceTransaction>();

    [InverseProperty("User")]
    public virtual ICollection<BalanceTransaction> BalanceTransactionUsers { get; set; } = new List<BalanceTransaction>();

    [InverseProperty("User")]
    public virtual ICollection<ChatGroup> ChatGroups { get; set; } = new List<ChatGroup>();

    [InverseProperty("User")]
    public virtual ICollection<ChatPreset> ChatPresets { get; set; } = new List<ChatPreset>();

    [InverseProperty("User")]
    public virtual ICollection<Chat> Chats { get; set; } = new List<Chat>();

    [InverseProperty("CreateUser")]
    public virtual ICollection<File> Files { get; set; } = new List<File>();

    [InverseProperty("CreateUser")]
    public virtual ICollection<Prompt> Prompts { get; set; } = new List<Prompt>();

    [InverseProperty("User")]
    public virtual ICollection<SmsRecord> SmsRecords { get; set; } = new List<SmsRecord>();

    [InverseProperty("CreditUser")]
    public virtual ICollection<UsageTransaction> UsageTransactions { get; set; } = new List<UsageTransaction>();

    [InverseProperty("User")]
    public virtual ICollection<UserApiKey> UserApiKeys { get; set; } = new List<UserApiKey>();

    [InverseProperty("User")]
    public virtual UserBalance? UserBalance { get; set; }

    [InverseProperty("User")]
    public virtual ICollection<UserModelUsage> UserModelUsages { get; set; } = new List<UserModelUsage>();

    [InverseProperty("User")]
    public virtual ICollection<UserModel> UserModels { get; set; } = new List<UserModel>();

    [ForeignKey("UserId")]
    [InverseProperty("Users")]
    public virtual ICollection<InvitationCode> InvitationCodes { get; set; } = new List<InvitationCode>();
}
