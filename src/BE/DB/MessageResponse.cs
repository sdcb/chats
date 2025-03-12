using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("MessageResponse")]
[Index("ChatConfigId", Name = "IX_MessageResponse_ChatConfigId")]
[Index("UsageId", Name = "IX_MessageResponse_UsageId")]
public partial class MessageResponse
{
    [Key]
    public long MessageId { get; set; }

    public long UsageId { get; set; }

    public bool? ReactionId { get; set; }

    public int ChatConfigId { get; set; }

    [ForeignKey("ChatConfigId")]
    [InverseProperty("MessageResponses")]
    public virtual ChatConfig ChatConfig { get; set; } = null!;

    [ForeignKey("MessageId")]
    [InverseProperty("MessageResponse")]
    public virtual Message Message { get; set; } = null!;

    [ForeignKey("UsageId")]
    [InverseProperty("MessageResponses")]
    public virtual UserModelUsage Usage { get; set; } = null!;
}
