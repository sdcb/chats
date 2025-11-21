using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("Model")]
[Index("ModelKeyId", Name = "IX_Model_ModelKeyId")]
[Index("Name", Name = "IX_Model_Name")]
public partial class Model
{
    [Key]
    public short Id { get; set; }

    public short ModelKeyId { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    [StringLength(50)]
    public string DeploymentName { get; set; } = null!;

    public short Order { get; set; }

    [Column(TypeName = "decimal(9, 5)")]
    public decimal InputTokenPrice1M { get; set; }

    [Column(TypeName = "decimal(9, 5)")]
    public decimal OutputTokenPrice1M { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public bool AllowSearch { get; set; }

    public bool AllowVision { get; set; }

    public bool AllowStreaming { get; set; }

    public bool ThinkTagParserEnabled { get; set; }

    [Column(TypeName = "decimal(3, 2)")]
    public decimal MinTemperature { get; set; }

    [Column(TypeName = "decimal(3, 2)")]
    public decimal MaxTemperature { get; set; }

    public int ContextWindow { get; set; }

    public int MaxResponseTokens { get; set; }

    public bool AllowCodeExecution { get; set; }

    [StringLength(50)]
    public string? ReasoningEffortOptions { get; set; }

    public bool AllowToolCall { get; set; }

    [StringLength(200)]
    public string? SupportedImageSizes { get; set; }

    public byte ApiType { get; set; }

    public bool UseAsyncApi { get; set; }

    public bool UseMaxCompletionTokens { get; set; }

    public bool IsLegacy { get; set; }

    public int? MaxThinkingBudget { get; set; }

    [InverseProperty("Model")]
    public virtual ICollection<ChatConfig> ChatConfigs { get; set; } = new List<ChatConfig>();

    [ForeignKey("ModelKeyId")]
    [InverseProperty("Models")]
    public virtual ModelKey ModelKey { get; set; } = null!;

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
