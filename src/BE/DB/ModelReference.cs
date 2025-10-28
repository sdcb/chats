using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("ModelReference")]
[Index("Name", Name = "IX_ModelReference_Name")]
[Index("ProviderId", "Name", Name = "IX_ModelSetting_ProviderId+Type")]
public partial class ModelReference
{
    [Key]
    public short Id { get; set; }

    public short ProviderId { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    [StringLength(50)]
    public string? DisplayName { get; set; }

    public DateOnly? PublishDate { get; set; }

    [Column(TypeName = "decimal(3, 2)")]
    public decimal MinTemperature { get; set; }

    [Column(TypeName = "decimal(3, 2)")]
    public decimal MaxTemperature { get; set; }

    public bool AllowSearch { get; set; }

    public bool AllowVision { get; set; }

    public bool AllowSystemPrompt { get; set; }

    public bool AllowStreaming { get; set; }

    public byte ReasoningResponseKindId { get; set; }

    public int ContextWindow { get; set; }

    public int MaxResponseTokens { get; set; }

    public short? TokenizerId { get; set; }

    [Column(TypeName = "decimal(9, 5)")]
    public decimal InputTokenPrice1M { get; set; }

    [Column(TypeName = "decimal(9, 5)")]
    public decimal OutputTokenPrice1M { get; set; }

    [StringLength(3)]
    [Unicode(false)]
    public string CurrencyCode { get; set; } = null!;

    [ForeignKey("CurrencyCode")]
    [InverseProperty("ModelReferences")]
    public virtual CurrencyRate CurrencyCodeNavigation { get; set; } = null!;

    [ForeignKey("ProviderId")]
    [InverseProperty("ModelReferences")]
    public virtual ModelProvider Provider { get; set; } = null!;

    [ForeignKey("ReasoningResponseKindId")]
    [InverseProperty("ModelReferences")]
    public virtual ReasoningResponseKind ReasoningResponseKind { get; set; } = null!;

    [ForeignKey("TokenizerId")]
    [InverseProperty("ModelReferences")]
    public virtual Tokenizer? Tokenizer { get; set; }
}
