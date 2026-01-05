using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ChatShare")]
[Index("ChatId", Name = "IX_ChatShare_Chat")]
public partial class ChatShare
{
    [Key]
    public int Id { get; set; }

    public int ChatId { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTime SnapshotTime { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("ChatId")]
    [InverseProperty("ChatShares")]
    public virtual Chat Chat { get; set; } = null!;
}
