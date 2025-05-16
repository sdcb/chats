using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("Model")]
[Index("ModelKeyId", Name = "IX_Model_ModelKeyId")]
[Index("ModelReferenceId", Name = "IX_Model_ModelReferenceId")]
[Index("Name", Name = "IX_Model_Name")]
[Index("Order", Name = "IX_Model_Order")]
public partial class Model
{
    [Key]
    public short Id { get; set; }

    public short ModelKeyId { get; set; }

    public short ModelReferenceId { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    [StringLength(50)]
    public string? DeploymentName { get; set; }

    public short? Order { get; set; }

    [Column(TypeName = "decimal(9, 5)")]
    public decimal InputTokenPrice1M { get; set; }

    [Column(TypeName = "decimal(9, 5)")]
    public decimal OutputTokenPrice1M { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [InverseProperty("Model")]
    public virtual ICollection<ChatConfig> ChatConfigs { get; set; } = new List<ChatConfig>();

    [ForeignKey("ModelKeyId")]
    [InverseProperty("Models")]
    public virtual ModelKey ModelKey { get; set; } = null!;

    [ForeignKey("ModelReferenceId")]
    [InverseProperty("Models")]
    public virtual ModelReference ModelReference { get; set; } = null!;

    [InverseProperty("Model")]
    public virtual ICollection<UsageTransaction> UsageTransactions { get; set; } = new List<UsageTransaction>();

    [InverseProperty("Model")]
    public virtual ICollection<UserApiCache> UserApiCaches { get; set; } = new List<UserApiCache>();

    [InverseProperty("Model")]
    public virtual ICollection<UserModelUsage> UserModelUsages { get; set; } = new List<UserModelUsage>();

    [InverseProperty("Model")]
    public virtual ICollection<UserModel> UserModels { get; set; } = new List<UserModel>();

    [ForeignKey("ModelId")]
    [InverseProperty("Models")]
    public virtual ICollection<UserApiKey> ApiKeys { get; set; } = new List<UserApiKey>();
}
