using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.DB;

[PrimaryKey("ChatId", "SpanId")]
[Table("ChatSpan")]
[Index("ChatConfigId", Name = "IX_ChatSpan_ChatConfigId")]
public partial class ChatSpan
{
    [Key]
    public int ChatId { get; set; }

    [Key]
    public byte SpanId { get; set; }

    public bool Enabled { get; set; }

    public int ChatConfigId { get; set; }

    [ForeignKey("ChatId")]
    [InverseProperty("ChatSpans")]
    public virtual Chat Chat { get; set; } = null!;

    [ForeignKey("ChatConfigId")]
    [InverseProperty("ChatSpans")]
    public virtual ChatConfig ChatConfig { get; set; } = null!;
}
