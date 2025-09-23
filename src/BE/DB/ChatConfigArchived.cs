using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.DB;

[Table("ChatConfigArchived")]
[Index("HashCode", Name = "IX_ChatConfigArchived_HashCode")]
public partial class ChatConfigArchived
{
    [Key]
    public int ChatConfigId { get; set; }

    public long HashCode { get; set; }

    [ForeignKey("ChatConfigId")]
    [InverseProperty("ChatConfigArchived")]
    public virtual ChatConfig ChatConfig { get; set; } = null!;
}
