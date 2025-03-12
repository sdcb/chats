using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("ChatConfig")]
[Index("HashCode", Name = "IX_ChatConfig_HashCode")]
[Index("ModelId", Name = "IX_ChatConfig_ModelId")]
public partial class ChatConfig
{
    [Key]
    public int Id { get; set; }

    public long HashCode { get; set; }

    public short ModelId { get; set; }

    public string? SystemPrompt { get; set; }

    public float? Temperature { get; set; }

    public bool WebSearchEnabled { get; set; }

    public int? MaxOutputTokens { get; set; }

    public byte? ReasoningEffort { get; set; }

    [InverseProperty("ChatConfig")]
    public virtual ICollection<ChatSpan> ChatSpans { get; set; } = new List<ChatSpan>();

    [InverseProperty("ChatConfig")]
    public virtual ICollection<MessageResponse> MessageResponses { get; set; } = new List<MessageResponse>();

    [ForeignKey("ModelId")]
    [InverseProperty("ChatConfigs")]
    public virtual Model Model { get; set; } = null!;
}
