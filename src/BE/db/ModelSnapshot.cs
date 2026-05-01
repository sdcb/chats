using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ModelSnapshot")]
[Index("ModelId", Name = "IX_ModelSnapshot_ModelId")]
[Index("ModelKeyId", Name = "IX_ModelSnapshot_ModelKeyId")]
[Index("ModelKeySnapshotId", Name = "IX_ModelSnapshot_ModelKeySnapshotId")]
public partial class ModelSnapshot
{
    [Key]
    public int Id { get; set; }

    public short ModelId { get; set; }

    [StringLength(100)]
    public string Name { get; set; } = null!;

    [StringLength(100)]
    public string DeploymentName { get; set; } = null!;

    public short ModelKeyId { get; set; }

    public int ModelKeySnapshotId { get; set; }

    public byte ApiTypeId { get; set; }

    [Column(TypeName = "decimal(9, 5)")]
    public decimal InputFreshTokenPrice1M { get; set; }

    [Column(TypeName = "decimal(9, 5)")]
    public decimal InputCachedTokenPrice1M { get; set; }

    [Column(TypeName = "decimal(9, 5)")]
    public decimal OutputTokenPrice1M { get; set; }

    public bool AllowSearch { get; set; }

    public bool AllowVision { get; set; }

    public bool AllowStreaming { get; set; }

    public bool AllowToolCall { get; set; }

    public bool AllowCodeExecution { get; set; }

    public bool ThinkTagParserEnabled { get; set; }

    [Column(TypeName = "decimal(3, 2)")]
    public decimal MinTemperature { get; set; }

    [Column(TypeName = "decimal(3, 2)")]
    public decimal MaxTemperature { get; set; }

    public int ContextWindow { get; set; }

    public int MaxResponseTokens { get; set; }

    [StringLength(100)]
    public string? ReasoningEffortOptions { get; set; }

    [StringLength(400)]
    public string? SupportedImageSizes { get; set; }

    public bool UseAsyncApi { get; set; }

    public bool UseMaxCompletionTokens { get; set; }

    public bool IsLegacy { get; set; }

    public int? MaxThinkingBudget { get; set; }

    public bool SupportsVisionLink { get; set; }

    public DateTime CreatedAt { get; set; }

    [InverseProperty("ModelSnapshot")]
    public virtual ICollection<ChatConfigSnapshot> ChatConfigSnapshots { get; set; } = new List<ChatConfigSnapshot>();

    [InverseProperty("CurrentSnapshot")]
    public virtual Model? Model { get; set; }

    [ForeignKey("ModelKeySnapshotId")]
    [InverseProperty("ModelSnapshots")]
    public virtual ModelKeySnapshot ModelKeySnapshot { get; set; } = null!;

    [InverseProperty("ModelSnapshot")]
    public virtual ICollection<UsageTransaction> UsageTransactions { get; set; } = new List<UsageTransaction>();

    [InverseProperty("ModelSnapshot")]
    public virtual ICollection<UserModelUsage> UserModelUsages { get; set; } = new List<UserModelUsage>();
}
