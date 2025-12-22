using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.Web.DB;

[Table("ChatGroup")]
[Index("UserId", "Rank", Name = "IX_ChatGroup_UserId")]
public partial class ChatGroup
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    public bool IsExpanded { get; set; }

    public short Rank { get; set; }

    [InverseProperty("ChatGroup")]
    public virtual ICollection<Chat> Chats { get; set; } = new List<Chat>();

    [ForeignKey("UserId")]
    [InverseProperty("ChatGroups")]
    public virtual User User { get; set; } = null!;
}
