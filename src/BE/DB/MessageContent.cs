using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("MessageContent")]
[Index("MessageId", Name = "IX_MessageContent2_Message")]
public partial class MessageContent
{
    [Key]
    public long Id { get; set; }

    public byte ContentTypeId { get; set; }

    public long MessageId { get; set; }

    [ForeignKey("ContentTypeId")]
    [InverseProperty("MessageContents")]
    public virtual MessageContentType ContentType { get; set; } = null!;

    [ForeignKey("MessageId")]
    [InverseProperty("MessageContents")]
    public virtual Message Message { get; set; } = null!;

    [InverseProperty("IdNavigation")]
    public virtual MessageContentBlob? MessageContentBlob { get; set; }

    [InverseProperty("IdNavigation")]
    public virtual MessageContentFile? MessageContentFile { get; set; }

    [InverseProperty("IdNavigation")]
    public virtual MessageContentText? MessageContentText { get; set; }

    [InverseProperty("IdNavigation")]
    public virtual MessageContentToolCall? MessageContentToolCall { get; set; }

    [InverseProperty("IdNavigation")]
    public virtual MessageContentToolCallResponse? MessageContentToolCallResponse { get; set; }
}
