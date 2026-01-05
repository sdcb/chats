using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[PrimaryKey("ChatPresetId", "SpanId")]
[Table("ChatPresetSpan")]
[Index("ChatConfigId", Name = "IX_ChatPresetSpan_Config")]
public partial class ChatPresetSpan
{
    [Key]
    public int ChatPresetId { get; set; }

    [Key]
    public byte SpanId { get; set; }

    public int ChatConfigId { get; set; }

    public bool Enabled { get; set; }

    [ForeignKey("ChatConfigId")]
    [InverseProperty("ChatPresetSpans")]
    public virtual ChatConfig ChatConfig { get; set; } = null!;

    [ForeignKey("ChatPresetId")]
    [InverseProperty("ChatPresetSpans")]
    public virtual ChatPreset ChatPreset { get; set; } = null!;
}
