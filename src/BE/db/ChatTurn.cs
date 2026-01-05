using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ChatTurn")]
[Index("ChatId", "SpanId", Name = "IX_Message_ChatSpan")]
public partial class ChatTurn
{
    [Key]
    public long Id { get; set; }

    public int ChatId { get; set; }

    public byte? SpanId { get; set; }

    public long? ParentId { get; set; }

    public bool IsUser { get; set; }

    public bool? ReactionId { get; set; }

    public int? ChatConfigId { get; set; }

    [ForeignKey("ChatId")]
    [InverseProperty("ChatTurns")]
    public virtual Chat Chat { get; set; } = null!;

    [ForeignKey("ChatConfigId")]
    [InverseProperty("ChatTurns")]
    public virtual ChatConfig? ChatConfig { get; set; }

    [InverseProperty("OwnerTurn")]
    public virtual ICollection<ChatDockerSession> ChatDockerSessions { get; set; } = new List<ChatDockerSession>();

    [InverseProperty("LeafTurn")]
    public virtual ICollection<Chat> Chats { get; set; } = new List<Chat>();

    [InverseProperty("Parent")]
    public virtual ICollection<ChatTurn> InverseParent { get; set; } = new List<ChatTurn>();

    [ForeignKey("ParentId")]
    [InverseProperty("InverseParent")]
    public virtual ChatTurn? Parent { get; set; }

    [InverseProperty("Turn")]
    public virtual ICollection<Step> Steps { get; set; } = new List<Step>();
}
