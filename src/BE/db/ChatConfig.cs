using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

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

    [StringLength(50)]
    [Unicode(false)]
    public string? Effort { get; set; }

    public bool CodeExecutionEnabled { get; set; }

    [StringLength(20)]
    public string? ImageSize { get; set; }

    public int? ThinkingBudget { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string? Format { get; set; }

    public byte? Compression { get; set; }

    [InverseProperty("ChatConfig")]
    public virtual ICollection<ChatConfigMcp> ChatConfigMcps { get; set; } = new List<ChatConfigMcp>();

    [InverseProperty("ChatConfig")]
    public virtual ICollection<ChatPresetSpan> ChatPresetSpans { get; set; } = new List<ChatPresetSpan>();

    [InverseProperty("ChatConfig")]
    public virtual ICollection<ChatSpan> ChatSpans { get; set; } = new List<ChatSpan>();

    [ForeignKey("ModelId")]
    [InverseProperty("ChatConfigs")]
    public virtual Model Model { get; set; } = null!;
}
