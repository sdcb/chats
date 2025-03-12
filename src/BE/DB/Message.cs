using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("Message")]
[Index("ChatId", "SpanId", Name = "IX_Message_ChatSpan")]
public partial class Message
{
    [Key]
    public long Id { get; set; }

    public int ChatId { get; set; }

    public byte? SpanId { get; set; }

    public long? ParentId { get; set; }

    public byte ChatRoleId { get; set; }

    public bool Edited { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("ChatId")]
    [InverseProperty("Messages")]
    public virtual Chat Chat { get; set; } = null!;

    [ForeignKey("ChatRoleId")]
    [InverseProperty("Messages")]
    public virtual ChatRole ChatRole { get; set; } = null!;

    [InverseProperty("LeafMessage")]
    public virtual ICollection<Chat> Chats { get; set; } = new List<Chat>();

    [InverseProperty("Parent")]
    public virtual ICollection<Message> InverseParent { get; set; } = new List<Message>();

    [InverseProperty("Message")]
    public virtual ICollection<MessageContent> MessageContents { get; set; } = new List<MessageContent>();

    [InverseProperty("Message")]
    public virtual MessageResponse? MessageResponse { get; set; }

    [ForeignKey("ParentId")]
    [InverseProperty("InverseParent")]
    public virtual Message? Parent { get; set; }
}
