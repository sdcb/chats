using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.DB;

[Table("ChatTag")]
[Index("Name", Name = "IX_ChatTag_Name", IsUnique = true)]
public partial class ChatTag
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    [ForeignKey("ChatTagId")]
    [InverseProperty("ChatTags")]
    public virtual ICollection<Chat> Chats { get; set; } = new List<Chat>();
}
