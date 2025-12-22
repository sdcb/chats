using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.DB;

[Table("ChatConfig")]
[Index("ModelId", Name = "IX_ChatConfig_ModelId")]
public partial class ChatConfig
{
    [Key]
    public int Id { get; set; }

    public short ModelId { get; set; }

    public string? SystemPrompt { get; set; }

    public float? Temperature { get; set; }

    public bool WebSearchEnabled { get; set; }

    public int? MaxOutputTokens { get; set; }

    public byte ReasoningEffortId { get; set; }

    public bool CodeExecutionEnabled { get; set; }

    [StringLength(20)]
    public string? ImageSize { get; set; }

    public int? ThinkingBudget { get; set; }

    [InverseProperty("ChatConfig")]
    public virtual ChatConfigArchived? ChatConfigArchived { get; set; }

    [InverseProperty("ChatConfig")]
    public virtual ICollection<ChatConfigMcp> ChatConfigMcps { get; set; } = new List<ChatConfigMcp>();

    [InverseProperty("ChatConfig")]
    public virtual ICollection<ChatPresetSpan> ChatPresetSpans { get; set; } = new List<ChatPresetSpan>();

    [InverseProperty("ChatConfig")]
    public virtual ICollection<ChatSpan> ChatSpans { get; set; } = new List<ChatSpan>();

    [InverseProperty("ChatConfig")]
    public virtual ICollection<ChatTurn> ChatTurns { get; set; } = new List<ChatTurn>();

    [ForeignKey("ModelId")]
    [InverseProperty("ChatConfigs")]
    public virtual Model Model { get; set; } = null!;
}
