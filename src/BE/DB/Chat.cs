using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("Chat")]
[Index("ChatGroupId", Name = "IX_Chat_ChatGroupId")]
[Index("IsTopMost", "UpdatedAt", Name = "IX_Chat_UpdatedAt")]
[Index("UserId", Name = "IX_Chat_UserId")]
public partial class Chat
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    public int? ChatGroupId { get; set; }

    [StringLength(50)]
    public string Title { get; set; } = null!;

    public bool IsArchived { get; set; }

    public bool IsTopMost { get; set; }

    public long? LeafMessageId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    [ForeignKey("ChatGroupId")]
    [InverseProperty("Chats")]
    public virtual ChatGroup? ChatGroup { get; set; }

    [InverseProperty("Chat")]
    public virtual ICollection<ChatShare> ChatShares { get; set; } = new List<ChatShare>();

    [InverseProperty("Chat")]
    public virtual ICollection<ChatSpan> ChatSpans { get; set; } = new List<ChatSpan>();

    [InverseProperty("Chat")]
    public virtual ICollection<ChatTurn> ChatTurns { get; set; } = new List<ChatTurn>();

    [ForeignKey("LeafMessageId")]
    [InverseProperty("Chats")]
    public virtual ChatTurn? LeafMessage { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("Chats")]
    public virtual User User { get; set; } = null!;

    [ForeignKey("ChatId")]
    [InverseProperty("Chats")]
    public virtual ICollection<ChatTag> ChatTags { get; set; } = new List<ChatTag>();
}
